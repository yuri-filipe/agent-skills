import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import { parse } from 'yaml';
import { zipSync } from 'fflate';

export const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sourceUrl = 'https://github.com/yuri-filipe/agent-skills.git';
const marker = '.agent-skills-install.json';
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const json = file => JSON.parse(fs.readFileSync(file, 'utf8'));

export function files(root) {
  const result = {};
  function walk(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const full = path.join(directory, entry.name);
      if (entry.isSymbolicLink()) throw new Error(`Link não permitido: ${full}`);
      if (entry.isDirectory()) walk(full);
      else if (entry.isFile()) result[path.relative(root, full).split(path.sep).join('/')] = fs.readFileSync(full);
      else throw new Error(`Tipo de arquivo não suportado: ${full}`);
    }
  }
  walk(root);
  return result;
}

function hashes(contents) {
  return Object.fromEntries(Object.keys(contents).sort().map(name => [name, hash(contents[name])]));
}
function equal(a, b) {
  return JSON.stringify(Object.entries(a).sort()) === JSON.stringify(Object.entries(b).sort());
}
function ensureNoLinks(target) {
  for (let current = path.resolve(target); ; current = path.dirname(current)) {
    if (fs.existsSync(current) && fs.lstatSync(current).isSymbolicLink()) throw new Error(`Destino contém link: ${current}`);
    if (path.dirname(current) === current) break;
  }
}

export function validate(root = repository) {
  const skillsRoot = path.join(root, 'skills');
  const names = fs.readdirSync(skillsRoot).sort();
  if (!names.length) throw new Error('Nenhuma skill encontrada.');
  for (const name of names) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(name) || name.length > 64) throw new Error(`Nome inválido: ${name}`);
    const skillRoot = path.join(skillsRoot, name);
    if (fs.lstatSync(skillRoot).isSymbolicLink()) throw new Error(`Skill não pode ser link: ${name}`);
    const contents = files(skillRoot);
    const text = contents['SKILL.md']?.toString('utf8');
    const match = text?.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n([\s\S]+)$/);
    if (!match) throw new Error(`SKILL.md sem frontmatter/corpo: ${name}`);
    const metadata = parse(match[1]);
    if (metadata.name !== name || typeof metadata.description !== 'string' || !metadata.description.trim() || metadata.description.length > 200) {
      throw new Error(`Metadados inválidos: ${name}`);
    }
    for (const [file, data] of Object.entries(contents)) {
      const content = data.toString('utf8');
      // Narrow credential patterns; this is not a complete secret scanner.
      if (/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}/.test(content)) {
        throw new Error(`Possível credencial em ${name}/${file}; conteúdo omitido.`);
      }
      if (!file.endsWith('.md')) continue;
      for (const link of content.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)) {
        const target = link[1].split('#')[0];
        if (!target || /^(?:https?:|mailto:)/.test(target)) continue;
        const resolved = path.resolve(skillRoot, path.dirname(file), target);
        if (!resolved.startsWith(skillRoot + path.sep) || !fs.existsSync(resolved)) throw new Error(`Referência inválida: ${name}/${file} -> ${target}`);
      }
      for (const ref of content.matchAll(/`((?:references|templates|assets)\/[\w./-]+)`/g)) {
        if (!fs.existsSync(path.join(skillRoot, ref[1]))) throw new Error(`Recurso ausente: ${name}/${ref[1]}`);
      }
    }
  }
  const version = json(path.join(root, 'package.json')).version;
  for (const platform of ['codex', 'claude']) {
    const manifest = json(path.join(root, 'packaging', platform, 'plugin.json'));
    if (manifest.name !== 'infinite-skills' || manifest.version !== version || !manifest.description) throw new Error(`Manifesto ${platform} inconsistente.`);
    if (platform === 'codex') {
      if (!manifest.author?.name || manifest.skills !== './skills/') throw new Error('Autor/caminho ausente no manifesto Codex.');
      for (const field of ['displayName', 'shortDescription', 'longDescription', 'developerName', 'category']) {
        if (!manifest.interface?.[field]) throw new Error(`Campo de interface ausente: ${field}`);
      }
      if (!Array.isArray(manifest.interface.capabilities) || !Array.isArray(manifest.interface.defaultPrompt)) throw new Error('Capabilities/defaultPrompt inválidos.');
    }
  }
  return names;
}

