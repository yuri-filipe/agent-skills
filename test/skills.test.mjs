import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { unzipSync } from 'fflate';
import { install, validate, packageSkills, repository, update } from '../scripts/skills.mjs';

function fixture() {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'agent-skills-test-'));
  const root = path.join(directory, 'repo');
  fs.mkdirSync(root);
  for (const name of ['skills', 'packaging', 'package.json']) fs.cpSync(path.join(repository, name), path.join(root, name), { recursive: true });
  return { root, profile: path.join(directory, 'profile') };
}
// Fixtures are retained in the OS temp directory for diagnosis; no user files are removed.
test('validates the complete imported catalog', () => {
  assert.equal(validate().length, 7);
});
test('installs both platforms, preserves unrelated files and blocks local edits before writes', () => {
  const options = fixture();
  assert.equal(install(options).length, 14);
  const extra = path.join(options.profile, '.agents/skills/unrelated.txt');
  fs.writeFileSync(extra, 'preserve');
  install(options);
  assert.equal(fs.readFileSync(extra, 'utf8'), 'preserve');
  const edited = path.join(options.profile, '.claude/skills/infinite-dotnet-api/SKILL.md');
  fs.appendFileSync(edited, '\nuser edit\n');
  const firstMarker = path.join(options.profile, '.agents/skills/frontend-remote-config/.agent-skills-install.json');
  const before = fs.readFileSync(firstMarker, 'utf8');
  assert.throws(() => install(options), /alteração local/);
  assert.equal(fs.readFileSync(firstMarker, 'utf8'), before);
  assert.match(fs.readFileSync(edited, 'utf8'), /user edit/);
});
test('refuses unmanaged collisions and adopts only identical content', () => {
  const options = fixture();
  const destination = path.join(options.profile, '.agents/skills/frontend-remote-config');
  fs.cpSync(path.join(options.root, 'skills/frontend-remote-config'), destination, { recursive: true });
  assert.throws(() => install(options), /não gerenciada/);
  assert.equal(install({ ...options, adopt: true }).length, 14);
  const other = fixture();
  const conflict = path.join(other.profile, '.agents/skills/frontend-remote-config');
  fs.mkdirSync(conflict, { recursive: true });
  fs.writeFileSync(path.join(conflict, 'user.txt'), 'keep');
  assert.throws(() => install({ ...other, adopt: true }), /não gerenciada/);
  assert.equal(fs.readFileSync(path.join(conflict, 'user.txt'), 'utf8'), 'keep');
});
test('updates changed source, drops obsolete managed files and retains a backup', () => {
  const options = fixture();
  const resource = path.join(options.root, 'skills/frontend-remote-config/old.txt');
  fs.writeFileSync(resource, 'previous');
  install(options);
  fs.renameSync(resource, path.join(path.dirname(resource), 'new.txt'));
  install(options);
  const installed = path.join(options.profile, '.agents/skills/frontend-remote-config');
  assert.equal(fs.existsSync(path.join(installed, 'old.txt')), false);
  assert.equal(fs.readFileSync(path.join(installed, 'new.txt'), 'utf8'), 'previous');
  const backups = path.join(options.profile, '.agent-skills-backups');
  assert.ok(fs.readdirSync(backups).some(name => fs.existsSync(path.join(backups, name, 'old.txt'))));
});
test('packages individual skills and hidden plugin manifests with resources intact', () => {
  const { root } = fixture();
  const output = packageSkills(root);
  assert.equal(fs.readdirSync(output).filter(n => n.endsWith('.zip')).length, 9);
  for (const platform of ['codex', 'claude']) {
    const zip = unzipSync(fs.readFileSync(path.join(output, `infinite-skills-${platform}-1.0.0.zip`)));
    assert.ok(zip[`infinite-skills/.${platform}-plugin/plugin.json`]);
    assert.ok(zip['infinite-skills/skills/infinite-dotnet-api/templates/servico/Program.cs']);
  }
  const zip = unzipSync(fs.readFileSync(path.join(output, 'infinite-api-logs-1.0.0.zip')));
  assert.ok(zip['infinite-api-logs/SKILL.md']);
  assert.ok(zip['infinite-api-logs/references/niveis.md']);
  assert.throws(() => packageSkills(root), /já existem/);
});
test('rejects malformed YAML and missing referenced resources', () => {
  const { root } = fixture();
  const file = path.join(root, 'skills/frontend-remote-config/SKILL.md');
  const original = fs.readFileSync(file, 'utf8');
  fs.writeFileSync(file, original.replace(/^description:.*$/m, 'description: [broken'));
  assert.throws(() => validate(root));
  fs.writeFileSync(file, original + '\n[Missing](references/absent.md)\n');
  assert.throws(() => validate(root), /Referência inválida/);
});
test('update rejects unexpected origins, dirty clones and non-main branches before fetching', () => {
  const options = fixture();
  const git = (...args) => execFileSync('git', ['-C', options.root, ...args], { stdio: 'pipe' });
  git('init', '-b', 'main');
  git('config', 'user.name', 'Skill test');
  git('config', 'user.email', 'test@example.invalid');
  git('add', '.');
  git('commit', '-m', 'fixture');
  git('remote', 'add', 'origin', 'https://example.invalid/other.git');
  assert.throws(() => update(options), /Origin diferente/);
  git('remote', 'set-url', 'origin', 'https://github.com/yuri-filipe/agent-skills.git');
  fs.writeFileSync(path.join(options.root, 'local.txt'), 'local');
  assert.throws(() => update(options), /alterações locais/);
  git('add', 'local.txt');
  git('commit', '-m', 'local fixture');
  git('switch', '-c', 'feature/test');
  assert.throws(() => update(options), /branch main/);
  assert.equal(fs.existsSync(options.profile), false);
});
