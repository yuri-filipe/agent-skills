# Agent Skills

Fonte oficial das skills de [yuri-filipe](https://github.com/yuri-filipe), com instruções,
referências e templates da Infinite. O conteúdo canônico vive em `skills/`.

| Skill | Finalidade |
|---|---|
| [infinite-dotnet-api](skills/infinite-dotnet-api/SKILL.md) | Estrutura de APIs .NET, módulos CQRS e NodaTime |
| [infinite-api-logs](skills/infinite-api-logs/SKILL.md) | Logs estruturados úteis em APIs .NET |
| [infinite-auth-zitadel](skills/infinite-auth-zitadel/SKILL.md) | Autenticação e autorização com Zitadel |
| [infinite-angular-build](skills/infinite-angular-build/SKILL.md) | Contrato de build/deploy Angular |
| [frontend-remote-config](skills/frontend-remote-config/SKILL.md) | Configuração de frontend Vite/Vue via Consul |
| [infinite-frontend-datetime](skills/infinite-frontend-datetime/SKILL.md) | Datas e fusos em frontends que consomem APIs NodaTime |

## 1. Clonar e preparar

Pré-requisitos: Git, Node.js 22 ou superior e Windows PowerShell 5.1 ou PowerShell 7+ para os comandos `.ps1`.
Os comandos Node também funcionam em Linux/macOS.

```powershell
git clone https://github.com/yuri-filipe/agent-skills.git
Set-Location agent-skills
npm ci --ignore-scripts
npm run validate
npm test
```

## 2. Instalar no Codex e Claude Code

```powershell
./scripts/install.ps1 -Target All
```

Ou diretamente com Node:

```sh
node scripts/skills.mjs install --target all
```

Isso instala as skills em `~/.agents/skills` (Codex) e `~/.claude/skills` (Claude Code).
Para instalar apenas em um aplicativo, use `-Target Codex` ou `-Target ClaudeCode`.
O instalador recusa pastas existentes não gerenciadas ou com alterações locais.

Se você já possui as mesmas skills, `-AdoptExisting` permite adotar **somente cópias
idênticas**. Caso os arquivos sejam diferentes, compare-os, preserve seu conteúdo e
mova a pasta conflitante para fora do diretório de skills antes de instalar.
Não há modo de sobrescrita forçada. Veja [migração e backups](docs/installation.md).

Abra uma nova sessão do aplicativo para conferir a descoberta das skills. No Codex,
use `$infinite-dotnet-api`; no Claude Code, `/infinite-dotnet-api`.

## 3. Atualizar pelo GitHub

```powershell
./scripts/update.ps1 -Target All
```

O comando exige um clone limpo na branch `main`, verifica o `origin`, faz fetch e
fast-forward, instala as dependências do lockfile e executa o instalador atualizado.
Conflitos locais interrompem a instalação. O commit instalado fica registrado em
`.agent-skills-install.json` dentro de cada pasta gerenciada.

Execute antes de iniciar novas sessões. Nenhum agendamento é instalado e o GitHub
não é consultado a cada mensagem. Veja [atualização e versões](docs/updating.md).

## 4. Claude web/Cowork e plugins

```powershell
npm run package
```

Em `dist/v1.0.0/` será gerado um ZIP individual por skill, dois ZIPs de plugins
(Codex/OpenAI e Claude), checksums SHA-256 e um arquivo de proveniência.

No Claude web/Cowork, envie o ZIP individual em **Customize → Skills → Upload a skill**.
Para ChatGPT, a instalação/distribuição depende da interface de skills/plugins
disponível na conta. Publicar este repositório ou gerar um ZIP **não instala** nada
no ChatGPT. O pacote OpenAI contém o manifesto de compatibilidade
`.codex-plugin/plugin.json`; não é uma integração MCP.

Os pacotes são derivados do mesmo conteúdo. Instale pelo script **ou** pelo plugin
na mesma ferramenta, para evitar duplicação. Veja [compatibilidade](docs/compatibility.md).

## 5. Alterar e publicar uma versão

Edite `skills/` neste clone, execute os checks e publique as mudanças por PR.
As regras de branch devem ser configuradas separadamente no GitHub; a presença de
um workflow não as habilita automaticamente.

```powershell
git switch -c feature/minha-skill
# Edite os arquivos desejados
npm run validate
npm test
npm run package
git add skills
git commit -m "Atualiza skill"
git push -u origin feature/minha-skill
```

Para uma release, atualize `package.json`, `package-lock.json`, os dois manifestos
em `packaging/` e `CHANGELOG.md` com a mesma versão, valide e integre em `main`.
Depois crie e envie uma tag correspondente, por exemplo `v1.0.0` para a versão inicial.
O workflow `release.yml` validará, testará e publicará os ZIPs em GitHub Releases.

## Organização

```text
skills/                 instruções e recursos canônicos
packaging/              manifestos por plataforma, sem duplicar as skills
scripts/                validação, instalação, atualização e empacotamento
test/                   testes do instalador e pacotes em pastas temporárias
docs/                   instalação, atualização e limites de compatibilidade
.github/workflows/      validação Windows/Linux e publicação por tag
```

As skills usam bibliotecas e pipelines da Infinite que podem exigir acesso privado.
Publicar as instruções não publica essas dependências. Nenhum token deve ser incluído.
A licença de redistribuição ainda não foi definida pelo autor; repositório público
não equivale a concessão de uma licença open source.
