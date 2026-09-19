---
name: infinite-angular-build
description: "Configura e revisa build/deploy Angular nas pipelines Azure DevOps da Infinite. Use para azure-pipelines.yml, outputPath e falhas de build; não cobre Vite/Vue ou APIs .NET."
---

# Build Angular para a pipeline Azure DevOps da Infinite

Este skill descreve, com precisão, o contrato entre um repositório Angular e o template
`pipelines/angular-frontend-pipeline.yaml@Pipelines` (repo `DevOps/Pipelines`). As regras aqui
foram conferidas lendo o template, os `scripts/` e o `Dockerfile.angular` reais — não são
suposição. Se o repo `DevOps/Pipelines` estiver clonado localmente, os arquivos-fonte são:

- `pipelines/angular-frontend-pipeline.yaml` — stages `ValidateAngular` → `DeployQA`/`DeployPRD`.
- `pipelines/templates/angular-frontend/validate-angular.yaml` e `deploy-stage.yaml`.
- `scripts/node/build-angular.sh` — build do Angular fora do container (sanity check).
- `scripts/ci/generate-runtime-config.sh` — baixa o `config.json` do Consul.
- `scripts/ci/docker-build-image.sh` — builda a imagem com `docker buildx`.
- `images/node-22/Dockerfile.angular` — o Dockerfile real usado (não fica no projeto).

Se algo aqui divergir do comportamento observado, esses arquivos vencem este documento.

## O que a pipeline faz, em ordem

1. **`ValidateAngular`** (roda no agente, sem Docker): instala Node na versão de
   `nodeVersion`, valida que é compatível com Angular 21, resolve o diretório do workspace
   Angular a partir de `solutionPath`, autentica o npm (`npmAuthenticate@0` usando o `.npmrc`
   do repo) e roda `npm ci && npm run build` puro — só para garantir que o build funciona e que
   `${angularDistPath}/index.html` existe. Esse build É DESCARTADO; não é o que vai pro Docker.
2. **`DeployQA`** (branches `breaking/*`, `feature/*`, `fix/*`, fora de PR) e **`DeployPRD`**
   (só em `main`): baixa `frontends/<consulProject>/<qa|prd>/config.json` do Consul e grava em
   `public/config/config.json` dentro do workspace Angular resolvido — **antes** do build/Docker.
   Depois builda a imagem Docker (ver abaixo) e publica via Docker Compose + Traefik.

## O que o repositório PRECISA ter

- `package.json` com o script `"build"` (roda `ng build`) e `package-lock.json` versionado —
  `npm ci` exige o lock file, sem ele o build falha em qualquer estágio.
- `angular.json` válido, na raiz do workspace resolvido (`solutionPath`, normalmente `.`).
- `azure-pipelines.yml` estendendo `pipelines/angular-frontend-pipeline.yaml@Pipelines` (ver
  `templates/azure-pipelines.yml` deste skill e a tabela de parâmetros abaixo).
- `.npmrc` com o registry `@infinite` (só se o projeto realmente consumir pacotes privados —
  ver limitação sobre pacotes privados mais abaixo):
  ```
  @infinite:registry=https://devinfinite.pkgs.visualstudio.com/_packaging/InfiniteNuget/npm/registry/
  always-auth=true
  ```
- Se o app usa a config remota do Consul (praticamente todo frontend da Infinite usa): um
  serviço/plugin que leia `/config/config.json` em runtime — ver seção "Config remota" abaixo.

## O que o repositório NÃO deve ter

- **Não crie `Dockerfile.angular` (nem `Dockerfile`) no projeto.** O parâmetro
  `dockerfilePath` não é um caminho dentro do repositório do projeto — o `deploy-stage.yaml`
  resolve ele como `$(Pipeline.Workspace)/scripts/images/node-22/${{ dockerfilePath }}`, ou
  seja, dentro do checkout do **repo `DevOps/Pipelines`**. O Dockerfile é centralizado e mantido
  lá; um `Dockerfile.angular` local seria simplesmente ignorado (ou, pior, confundir quem lê o
  repo achando que é ele que roda). Isso é diferente do pipeline de API .NET
  (`infinite-dotnet-api`), onde o `Dockerfile` fica no próprio repo — não misture os dois padrões.
- Não commite `public/config/config.json` (é gerado pela pipeline a partir do Consul) — só
  `public/config/config.example.json` e, se o app tiver fallback de dev, `config.local.json`.
- Não commite token real de npm/Consul em nenhum arquivo.

