# Bootstrap de um novo serviço

Ordem de execução. Substitua `{{Servico}}` (ex.: `Cadastro`, `Agendamentos`) e `{{servico}}`
(kebab/lower do mesmo nome, ex.: `agendamentos`).

## 1. Esqueleto

```text
src/
  nuget.config
  Infinite.{{Servico}}.slnx
  Infinite.{{Servico}}/
  Infinite.{{Servico}}.Domain/
  Infinite.{{Servico}}.Infrastructure/
```

Copie de `templates/servico/`:

| Template | Destino |
|---|---|
| `nuget.config` | `src/nuget.config` |
| `Infinite.Servico.slnx` | `src/Infinite.{{Servico}}.slnx` |
| `Api.csproj` | `src/Infinite.{{Servico}}/Infinite.{{Servico}}.csproj` |
| `Domain.csproj` | `src/Infinite.{{Servico}}.Domain/Infinite.{{Servico}}.Domain.csproj` |
| `Infrastructure.csproj` | `src/Infinite.{{Servico}}.Infrastructure/Infinite.{{Servico}}.Infrastructure.csproj` |
| `Program.cs` | `src/Infinite.{{Servico}}/Program.cs` |
| `Startup.cs` | `src/Infinite.{{Servico}}/Startup.cs` |
| `appsettings.json` | `src/Infinite.{{Servico}}/appsettings.json` |
| `appsettings.Development.json` | idem |
| `launchSettings.json` | `src/Infinite.{{Servico}}/Properties/launchSettings.json` |
| `InfiniteContextFactory.cs` | `src/Infinite.{{Servico}}/Postgres/InfiniteContextFactory.cs` |
| `azure-pipelines.yml` | raiz do repo |
| `editorconfig` | `src/.editorconfig` (torna obrigatório o padrão de logs; ver skill `infinite-api-logs`) |
| `consul-config.json` | base de `/apis/{{servico}}/{qa|prd}/config.json` no Consul (não vai para o repo) |

Referências de projeto: API → Domain + Infrastructure; Infrastructure → Domain; Domain → nenhum
projeto interno. **Nunca** Domain → API, nem Domain → Infrastructure.

Resolva os placeholders `{{versao}}` antes de usar os templates, copiando as versões do serviço de
referência mais atual e confirmando no feed a versão das libs `Infinite.Core.*`.

**EF Core não é referenciado nos projetos do serviço.** `Microsoft.EntityFrameworkCore`,
`.Relational`, `.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL` (+ plugin NodaTime) e `NodaTime`
chegam transitivos pela `Infinite.Core.Postgres` (hoje EF Core 10.0.12, Npgsql 10.0.3, NodaTime
3.3.4). O `Design` vem sem `PrivateAssets` justamente para o `dotnet ef` funcionar no projeto da
API. Para mudar a versão do EF, atualize a lib — não adicione `PackageReference` no serviço.
Isso exige a versão da `Infinite.Core.Postgres` que já traz o `Design` transitivo; com uma versão
anterior, o `dotnet ef` acusa a falta de `Microsoft.EntityFrameworkCore.Design`.

## 2. Referências obrigatórias por projeto

- **API**: `Infinite.Core.Consul`, `Infinite.Core.WebHost`. Swagger e o serializador NodaTime
  chegam pela WebHost; EF Core, Design e Npgsql chegam pelo Domain. `Refit.HttpClientFactory` só se
  houver integração externa.
- **Domain**: `Infinite.Core.Postgres`, `Riok.Mapperly`, `FluentValidation`. `Refit` só se houver
  cliente externo.
- **Infrastructure**: só `ProjectReference` para o Domain.

## 3. Configuração

- `Program.cs`: `builder.AddConsulConfig("/apis/{{servico}}")` — o caminho no Consul segue
  `/apis/<servico>` e é a **única** chave a mudar por serviço. A chave lida é
  `/apis/{{servico}}/qa/config.json`, ou `.../prd/...` quando `ASPNETCORE_ENVIRONMENT=Production`.
- `AddConsulConfig` **descarta `appsettings*.json`** em runtime (só variáveis de ambiente e linha de
  comando sobrevivem). Toda configuração, inclusive `Logging`, vive no Consul. Os `appsettings`
  do template ficam apenas como default local de ferramentas; não coloque neles nada de que a API
  dependa.
- Seções que a API precisa no Consul para subir (base em `templates/servico/consul-config.json`):
  `Cors:AllowedOrigins` (obrigatório, não vazio), `PostgresConfig` (`Host`, `Database`,
  `Username`, `Password`), `Autenticacao` e `Observabilidade` (ver skills
  `infinite-auth-zitadel` e `infinite-api-logs`) e `LicenseKeyMediatr`.
