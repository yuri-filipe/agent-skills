# Superfície das libs internas `Infinite.Core.*`

Use apenas o que está aqui. Se precisar de algo que não existe, procure primeiro no código do
serviço; não crie abstração paralela (repositório próprio, `ApiResponse` próprio, base controller
próprio) — isso quebra o padrão compartilhado.

Levantado do código das libs (`Infinite.Core` em `breaking/force-deploy`, `Infinite.Core.Postgres`
em `breaking/timezone`, `Infinite.Core.WebHost` em `breaking/ajustes`, `Infinite.Core.Consul` em
`feature/new-consul`). Confira no feed a versão efetivamente referenciada pelo serviço.

## Pacotes

| Pacote | Onde referenciar | Fornece |
|---|---|---|
| `Infinite.Core` | transitivo via Postgres/WebHost | `Response<T>`, `ResponsePaged<T>`, `PagedQueryBase<T>`, `AddInfiniteCqrs`, `IAuditContextAccessor` |
| `Infinite.Core.Postgres` | `.Domain` | `CoreEntity`, `CoreTableMapping<T>`, `CoreViewMapping<T>`, repositórios, `InfiniteContext`, `AddInfiniteContext`, `InfiniteContextFactoryBase`, `PredicateBuilder`; traz EF Core 10, Npgsql + plugin NodaTime |
| `Infinite.Core.WebHost` | API | `InfiniteApiController`, `AddInfiniteApiController`, `UseInfiniteApi`, auth Zitadel, observabilidade, JSON NodaTime, Swagger |
| `Infinite.Core.Consul` | API | `AddConsulConfig`, `GetConsulConfiguration<T>`, `IConsulConfigurationProvider` |
| `NodaTime` | Domain quando expõe tipos temporais | `Instant`, `LocalDate`, `LocalTime`, `LocalDateTime`, `DateTimeZoneProviders` |

`NodaTime.Serialization.SystemTextJson` e `Swashbuckle.AspNetCore` chegam **transitivos** pela
WebHost; não referencie de novo na API. Versões na lib: EF Core 10.0.12, Npgsql 10.0.3,
NodaTime 3.3.4, MediatR 13.1.0, FluentValidation 12.1.0.

Feed privado: `InfiniteNuget` (`src/nuget.config`). Versões são pinadas no `.csproj` — copie as do
serviço de referência mais atual em vez de chutar.

## `Infinite.Core.Response`

`Response<T>` — retorno de todo handler não paginado. Todos os métodos aceitam `title` opcional:

```csharp
Response<T> response = new();

response.Success(valor);                    // 200
response.Success(valor, "mensagem");        // 200 com mensagem
response.NotFound("mensagem");              // 404 — recurso inexistente
response.Invalid("mensagem");               // 400 — estado/regra inválida
response.Forbidden("mensagem");             // 403 — usuário autenticado sem direito sobre o recurso
response.Error("mensagem");                 // 500 — falha inesperada tratada
```

`ResponsePaged<T>` — retorno de listagem: `Items`, `TotalItems`, `Page`, `PageSize`. Não
instancie manualmente: use `IReadRepository<T>.PaginatedAsync`.

Regras:
- Mensagens sempre em `private const string` no topo do handler, nomeadas `...Message`.
- `Invalid` para violação de regra/estado; `NotFound` só quando o registro não existe;
  `Error` apenas em falha realmente inesperada (integração externa, por exemplo).
- `Validation(...)` existe, mas erros de campo são do FluentValidation — não monte à mão.

### Contrato HTTP produzido pelo `Send`

| Situação | Status | Corpo |
|---|---|---|
| `Success` | 200 | o `Value` puro (sem envelope) |
| `ResponsePaged` com sucesso | 200 | **só o array de itens**; paginação nos headers `X-Total-Count`, `X-Page`, `X-Page-Size`, `X-Total-Pages` |
| `NotFound` / `Invalid` / `Forbidden` / `Error` | 404 / 400 / 403 / 500 | `{ title, message, effect }` |
| Validator FluentValidation falhou | 422 | `{ title, message, effect, errors: [{ field, message }] }` |
| Exceção no handler | 500 | `{ title, message, effect }` via `ErrorHandlingBehavior` (ver abaixo) |
| Cliente cancelou | 499 | vazio |