## Parâmetros do `azure-pipelines.yml`

| Parâmetro | Obrigatório | O que é |
|---|---|---|
| `solutionPath` | não (default `.`) | Onde está o `angular.json`/`package.json`, relativo à raiz do repo. Use `.` a menos que o Angular esteja em subpasta. |
| `angularDistPath` | **sim** | Caminho de saída do build, relativo ao `solutionPath`. **Tem que bater exatamente** com o `outputPath` real do Angular (ver seção seguinte) — senão o build falha com `index.html não encontrado`. |
| `dockerfilePath` | não (default `Dockerfile.angular`) | Nome do arquivo dentro de `DevOps/Pipelines/images/node-22/`. Normalmente não precisa mudar. |
| `dockerImageName` / `dockerComposeProjectName` | sim | Nome da imagem/projeto compose. QA recebe sufixo `-qa` automaticamente — não adicione você mesmo. |
| `nodeVersion` | não (default `22.x` no exemplo) | Passado pro `UseNode@1`. Precisa satisfazer Angular 21: **20.19+, 22.12+ ou 24.0+**. `.nvmrc` do projeto é só para uso local; a pipeline usa este parâmetro. |
| `path` | sim | Subdomínio: PRD vai em `<path>.<baseDomain>`, QA em `qa-<path>.<baseDomain>`. |
| `consulProject` | **sim** | Nome do app no Consul, sem `/`. A pipeline monta a chave `frontends/<consulProject>/<qa\|prd>/config.json`. `ValidateAngular` falha de propósito se vier vazio. |
| `baseDomain` / `customDomain` / `useRootDomain` | não | Domínio alternativo. `useRootDomain: true` **exige** `customDomain` diferente de `baseDomain`, senão a pipeline recusa (mensagem explica o motivo). |
| `variableGroup` / `variableGroupPrd` | sim | Variable Group do Azure DevOps por ambiente — QA e PRD apontam para Consuls diferentes. |
| `dockerBuildArgs` | não | Build-args extras (ex.: `--build-arg NODE_ENV=production`). **Não** passe `ANGULAR_DIST_PATH` nem `APP_SOURCE_DIR` aqui — a própria pipeline já injeta os dois automaticamente a partir de `angularDistPath` e do `solutionPath` resolvido. |

## `angularDistPath` tem que bater com o `angular.json`

O builder `@angular/build:application` (Angular 17+) sempre gera a saída em
`<outputPath>/browser/`. Como o `outputPath` default do CLI é `dist/<nome-do-projeto>` (o nome
declarado em `angular.json` → `projects.<nome>`), o caminho real quase sempre é:

```
dist/<nome-do-projeto-no-angular.json>/browser
```

Antes de escrever `angularDistPath`, confira o nome real do projeto:

```bash
node -e "console.log(Object.keys(require('./angular.json').projects))"
```

e rode `npm run build` localmente para confirmar onde o `index.html` aparece. Um erro comum é
copiar `angularDistPath: 'dist/browser'` de outro projeto sem ajustar o nome — a pipeline builda
com sucesso o Angular mas falha ao testar `${angularDistPath}/index.html`, tanto no
`ValidateAngular` (fora do Docker) quanto dentro do `Dockerfile.angular`.

## Limitação: pacotes npm privados (`@infinite/*`)

Só o estágio `ValidateAngular` autentica o npm (task `npmAuthenticate@0`, roda no agente). O
build de produção acontece dentro do `Dockerfile.angular`, em `DeployQA`/`DeployPRD`, e **esse
estágio não injeta nenhuma credencial de npm** para dentro do container (só passa
`--secret id=azdo_token,...` para o Git, nada de token de npm). Ou seja: se o `package.json`
depender de um pacote com escopo `@infinite/...` do feed privado, `npm ci` dentro do
`Dockerfile.angular` vai falhar por falta de autenticação, mesmo que o `ValidateAngular` passe.
Hoje nenhum frontend Angular da Infinite usa pacote `@infinite/*` — se for necessário, isso
precisa de mudança no `Dockerfile.angular` centralizado (passar o token como build secret), não
dá pra resolver só no repo do projeto.

## Config remota (Consul) — padrão runtime, não build-time

A pipeline gera `public/config/config.json` (a partir de `frontends/<consulProject>/<qa|prd>`)
**antes** de rodar `ng build`. Como o Angular CLI copia o conteúdo de `public/` para a raiz do
`outputPath` (ver `assets` em `angular.json`), o arquivo termina em
`<angularDistPath>/config/config.json` e é servido como estático pelo Nginx da imagem.

