# Superfície das libs internas `Infinite.Core.*`

Use apenas o que está aqui. Se precisar de algo que não existe, procure primeiro no código do
serviço; não crie abstração paralela (repositório próprio, `ApiResponse` próprio, base controller
próprio) — isso quebra o padrão compartilhado.

## Pacotes

| Pacote | Onde referenciar | Fornece |
|---|---|---|
| `Infinite.Core` | vem transitivo via Postgres/WebHost | `Response<T>`, `ResponsePaged<T>`, `PagedQueryBase<T>`, `AddInfiniteCqrs` |
| `Infinite.Core.Postgres` | `.Domain` | `CoreEntity`, `CoreTableMapping<T>`, `CoreViewMapping<T>`, `IReadRepository<T>`, `IWriteRepository<T>`, `InfiniteContext`, `AddInfiniteContext`, `PredicateBuilder` |
| `Infinite.Core.WebHost` | API | `InfiniteApiController`, `AddInfiniteApiController`, `UseInfiniteApi` |
| `Infinite.Core.Consul` | API | `AddConsulConfig` |

Feed privado: `InfiniteNuget` (`src/nuget.config`). Versões são pinadas no `.csproj` — copie as do
serviço de referência mais atual em vez de chutar.

## `Infinite.Core.Response`

`Response<T>` — retorno de todo handler não paginado:

```csharp
Response<T> response = new();

response.Success(valor);                    // 200
response.Success(valor, "mensagem");        // 200 com mensagem
response.NotFound("mensagem");              // 404 — recurso inexistente
response.Invalid("mensagem");               // 400 — estado/regra inválida
response.Error("mensagem");                 // 500 — falha inesperada tratada
```

`ResponsePaged<T>` — retorno de handler de listagem paginada. Campos: `Items`, `TotalItems`,
`Page`, `PageSize`. Não instancie manualmente: use `IReadRepository<T>.PaginatedAsync`.

Regras:
- Mensagens sempre em `private const string` no topo do handler, nomeadas `...Message`.
- `Invalid` para violação de regra/estado; `NotFound` só quando o registro não existe;
  `Error` apenas em falha realmente inesperada (integração externa, por exemplo).

## `Infinite.Core.Queries`

```csharp
public abstract record PagedQueryBase<TResponse>;   // Page, PageSize, SortField, SortOrder
```

Toda query de listagem herda dela e é `IRequest<ResponsePaged<TResponse>>` implicitamente.
Não redeclare `Page`/`PageSize`/`SortField`/`SortOrder` na query filha.

Quando vários módulos compartilham os mesmos filtros, crie
`{{Modulo}}/Common/Queries/Pesquisar{{Modulo}}BaseQuery.cs` como `abstract record` herdando
`PagedQueryBase<TResponse>`.

## `Infinite.Core.Postgres.Interfaces`

```csharp
IReadRepository<TEntity>
    IQueryable<TEntity> Query                       // composição com EF (AsNoTracking já aplicado)
    Task<TEntity?> FindAsync(object?[] keys, CancellationToken ct)
    Task<ResponsePaged<TDto>> PaginatedAsync(
        request: PagedQueryBase<TDto>,
        mapper: Func<TEntity, TDto>,
        predicate: Expression<Func<TEntity, bool>>,
        cancellationToken: CancellationToken)

IWriteRepository<TEntity>
    Task AddAsync(TEntity entity, CancellationToken ct)
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct)
    Task UpdateAsync(TEntity entity, CancellationToken ct)
    Task DeleteAsync(TEntity entity, CancellationToken ct)
```

- `PaginatedAsync` sempre com **argumentos nomeados** (`request:`, `mapper:`, `predicate:`,
  `cancellationToken:`) — é o estilo do padrão e evita troca acidental de parâmetro.
- `Query` para leituras compostas (`FirstOrDefaultAsync`, `Include`, projeções).
- `FindAsync` para busca por chave: `await _readRepository.FindAsync([request.Id], cancellationToken)`.
- Não injete `InfiniteContext` em handler. Se precisar de transação entre agregados, isole num
  service em `{{Modulo}}/Services/`.

## `Infinite.Core.Postgres.Models` / `.Mapping`

```csharp
public sealed class {{Entidade}} : CoreEntity   // CoreEntity: Id (long), Ativo, DataCriacao, ...
```

- Entidade **não** declara `Id` — vem de `CoreEntity`.
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

Tabela em snake_case singular, schema em snake_case = módulo. Colunas base e convenções de nome
já vêm de `CoreTableMapping`. Para view read-only, use `CoreViewMapping<T>`.

## `Infinite.Core.Postgres.Extensions`

- `services.AddInfiniteContext(configuration, typeof(<MappingQualquer>).Assembly)` — registra
  `InfiniteContext` + repositórios; o assembly marca onde estão os `*Mapping`.
- `PredicateBuilder.True<T>()` + `.And(...)` — base dos `Filters`.

## `Infinite.Core.Extensions.Cqrs`

`services.AddInfiniteCqrs(configuration, typeof(<HandlerQualquer>).Assembly)` — registra MediatR,
todos os handlers, os validators FluentValidation do assembly e os pipeline behaviors
(validação → resposta `Invalid`, tratamento de erro). Por isso **handler não chama validator**.

## `Infinite.Core.WebHost`

```csharp
services.AddInfiniteApiController(configuration, NameApi);   // controllers, CORS, Swagger, health
app.UseInfiniteApi("<título da API>");                       // pipeline HTTP completo
```

`InfiniteApiController` expõe `protected Task<IActionResult> Send(IRequest<...> request)`, que
traduz `Response<T>`/`ResponsePaged<T>` no status HTTP correto. Não escreva `Ok()`, `NotFound()`
ou `StatusCode(...)` em controller.

## `Infinite.Core.Consul`

`builder.AddConsulConfig("/apis/{{servico}}")` no `Program.cs`: carrega a configuração remota do
Consul como fonte de `IConfiguration` (connection strings, chaves de integração). Credenciais vêm
de variáveis de ambiente (`CONSUL_DOMAIN`, `CONSUL_BACKEND_TOKEN`) — **nunca commitadas**.