`effect` é o enum numérico `Effect` (`Success=1 … Validation=7`). O CORS da lib não expõe os
headers de paginação: em chamada cross-origin o navegador não os entrega ao JS. Confirme que
frontend e API estão na mesma origem, ou alinhe a exposição na lib, antes de depender deles.

## `Infinite.Core.Queries`

```csharp
public abstract record PagedQueryBase<TResponse> : IRequest<ResponsePaged<TResponse>>
// Page (1), PageSize (20, teto 200), Search (""), SortField (""), SortOrder ("asc")
// Skip, Take e HasSearch são calculados.
```

- Toda query de listagem herda dela. Não redeclare `Page`/`PageSize`/`Search`/`SortField`/`SortOrder`.
- A lib **não aplica `Search`**: se o endpoint suporta busca livre, trate `request.HasSearch` no
  `Filters`.
- `SortField` é o nome de uma propriedade **da entidade** (sem diferenciar maiúsculas); nome
  inexistente ou vazio ordena por `Id`. `SortOrder` `desc` inverte; qualquer outro valor é asc.
- `QueryBase<T>` (`IRequest<Response<T>>`) e `CommandBase<T>` existem; siga o que o serviço já usa.

Quando vários módulos compartilham os mesmos filtros, crie
`{{Modulo}}/Common/Queries/Pesquisar{{Modulo}}BaseQuery.cs` como `abstract record` herdando
`PagedQueryBase<TResponse>`.

## `Infinite.Core.Postgres.Interfaces`

```csharp
IReadRepository<T>
    IQueryable<T> Query                                   // AsNoTracking já aplicado
    Task<T?> FindAsync(object[] keys, CancellationToken)  // DbSet.FindAsync — entidade RASTREADA
    Task<bool> ExistsAsync(predicate, CancellationToken)
    Task<T?> FirstAsync(predicate, CancellationToken)
    Task<List<T>> ListAsync(CancellationToken) / ListAsync(predicate, CancellationToken)
    Task<T?> SingleAsync(predicate, CancellationToken)    // lança se houver mais de um
    Task<ResponsePaged<TResponse>> PaginatedAsync<TResponse>(
        PagedQueryBase<TResponse> request, Func<T, TResponse> mapper,
        Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)

IWriteRepository<T> where T : CoreEntity                  // todos chamam SaveChanges
    AddAsync(entity, ct)            AddRangeAsync(entities, batchSize = 500, ct)
    UpdateAsync(entity, ct)         UpdateRangeAsync(entities, batchSize = 500, ct)
    DeleteAsync(entity, ct)         DeleteRangeAsync(entities, batchSize = 500, ct)   // soft delete
    HardDeleteAsync(entity, ct)     HardDeleteRangeAsync(entities, ct)                // físico
    RestoreAsync(entity, ct)                                                          // Excluido = false
    ExecuteInTransactionAsync(Func<Task> action, ct)                                  // commit/rollback
```

- `PaginatedAsync` sempre com **argumentos nomeados** (`request:`, `mapper:`, `predicate:`,
  `cancellationToken:`) — é o estilo do padrão e evita troca acidental de parâmetro.
- `Query` para leituras compostas (`FirstOrDefaultAsync`, `Include`, projeções).
- `UpdateAsync` protege `Excluido`; excluir é `DeleteAsync`. `HardDelete*` só com pedido explícito.
- Consultas já filtram `Excluido = false` (query filter global).
- Não injete `InfiniteContext` em handler. Para várias gravações atômicas, use
  `ExecuteInTransactionAsync`; se a orquestração for reutilizada, isole num service em
  `{{Modulo}}/Services/`.

