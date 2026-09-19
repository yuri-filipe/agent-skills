# Internals da pipeline Angular (para debug avançado)

Detalhe estágio a estágio do que `pipelines/angular-frontend-pipeline.yaml@Pipelines` executa.
Use isto quando o checklist e a tabela de sintomas do `SKILL.md` não bastarem para explicar uma
falha — por exemplo, para saber exatamente em que working directory um script roda, ou que
variável de ambiente ele espera.

## Stage `ValidateAngular`

Job `ValidateAngularJob`, roda direto no agente (sem Docker):

1. Checkout do repo do projeto em `$(Pipeline.Workspace)/s` e do repo `DevOps/Pipelines` em
   `$(Pipeline.Workspace)/scripts`.
2. `UseNode@1` instala o Node na versão de `nodeVersion`.
3. Valida a versão do Node contra o mínimo do Angular 21 (20.19+, 22.12+ ou 24.0+) — falha o
   build se não bater.
4. `init-containers.sh`, `show-vars.sh`, `determine-version.sh` (versão da release, usada depois
   como tag da imagem Docker).
5. `npmAuthenticate@0` autentica o `.npmrc` da raiz do checkout do projeto (`$(Pipeline.Workspace)/s/.npmrc`)
   contra o feed privado do Azure DevOps.
6. Resolve `AngularSourceDir` a partir de `solutionPath`: se `solutionPath` não tiver
   `package.json`+`angular.json`, tenta a raiz do repo; se nenhum dos dois tiver, falha.
7. `scripts/node/build-angular.sh --source-dir "$(AngularSourceDir)" --dist-path "<angularDistPath>"`:
   copia o `.npmrc` (se existir), roda `npm ci` e `npm run build`, e falha explicitamente se
   `<dist-path>/index.html` não existir — essa é a primeira validação de `angularDistPath`.
8. Esse build é descartado — serve só de sanity check. O build que efetivamente vira imagem
   acontece de novo dentro do Docker, no stage de deploy.

Falha de variável de ambiente (`consulProject`, `variableGroup`, `variableGroupPrd`,
`angularDistPath` vazios, ou `useRootDomain: true` sem `customDomain` válido) também é checada
neste stage, com mensagem explicando exatamente o que falta.

## Stages `DeployQA` / `DeployPRD`

Definidos pelo mesmo template (`pipelines/templates/angular-frontend/deploy-stage.yaml` no repositório DevOps/Pipelines), parametrizados
por `environment: qa|prd`. `DeployQA` roda em `feature/*`, `fix/*`, `breaking/*` fora de PR;
`DeployPRD` só em `main`. QA usa `variableGroup`; PRD usa `variableGroupPrd` — Consuls diferentes
por ambiente. Job `BuildAndDeployJob`:

1. Novo checkout do projeto (`fetchDepth: 0`, para ter histórico completo de tags/versão) e do
   `DevOps/Pipelines`.
2. Resolve `AngularSourceDir` de novo (mesma lógica do stage anterior — os dois stages rodam em
   agentes/checkouts independentes).
3. **`generate-runtime-config.sh`** roda com `workingDirectory` igual ao `AngularSourceDir`
   resolvido, e variáveis `CONSUL_DOMAIN`, `CONSUL_FRONTEND_TOKEN` (do Variable Group),
   `CONSUL_PROJECT=<consulProject>`, `CONSUL_ENVIRONMENT=<qa|prd>`. Baixa
   `https://<CONSUL_DOMAIN>/v1/kv/frontends/<consulProject>/<qa|prd>/config.json?raw` e grava em
   `public/config/config.json` (path fixo, salvo se `RUNTIME_CONFIG_OUTPUT` for setado — a
   pipeline não seta, então é sempre esse). HTTP diferente de 200 ou corpo vazio faz o step
   falhar. Isso acontece **antes** do build, direto no checkout — não dentro do container.
4. **`docker-build-image.sh`** builda com:
   - `--dockerfile "$(Pipeline.Workspace)/scripts/images/node-22/<dockerfilePath>"` — ou seja,
     o Dockerfile vem do repo `DevOps/Pipelines`, não do projeto.
   - `--context "$(Pipeline.Workspace)/s"` — o contexto é o checkout inteiro do projeto
     (já com o `public/config/config.json` gerado no passo anterior).
   - `--build-arg "APP_SOURCE_DIR=$(AngularSourceDir)"` e
     `--build-arg "ANGULAR_DIST_PATH=<angularDistPath>"` — injetados automaticamente, **antes**
     de `dockerBuildArgs`, então não precisa (e não deve) repetir esses dois no
     `azure-pipelines.yml` do projeto.
   - `--secret id=azdo_token,env=SYSTEM_ACCESSTOKEN` — só isso; nenhuma credencial de npm chega
     ao container (ver limitação de pacotes `@infinite/*` privados no `SKILL.md`).
5. Dentro do `Dockerfile.angular` (`DevOps/Pipelines/images/node-22/Dockerfile.angular`):
   - Estágio `builder` (`node:24-alpine`): copia o repo inteiro para `/app`, roda
     `npm ci && npm run build`, e falha se `${ANGULAR_DIST_PATH}/index.html` não existir —
     segunda validação do mesmo caminho, agora dentro do container.
   - Estágio final (`nginx:alpine`): copia só `${ANGULAR_DIST_PATH}/` para
     `/usr/share/nginx/html`, gera um `default.conf` com SPA fallback
     (`try_files $uri $uri/ /index.html`), `no-store` no `index.html` e cache longo para
     estáticos com hash. Expõe a porta 80.
6. `generate-compose-env.sh`, `resolve-traefik-host.sh` e `prepare-compose-files.sh` montam o
   `docker-compose.yml` final a partir do template em
   `DevOps/Pipelines/images/node-22/docker-compose.yml`, resolvendo o host do Traefik
   (`<path>.<baseDomain>` em PRD, `qa-<path>.<baseDomain>` em QA, ou a variação com
   `customDomain`/`useRootDomain`).
7. `docker-compose-deploy.sh` sobe o serviço. A imagem final é `<dockerImageName>[-qa]:<tag>`,
   onde `<tag>` é a versão calculada em `determine-version.sh` no stage `ValidateAngular`.

## Coisas que só existem na infraestrutura (fora do código)

- Service connection do Azure DevOps para o `npmAuthenticate@0` autenticar contra o feed
  `InfiniteNuget` (necessário mesmo que o projeto não use pacote privado, porque o passo roda
  sempre).
- Chave `frontends/<consulProject>/qa/config.json` e `.../prd/config.json` no Consul, com JSON
  compatível com o `AppConfig` do frontend.
- DNS do `customDomain` (quando usado) apontando para a VPS antes do primeiro deploy.