Isso é diferente do skill `frontend-remote-config` (Vite/Vue): lá a config vira uma constante
**embutida no bundle em build-time** via plugin Vite. Um projeto Angular não tem esse mecanismo
de módulo virtual — o padrão validado aqui é **buscar o arquivo em runtime** com `fetch()`,
antes da aplicação renderizar, tipicamente via `provideAppInitializer`:

```ts
// core/config/app-config.service.ts
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly configState = signal<AppConfig | null>(null);
  readonly config = computed(() => this.snapshot);
  get snapshot(): AppConfig { /* lança se ainda não carregou */ }

  async load(): Promise<void> {
    // 1) tenta /config/config.json (produção, gerado pela pipeline)
    // 2) se falhar E isDevMode(), cai para /config/config.local.json (dev)
    // 3) senão, lança erro — falha rápido em vez de rodar com config incompleta
  }
}

// app.config.ts
providers: [
  provideAppInitializer(() => inject(AppConfigService).load()),
  // ...demais providers, incluindo provideHttpClient
]
```

Pré-requisitos:
- `public/config/config.example.json` (commitado, documenta as chaves) e
  `public/config/config.local.json` (commitado, valores de localhost — só usado em `isDevMode()`).
- A chave `frontends/<consulProject>/<qa|prd>/config.json` já precisa existir no Consul, senão o
  passo "Gerar config.json" falha com HTTP diferente de 200 — isso é operação, não código.
- `provideAppInitializer` tem que rodar **antes** de qualquer provider que dependa da config
  (ex.: interceptors HTTP que leem `apiBaseUrl`) — a ordem no array de `providers` importa.

## Checklist ao criar ou revisar o build de um Angular novo

1. `npm run build` local e confirme o caminho real de saída (`dist/<projeto>/browser`).
2. `angularDistPath` no `azure-pipelines.yml` bate exatamente com esse caminho.
3. `consulProject`, `path`, `variableGroup` e `variableGroupPrd` preenchidos — nenhum vazio.
4. `package-lock.json` commitado.
5. Nenhum `Dockerfile`/`Dockerfile.angular` no repo do projeto.
6. Se usa config remota: `AppConfigService` (ou equivalente) lendo `/config/config.json` com
   fallback de dev, chave já provisionada no Consul para `qa` e `prd`.
7. `nodeVersion` do pipeline compatível com a versão do Angular usada (21 → 20.19+/22.12+/24+).
8. Nenhuma dependência com escopo `@infinite/*` — se precisar, ver limitação acima antes de
   assumir que vai funcionar.

## Diagnosticando falha de pipeline

| Sintoma | Causa provável |
|---|---|
| `index.html não encontrado em: <path>` (no `ValidateAngular` ou no build da imagem) | `angularDistPath` não bate com o `outputPath` real — confira o nome do projeto em `angular.json`. |
| `Node X nao e compativel com Angular 21` | `nodeVersion` do `azure-pipelines.yml` abaixo do mínimo (20.19/22.12/24.0). |
| `Parametro 'consulProject' nao foi definido` / `'variableGroup(Prd)' nao foi definido` | Faltou parâmetro obrigatório no `extends.parameters`. |
| `ERRO: 'useRootDomain: true' exige...` | `useRootDomain: true` sem `customDomain`, ou `customDomain` igual ao `baseDomain`. |
| Falha silenciosa/estranha de `npm ci` dentro do build da imagem, mas `ValidateAngular` passou | Dependência `@infinite/*` sem autenticação disponível no `Dockerfile.angular` — ver limitação acima. |
| Passo "Gerar config.json" falha com HTTP != 200 | Chave `frontends/<consulProject>/<qa\|prd>/config.json` não existe no Consul — provisionar (operação, não código). |

## Referências

- `templates/azure-pipelines.yml` — exemplo completo comentado, copie e ajuste os placeholders.
- `references/pipeline-internals.md` — stages, scripts e variáveis internas da pipeline, para
  quando o checklist acima não for suficiente para explicar uma falha.
- Skill `infinite-frontend-datetime` — contrato de datas e fusos com as APIs (instantes UTC,
  datas civis, fuso IANA em formulários). Use-a ao criar/ajustar DTOs, formulários, filtros ou
  exibição de datas no app; este skill cobre só build/deploy.
- Skill `infinite-frontend-api` — contrato HTTP das APIs (paginação, erros, envio de DTOs) para o
  código do app.
