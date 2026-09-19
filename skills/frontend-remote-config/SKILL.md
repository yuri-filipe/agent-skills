---
name: frontend-remote-config
description: "Migra frontends Vite/Vue da Infinite de VITE_* para configuração síncrona gerada via Consul no build. Use ao configurar remote config; não cobre Angular."
---

# Frontend Remote Config (Consul + Vite)

Replica, em qualquer frontend **Vite/Vue**, o padrão de configuração remota validado em
`Infinite.Optics.Frontend`: as URLs (e quaisquer parâmetros) deixam de vir de
`import.meta.env.VITE_*` e passam a ser injetadas como **constante síncrona em tempo de
build/dev** por um plugin Vite, a partir de um JSON que a pipeline baixa do Consul.

Princípios (não os viole):
- **Sem `import.meta.env.VITE_*`** para configuração de runtime.
- **Sem fetch assíncrono** no boot e **sem getter avaliado a cada request** (isso é má prática aqui).
- A config é uma **constante síncrona** vinda do módulo virtual `virtual:app-config`.
- **Um único type `AppConfig`** vale para dev (local) e build (Consul), e é **extensível**.

## Contrato com a pipeline DevOps (precisão — não invente)

O template `pipelines/frontend-pipeline.yaml@Pipelines` (repo `DevOps/Pipelines`) já implementa
o lado da pipeline. No stage de deploy, ANTES do `npm run build`, o script
`scripts/ci/generate-runtime-config.sh` baixa a chave do Consul e grava em
**`public/config/config.json`** (variável `OUTPUT_FILE`, default `public/config/config.json`).

- Chave no Consul KV: **`frontends/<app>/<env>/config.json`** (ex.: `frontends/otica/qa/config.json`,
  `frontends/otica/prd/config.json`). `<env>` (`qa`/`prd`) é resolvido automaticamente pela pipeline.
- No `azure-pipelines.yml` do frontend, o parâmetro **`consulProject` recebe só o nome do app,
  sem `/`** (ex.: `consulProject: 'otica'`); a pipeline acrescenta `<env>` e `config.json`.
- `consulProject` é **obrigatório**: o stage `ValidateNode` falha de propósito se estiver vazio.
- Credenciais (`CONSUL_DOMAIN`, `CONSUL_FRONTEND_TOKEN`) vêm do Variable Group — nunca no código.

Como o `public/config/config.json` está em `public/`, o Vite o copia para `dist/`; o plugin
deste skill o LÊ no build e embute no bundle. Cada deploy (QA/PRD) faz seu próprio build com
o seu próprio `config.json`, então o mesmo padrão produz a imagem correta por ambiente.

> Pré-requisito de operação (fora do código): a chave precisa existir no Consul
> (`frontends/<app>/qa/config.json` e `.../prd/config.json`) com o JSON do `AppConfig`.
> Senão o passo "Gerar config.json" falha (HTTP != 200).

## Passo a passo

Os arquivos canônicos estão em `templates/` deste skill (estrutura espelha o destino).
Copie-os e adapte os pontos marcados com 👉.

### 1. Type compartilhado — `src/config/types.ts`
Copie `templates/src/config/types.ts`.
👉 Ajuste as chaves conhecidas (`apiBaseUrl`, `authApiBaseUrl`) ao que o app realmente usa.
A index signature `[key: string]: unknown` permite parâmetros extras sem quebrar o build.

### 2. Plugin Vite — `vite/app-config.ts`
Copie `templates/vite/app-config.ts` **verbatim**. Ele:
- em `build` lê `public/config/config.json` (Consul); se ausente, fallback p/ local com `warn`;
- em `serve` (dev) lê `config/app-config.local.json`;
- emite `export default <json>` (constante síncrona) no módulo `virtual:app-config`.
👉 Não mude os caminhos `public/config/config.json` (contrato da pipeline) nem
`config/app-config.local.json` sem necessidade.

### 3. Registrar no `vite.config.ts`
```ts
import { appConfig } from './vite/app-config'
// ...
export default defineConfig({
  plugins: [
    appConfig(), // injeta a config (Consul no build / local no dev) via `virtual:app-config`
    // ...demais plugins
  ],
})
```

### 4. Tipo do módulo virtual — `src/config/virtual-modules.d.ts`
Copie `templates/src/config/virtual-modules.d.ts`.
👉 Usa o alias `@/config/types`. Se o projeto não tiver alias `@` → `src`, troque por caminho relativo.
Garanta que o `.d.ts` esteja no `include` do `tsconfig.json` (normalmente `src/**/*` cobre).

### 5. Barrel — `src/config/index.ts`
Copie `templates/src/config/index.ts`. Consumo no app: `import { appConfig } from '@/config'`.

