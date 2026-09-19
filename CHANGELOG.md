# Changelog

## Unreleased

- Nova skill `infinite-frontend-api`: contrato HTTP das APIs Infinite para frontends (rotas,
  paginação por headers e limite de CORS, formatos de erro, JSON estrito, Bearer) e template
  TypeScript `api-contract.ts`.
- Skills alinhadas ao código atual das libs `Infinite.Core`, `.Postgres`, `.WebHost` e `.Consul`:
  - `infinite-dotnet-api`: namespaces `Extensoes`, JSON NodaTime já registrado pela WebHost,
    Consul descartando `appsettings`, design-time lendo Consul, `consul-config.json` de exemplo,
    auditoria com organização, novos métodos de repositório e contrato HTTP (422/400/headers de paginação).
  - `infinite-api-logs`: exceções de handler são registradas pelo `ErrorHandlingBehavior`.
  - `infinite-auth-zitadel`: auditoria automática e `IAuditContextAccessor`.
  - `infinite-frontend-datetime`: regras do JSON estrito da API.
- `infinite-dotnet-api`: contrato de datas e fusos com NodaTime (`references/datas-e-fusos.md`), JSON ISO-8601 UTC e templates ajustados.
- Nova skill `infinite-frontend-datetime` para datas e fusos nos frontends.
- `infinite-angular-build` e `frontend-remote-config` apontam para a nova skill de datas.
- Scripts de instalação, atualização, validação e empacotamento aceitam Windows PowerShell 5.1.
- CI verifica os pontos de entrada também no Windows PowerShell, além do PowerShell 7.

## 1.0.0

- Importação de cinco skills pessoais da Infinite com referências e templates.
- Descrições do frontmatter concisas, até 200 caracteres, como strings YAML explícitas.
- Referência externa do template Angular explicitada como caminho no repo de pipelines.
- Instalação gerenciada para Codex e Claude Code, com detecção de conflitos e backups.
- Atualização por Git, validação, testes de integração e pacotes ZIP por plataforma.
- Workflows de validação e publicação de releases por tag.

Esta entrada descreve o conteúdo da versão; uma release só existe após a execução
bem-sucedida do workflow da tag correspondente.
