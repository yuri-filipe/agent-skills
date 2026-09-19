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

Referências de projeto: API → Domain + Infrastructure; Infrastructure → Domain; Domain → nenhum
projeto interno. **Nunca** Domain → API, nem Domain → Infrastructure.

As versões de pacote nos `.csproj` dos templates são um ponto de partida: confirme as versões
atuais no serviço de referência mais recente antes de commitar.

## 2. Referências obrigatórias por projeto

- **API**: `Infinite.Core.Consul`, `Infinite.Core.WebHost`, `Microsoft.EntityFrameworkCore.Design`
  (`PrivateAssets=all`), `Npgsql`, `Swashbuckle.AspNetCore`. `Refit.HttpClientFactory` só se houver
  integração externa.
- **Domain**: `Infinite.Core.Postgres`, `Microsoft.EntityFrameworkCore`,
  `Npgsql.EntityFrameworkCore.PostgreSQL`, `Riok.Mapperly`, `FluentValidation`. `Refit` só se houver
  cliente externo.
- **Infrastructure**: só `ProjectReference` para o Domain.

## 3. Configuração

- `Program.cs`: `builder.AddConsulConfig("/apis/{{servico}}")` — o caminho no Consul segue
  `/apis/<servico>` e é a **única** chave a mudar por serviço.
- `appsettings.json` guarda só defaults não sensíveis (Logging, AllowedHosts e seções de opções com
  valores neutros). **Connection string e segredos vêm do Consul.**
- Opções de integração: classe `sealed` em `{{Modulo}}/Services/` com
  `public const string SectionName = "<Secao>:<Sub>";`, registrada no `Startup` via
  `configuration.GetSection(X.SectionName).Get<X>()`.
- `launchSettings.json`: `CONSUL_DOMAIN` e `CONSUL_BACKEND_TOKEN` são variáveis locais do dev.
  Não commite token real — deixe o valor vazio no template do repositório novo.

## 4. Startup

`Startup.ConfigureServices` tem exatamente esta ordem, e nada mais no corpo:

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

## 5. Banco e migrations

- `InfiniteContextFactory` (design-time) usa a connection string `Postgres`, define
  `MigrationsAssembly` = assembly do Infrastructure e
  `MigrationsHistoryTable("__EFMigrationsHistory_{{Servico}}", "migrations")`.
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