- Opções de integração: classe `sealed` em `{{Modulo}}/Services/`. Siga o padrão já usado no
  serviço: `public const string SectionName = "<Secao>:<Sub>";` lido com
  `configuration.GetSection(X.SectionName).Get<X>()`, ou a forma tipada da lib Consul —
  `builder.AddConsulConfig("/apis/{{servico}}", typeof(X))`, com a seção nomeada como a classe e
  `X` injetável como singleton.
- `launchSettings.json`: `CONSUL_DOMAIN` e `CONSUL_BACKEND_TOKEN` são variáveis locais do dev.
  Não commite token real — deixe o valor vazio no template do repositório novo.

## 4. Startup

O template de `Startup.ConfigureServices` segue esta ordem:

```csharp
services.AddInfiniteApiController(configuration, NameApi);
services.AddInfiniteContext(configuration, typeof(<QualquerMapping>).Assembly);
services.AddInfiniteCqrs(configuration, typeof(<QualquerQueryHandler>).Assembly);
AddMappers(services);
Add{{Modulo}}Services(services);           // um método estático por módulo que tenha services
```

- Os dois `typeof(...)` são **marcadores de assembly**: qualquer tipo do assembly serve, mas use um
  `*Mapping` para o Infrastructure e um `*QueryHandler` para o Domain, por consistência.
- `AddMappers` registra todo mapper Mapperly como **Singleton** (são stateless).
- Services de domínio são **Scoped**; classes de opções, **Singleton**.
- Cada grupo é um `public static IServiceCollection Add...(IServiceCollection services)` que
  retorna `services`.
- `AddInfiniteApiController` já configura o JSON (NodaTime e regras estritas) para controllers e
  Minimal APIs. Não registre `ConfigureForNodaTime` de novo; ajuste extra vai no parâmetro
  `configurarJson`. Ver [libs-infinite.md](libs-infinite.md) e [datas-e-fusos.md](datas-e-fusos.md).

## 5. Banco e migrations

- `InfiniteContextFactory` herda `InfiniteContextFactoryBase` e informa o assembly do Infrastructure.
  Isso mantém as convenções e `UseNodaTime()` iguais aos do runtime.
- A base lê `PostgresConfig` de appsettings e variáveis de ambiente; como a seção vive no Consul,
  o template sobrescreve `ConfigureConfiguration` com `AddEnvironmentVariables().AddConsulConfig(...)`.
  Rode `dotnet ef` com `CONSUL_DOMAIN` e `CONSUL_BACKEND_TOKEN` definidos; sem
  `ASPNETCORE_ENVIRONMENT=Production` ele usa a config de **qa**. Não recrie um `UseNpgsql`
  independente com opções diferentes.
- Ao atualizar um serviço existente para a `Infinite.Core.Postgres` com auditoria de organização,
  gere a migration das colunas `organizacao_inclusao`/`organizacao_alteracao` (nullable, sem
  default) **antes** de subir a versão nova — o EF passa a ler essas colunas em toda tabela de
  `CoreEntity`.
- Configure `MigrationsHistoryTable`/`MigrationsSchema` na seção compartilhada `PostgresConfig`;
  qualquer nome específico do serviço deve ser o mesmo no runtime e no design-time.
- Um schema Postgres por módulo, em snake_case.

```bash
dotnet ef migrations add <NomeDaMigration> --project src/Infinite.{{Servico}}.Infrastructure --startup-project src/Infinite.{{Servico}}
```

```bash
dotnet ef database update --project src/Infinite.{{Servico}}.Infrastructure --startup-project src/Infinite.{{Servico}}
```

Migration nasce de mudança de entidade + mapping, nunca escrita à mão do zero.

## 6. Pipeline e container

`azure-pipelines.yml` estende `pipelines/api-pipeline.yaml@Pipelines` (repo `DevOps/Pipelines`).
Parâmetros que mudam por serviço: `dockerImageName`, `dockerComposeProjectName`, `apiPath`,
`PROJECT_FILE`, `APP_DLL`. `solutionPath: 'src/'` e `dockerContext: '.'` são fixos. O `Dockerfile`
na raiz recebe `DOTNET_VERSION`, `BUILD_CONFIGURATION`, `PROJECT_FILE`, `APP_DLL` como build args.

## 7. Validação final

```bash
dotnet build src/Infinite.{{Servico}}.slnx
```