## `Infinite.Core.Postgres.Models` / `.Mapping`

```csharp
public sealed class {{Entidade}} : CoreEntity
// CoreEntity: Id (long), Excluido (bool),
//   UsuarioInclusao / UsuarioAlteracao (string?),
//   DataInclusao / DataAlteracao (Instant?),
//   OrganizacaoInclusao / OrganizacaoAlteracao (string?)
```

- Entidade **não** declara `Id` nem campos de auditoria.
- Auditoria automática no `SaveChanges`: um instante por salvamento; usuário = `sub` e
  organização = `urn:zitadel:iam:org:id` do token, lidos via `IAuditContextAccessor` (registrado
  pela WebHost). Sem usuário grava `"-"`; sem organização, `null`. Na alteração, os campos de
  inclusão são preservados. Não preencha esses campos no handler.
- Organização na auditoria registra **quem fez**; não é regra de propriedade nem filtro de tenant.
- Instantes usam `Instant`; valores civis usam `LocalDate`, `LocalTime` ou `LocalDateTime`.
  Ver [datas-e-fusos.md](datas-e-fusos.md).
- FK escalar `public long Id{{Relacionada}} { get; set; }` + navegação
  `public {{Relacionada}} {{Relacionada}} { get; set; } = null!;`
- Coleção: `public List<{{Filho}}> {{Filhos}} { get; set; } = [];`
- FK opcional entre módulos: `long?` e `DeleteBehavior.Restrict` no mapping.

```csharp
public class {{Entidade}}Mapping : CoreTableMapping<{{Entidade}}>
{
    public {{Entidade}}Mapping() : base("{{tabela}}", "{{schema}}") { }

    protected override void ConfigureAdditionalProperties(EntityTypeBuilder<{{Entidade}}> builder)
    { /* só o que foge do default: MaxLength, IsRequired, precision, relacionamentos */ }
}
```

Tabela em snake_case singular, schema em snake_case = módulo. `CoreTableMapping` ordena as
colunas pela declaração (base primeiro) e põe as de auditoria no fim. Convenções globais:
`string` → `varchar(255)`, `decimal` → `numeric(12,2)`, `Instant`/`DateTime`/`DateTimeOffset` →
`timestamptz`, `LocalDateTime` → `timestamp`, `LocalDate`/`DateOnly` → `date`,
`LocalTime`/`TimeOnly` → `time`. Configure no mapping só o que difere.

Para view read-only, use `CoreViewMapping<T>` (`T : ICoreEntity`): sobrescreva `ViewName` e,
se não for `infinite_api`, `Schema`.

## `Infinite.Core.Postgres.Extensions` / configuração

- `services.AddInfiniteContext(configuration, typeof(<MappingQualquer>).Assembly)` — registra
  `InfiniteContext` com pooling + repositórios; o assembly marca onde estão os `*Mapping`.
- Seção `PostgresConfig` (validada no startup): `Host`, `Database`, `Username`, `Password`
  obrigatórios; `EnableRetryOnFailure` (3), `QueryTrackingBehavior` (`TrackAll`),
  `MigrationsHistoryTable` (`__EFMigrationsHistory`), `MigrationsSchema` (`migrations`).
- Runtime e `InfiniteContextFactoryBase` passam pelo mesmo `PostgresOptionsConfigurator` com
  `UseNodaTime()`. Não crie conversores EF de `Instant` para `DateTime` nem outro `UseNpgsql`.
- `PredicateBuilder.True<T>()` + `.And(...)`/`.Or(...)` — base dos `Filters`.
- `AutoProject<TEntity, TDto>()` / `GetCompiledProjector` copiam propriedades de mesmo nome e
  tipo compatível; não convertem tipos temporais.

## `Infinite.Core.Extensions.Cqrs`