### 6. Defaults de dev + exemplo
- Copie `templates/config/app-config.local.json` → `config/app-config.local.json` (commitado, localhost).
- Copie `templates/public/config/config.example.json` → `public/config/config.example.json` (commitado, exemplo do Consul).
👉 Use as MESMAS chaves do `AppConfig` nos dois JSONs.

### 7. `.gitignore`
Adicione (o arquivo é gerado pela pipeline; não deve ser commitado):
```
# Runtime config gerado pela pipeline a partir do Consul (ver vite/app-config.ts)
public/config/config.json
```

### 8. Reescrever os consumidores (uso SÍNCRONO)
Substitua cada leitura `import.meta.env.VITE_*` por `appConfig.<chave>`.
Não recrie a baseURL por request nem use getters lazy.

- **ofetch** (`ofetch.create`):
  ```ts
  import { appConfig } from '@/config'
  export const $api = ofetch.create({ baseURL: appConfig.apiBaseUrl, /* ... */ })
  ```
- **VueUse `createFetch`**:
  ```ts
  import { appConfig } from '@/config'
  export const useApi = createFetch({ baseUrl: appConfig.apiBaseUrl, /* ... */ })
  ```
- **axios**: `axios.create({ baseURL: appConfig.apiBaseUrl })`.

Remova quaisquer `export const apiBaseUrl = import.meta.env...` antigos e atualize quem os importava.

### 9. Limpar variáveis de ambiente
- `env.d.ts`: remova o bloco `interface ImportMetaEnv { ... VITE_* }` e `interface ImportMeta`.
  `import.meta.env.BASE_URL` continua tipado por `vite/client` (confira `types` no `tsconfig.json`).
- Remova `.env.example` e os `VITE_*` dos `.env`. (Em geral `.env*` é gitignored; avise os devs para apagar o local.)
- **Não** é preciso boot assíncrono em `main.ts` — a config é constante de build/dev.

### 10. `azure-pipelines.yml` do frontend
Adicione o parâmetro obrigatório `consulProject` (só o nome do app, sem `/`):
```yaml
extends:
  template: pipelines/frontend-pipeline.yaml@Pipelines
  parameters:
    # ...demais parâmetros (dockerImageName, path, baseDomain, nodeVersion, etc.)
    # A pipeline baixa do Consul e compõe frontends/<app>/<env>/config.json automaticamente
    consulProject: "<app>"
```
👉 `<app>` = nome da aplicação (normalmente igual a `dockerImageName`/`path`, ex.: `otica`).

## Checklist de adaptação por projeto
- [ ] Nome do app no `consulProject` (sem `/`) e a chave provisionada no Consul (`frontends/<app>/{qa,prd}/config.json`).
- [ ] Chaves do `AppConfig` = parâmetros que o app usa (idênticas nos dois JSONs e no Consul).
- [ ] Alias `@` → `src` existe? Se não, ajustar imports `@/config` e `@/config/types`.
- [ ] Cliente HTTP do projeto (ofetch / axios / VueUse) recebendo `appConfig.*`.
- [ ] Todas as ocorrências de `import.meta.env.VITE_*` de config removidas (`grep -rn "import.meta.env.VITE_" src`).

## Verificação
1. **Dev**: `npm run dev` → `appConfig` vem de `config/app-config.local.json`; no DevTools (Network),
   as requisições usam a baseURL local.
2. **Build local**: crie `public/config/config.json` com URLs distintas → `npm run build` + `npm run preview`
   → as requisições usam essas URLs (config embutida). Sem o arquivo, o build usa os defaults locais com `warn`.
3. **Extensibilidade**: adicione uma chave em `AppConfig` + nos dois JSONs e leia via `appConfig.minhaChave`.
4. **Tipos**: `npm run typecheck` (ou `vue-tsc --noEmit`) sem erros NOVOS nos arquivos tocados.
5. **Pipeline**: push em `feature/*` → stage `DeployQA`; conferir o passo "Gerar config.json"
   (chave `frontends/<app>/qa/config.json`) e que o build embute a config.

## Notas
- `auto-imports.d.ts` / `components.d.ts` são gerados; ao remover exports antigos (ex.: `apiBaseUrl`),
  entradas obsoletas regeneram no próximo dev/build. Use imports explícitos de `@/config` (não dependa de auto-import).
- Este skill cobre **apenas o lado do frontend**. O template/scripts em `DevOps/Pipelines` já suportam o fluxo
  e não devem ser alterados por aqui.
- Datas e fusos (DTOs, formulários, filtros e exibição de datas vindas das APIs) ficam fora deste skill —
  use o skill `infinite-frontend-datetime`.
- Consumo das APIs (paginação por headers, erros 400/401/403/422, JSON estrito) fica no skill
  `infinite-frontend-api`; este skill só fornece a URL base.