function git(root, ...args) {
  return execFileSync('git', ['-C', root, ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
}
function commit(root) {
  try { return git(root, 'rev-parse', 'HEAD'); } catch { return null; }
}

export function install({ root = repository, target = 'all', profile = os.homedir(), adopt = false } = {}) {
  const platforms = target === 'all' ? ['codex', 'claude'] : [target.toLowerCase()];
  if (platforms.some(p => !['codex', 'claude'].includes(p))) throw new Error('Target deve ser all, codex ou claude.');
  const plans = [];
  // Preflight all destinations before any write.
  for (const platform of platforms) {
    const base = path.resolve(profile, platform === 'codex' ? '.agents/skills' : '.claude/skills');
    ensureNoLinks(base);
    for (const name of validate(root)) {
      const source = path.join(root, 'skills', name);
      const destination = path.join(base, name);
      ensureNoLinks(destination);
      const expected = hashes(files(source));
      if (fs.existsSync(destination)) {
        const actualContents = files(destination);
        const state = actualContents[marker] ? JSON.parse(actualContents[marker].toString()) : null;
        delete actualContents[marker];
        const actual = hashes(actualContents);
        if (!state) {
          if (!adopt || !equal(actual, expected)) throw new Error(`Skill não gerenciada: ${destination}. -AdoptExisting só aceita conteúdo idêntico.`);
        } else if (state.source !== sourceUrl || state.name !== name || !equal(actual, state.hashes)) {
          throw new Error(`Conflito ou alteração local: ${destination}. Preserve suas alterações antes de atualizar.`);
        }
      }
      plans.push({ source, destination, expected, name, base });
    }
  }
  for (const plan of plans) {
    fs.mkdirSync(plan.base, { recursive: true });
    const staging = fs.mkdtempSync(path.join(plan.base, '.agent-skills-stage-'));
    fs.cpSync(plan.source, staging, { recursive: true });
    fs.writeFileSync(path.join(staging, marker), JSON.stringify({ source: sourceUrl, name: plan.name, commit: commit(root), installedAt: new Date().toISOString(), hashes: plan.expected }, null, 2) + '\n');
    let backup;
    if (fs.existsSync(plan.destination)) {
      const backups = path.resolve(profile, '.agent-skills-backups');
      ensureNoLinks(backups);
      fs.mkdirSync(backups, { recursive: true });
      backup = path.join(backups, `${plan.name}-${crypto.randomUUID()}`);
      fs.renameSync(plan.destination, backup);
    }
    try { fs.renameSync(staging, plan.destination); }
    catch (error) {
      if (backup) fs.renameSync(backup, plan.destination);
      throw error;
    }
  }
  return plans.map(p => p.destination);
}

export function packageSkills(root = repository) {
  const names = validate(root);
  const version = json(path.join(root, 'package.json')).version;
  const output = path.join(root, 'dist', `v${version}`);
  if (fs.existsSync(output)) throw new Error(`Pacotes já existem: ${output}. Preserve ou mova essa pasta antes de gerar novamente.`);
  fs.mkdirSync(output, { recursive: true });
  const generated = [];
  const writeZip = (name, entries) => {
    const target = path.join(output, name);
    fs.writeFileSync(target, zipSync(entries, { level: 9 }));
    generated.push(target);
  };
  const skillEntries = {};
  for (const name of names) {
    const entries = files(path.join(root, 'skills', name));
    writeZip(`${name}-${version}.zip`, Object.fromEntries(Object.entries(entries).map(([file, data]) => [`${name}/${file}`, data])));
    for (const [file, data] of Object.entries(entries)) skillEntries[`infinite-skills/skills/${name}/${file}`] = data;
  }
  for (const platform of ['codex', 'claude']) {
    writeZip(`infinite-skills-${platform}-${version}.zip`, {
      ...skillEntries,
      [`infinite-skills/.${platform}-plugin/plugin.json`]: fs.readFileSync(path.join(root, 'packaging', platform, 'plugin.json'))
    });
  }
  fs.writeFileSync(path.join(output, 'SHA256SUMS.txt'), generated.map(file => `${hash(fs.readFileSync(file))}  ${path.basename(file)}`).join('\n') + '\n');
  fs.writeFileSync(path.join(output, 'provenance.json'), JSON.stringify({ repository: sourceUrl, version, commit: commit(root), skills: names }, null, 2) + '\n');
  return output;
}

export function update({ root = repository, target = 'all', profile = os.homedir() } = {}) {
  if (git(root, 'remote', 'get-url', 'origin') !== sourceUrl) throw new Error('Origin diferente do repositório oficial.');
  if (git(root, 'status', '--porcelain')) throw new Error('O clone tem alterações locais. Faça commit ou preserve-as antes de atualizar.');
  if (git(root, 'branch', '--show-current') !== 'main') throw new Error('Atualização automática exige a branch main.');
  git(root, 'fetch', 'origin', 'main');
  const head = git(root, 'rev-parse', 'HEAD');
  const upstream = git(root, 'rev-parse', 'origin/main');
  if (head !== upstream) {
    git(root, 'merge-base', '--is-ancestor', head, upstream);
    git(root, 'merge', '--ff-only', upstream);
  }
  // Reload code and lockfile from the fetched revision; never run the old installer.
  const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const command = process.platform === 'win32' ? 'cmd.exe' : npm;
  const args = process.platform === 'win32' ? ['/d', '/c', 'npm ci --ignore-scripts'] : ['ci', '--ignore-scripts'];
  execFileSync(command, args, { cwd: root, stdio: 'inherit' });
  execFileSync(process.execPath, [path.join(root, 'scripts/skills.mjs'), 'install', '--target', target, '--profile', profile], { stdio: 'inherit' });
  return upstream;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const [command, ...args] = process.argv.slice(2);
    const options = {};
    for (let i = 0; i < args.length; i++) {
      if (args[i] === '--adopt') options.adopt = true;
      else if (['--target', '--profile'].includes(args[i]) && args[i + 1]) options[args[i].slice(2)] = args[++i];
      else throw new Error(`Argumento inválido: ${args[i]}`);
    }
    if (command === 'validate') console.log(`OK: ${validate().length} skills.`);
    else if (command === 'install') console.log(install(options).join('\n'));
    else if (command === 'package') console.log(packageSkills());
    else if (command === 'update') console.log(`Commit instalado: ${update(options)}`);
    else throw new Error('Use validate, install, package ou update.');
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