`services.AddInfiniteCqrs(configuration, typeof(<HandlerQualquer>).Assembly)` — registra MediatR
(licença em `LicenseKeyMediatr` no Consul), handlers, validators FluentValidation e os behaviors:

1. `ValidationBehavior` — roda os validators e lança `ValidationException`; o `Send` responde 422.
2. `ErrorHandlingBehavior` — captura exceção do handler, registra `LogError` para ela e cada
   `InnerException`, e devolve `Response.Error` (500). Mensagem padrão
   `"Falha ao processar a requisição."`; a request ou a exceção pode implementar
   `IHasFriendlyError` para trocar a mensagem. Cancelamento do cliente não é capturado.

Por isso **handler não chama validator** nem precisa de `try/catch` só para devolver 500.

## `Infinite.Core.WebHost`

```csharp
services.AddInfiniteApiController(configuration, NameApi);   // controllers, CORS, Swagger, health, auth, telemetria, JSON
app.UseInfiniteApi("<título da API>");                       // pipeline HTTP completo
```

Namespaces: `Infinite.Core.WebHost.Extensoes.Controllers` (extensões) e
`Infinite.Core.WebHost.Controllers` (`InfiniteApiController`). Os antigos `...Extensions.*` não
existem mais.

`AddInfiniteApiController` exige `Cors:AllowedOrigins` não vazio e lê `Autenticacao` e
`Observabilidade` (ver skills `infinite-auth-zitadel` e `infinite-api-logs`). Swagger fica na raiz,
publicado conforme `Autenticacao:ExporSwagger`; `/health` é anônimo.

JSON (controllers **e** Minimal APIs) já sai configurado por `ConfigurarJsonInfinite`:

- NodaTime com `DateTimeZoneProviders.Tzdb` (`Instant` → `"2026-01-31T13:45:00Z"`);
- propriedade desconhecida no corpo é **erro** (`UnmappedMemberHandling.Disallow`);
- chave duplicada é erro; parâmetro obrigatório de construtor ausente é erro;
- ciclos de navegação viram `null` (`IgnoreCycles`);
- `DateTime`/`DateTimeOffset` só aceitam string com `Z` ou offset; `DateTime` lido vira UTC.

Swagger já mapeia os tipos NodaTime como string com exemplo. Não registre `ConfigureForNodaTime`
de novo. Ajuste adicional vai no terceiro parâmetro:
`AddInfiniteApiController(configuration, NameApi, json => { ... })`.

`InfiniteApiController` expõe `protected Task<IActionResult> Send(IRequest<TResponse> request)`.
Não escreva `Ok()`, `NotFound()` ou `StatusCode(...)` em controller.

## `Infinite.Core.Consul`

```csharp
builder.AddConsulConfig("/apis/{{servico}}");                       // Program.cs
builder.AddConsulConfig("/apis/{{servico}}", typeof(MinhaOpcao));   // + registra opções tipadas
```

- Lê `{configPath}/{prd|qa}/config.json`: `prd` quando `ASPNETCORE_ENVIRONMENT=Production`,
  `qa` em qualquer outro valor (inclusive `Development`).
- **Remove as demais fontes de configuração** (inclusive `appsettings*.json` e user secrets),
  mantendo só variáveis de ambiente e linha de comando. Tudo que a API lê em runtime — inclusive
  `Logging` — precisa estar no Consul.
- Exige `CONSUL_DOMAIN` e `CONSUL_BACKEND_TOKEN` no ambiente — **nunca commitadas**.
- Valores podem interpolar outras chaves: `"{{ chave }}"` ou o formato antigo `{{ key 'chave' }}`.
  Um placeholder no lugar de um membro de objeto que resolve para um objeto JSON é mesclado no pai.
- Opções tipadas: a seção tem o **nome da classe**. `AddConsulConfig(path, typeof(X))` registra
  `X` como singleton; também há `configuration.GetConsulConfiguration<X>()` e
  `IConsulConfigurationProvider.Get<X>()`. Seção ausente falha no startup.
