# Checklist de revisão

Estrutura (todo PR):

- [ ] Assembly/namespace começam com `Infinite.`
- [ ] Arquivo na pasta certa segundo a tabela de nomenclatura; um tipo por arquivo, nome igual
- [ ] Nenhuma pasta nova fora do layout (`Interfaces/`, `Helpers/`, `Repositories/`… não existem)
- [ ] Namespace espelha exatamente o caminho de pastas
- [ ] Verbos em português (Adicionar/Atualizar/Deletar/Pesquisar/Obter…PorId)

Controller:

- [ ] Herda `InfiniteApiController` com primary constructor `(IMediator, ILogger<T>)`
- [ ] `[Route("v1/{{rota}}")]` em kebab-case plural + `[ApiController]`
- [ ] Só `Send(...)`: sem repositório, sem service, sem `Ok()`/`NotFound()`/`StatusCode()`
- [ ] `Put` atribui `command.Id = id;` antes do `Send`
- [ ] Nenhuma regra de negócio, nenhum mapeamento

Command / Query:

- [ ] Command `sealed class` com `get; set;`; delete é `sealed record ...(long Id)`
- [ ] Query `sealed record` com `init`
- [ ] Query paginada herda `PagedQueryBase<Pesquisar{{Entidade}}Dto>` e não redeclara paginação
- [ ] Query por id herda `ObterPorIdBaseQuery<{{Entidade}}Dto>`
- [ ] Campos comuns entre Adicionar/Atualizar extraídos em `I{{Entidade}}Writable`

Handler:

- [ ] `public sealed class` com primary constructor + campos `private readonly`
- [ ] Mensagens em `private const string ...Message`
- [ ] `IReadRepository` em query; `IWriteRepository` em mutação
- [ ] `CancellationToken` propagado em toda chamada async
- [ ] Retorna `Success/NotFound/Invalid/Forbidden/Error` — sem `throw` para fluxo esperado
- [ ] Sem `try/catch` só para devolver 500 (o `ErrorHandlingBehavior` já faz)
- [ ] Sem validação de entrada manual (é do pipeline) e sem mapeamento manual (é do Mapperly)
- [ ] Listagem usa `PaginatedAsync` com argumentos nomeados e predicate do `Filters`
- [ ] Se a listagem aceita busca livre, o filtro trata `request.HasSearch` (a lib não aplica `Search`)

Dto / Mapper / Validator / Filter:

- [ ] DTO de listagem separado do DTO de detalhe
- [ ] Mapper `[Mapper] partial class` com métodos `partial`, sem corpo escrito à mão
- [ ] Mapper registrado como Singleton em `Startup.AddMappers`
- [ ] Validator base genérico sobre `I{{Entidade}}Writable`, incluído via `Include(...)`
- [ ] Validators de Id usam `.GreaterThan(0)`
- [ ] Filtro usa `PredicateBuilder.True<T>()` + `.And(...)`, checando null/empty antes de cada filtro

Persistência:

- [ ] Entidade herda `CoreEntity` e não redeclara `Id` nem campos de auditoria/organização
- [ ] Mapping herda `CoreTableMapping<T>` com `("{{tabela}}", "{{schema}}")` em snake_case
- [ ] `ConfigureAdditionalProperties` só com o que foge do default
- [ ] FK entre módulos opcional quando fizer sentido, com `DeleteBehavior.Restrict`
- [ ] Migration gerada por `dotnet ef` após a mudança de entidade/mapping

Datas e contrato com o frontend (quando houver campos temporais):

- [ ] Instantes em `Instant`/`Instant?`; datas/horas civis nos tipos NodaTime correspondentes
- [ ] Auditoria herdada de `CoreEntity`, sem preenchimento ou conversão de fuso no handler
- [ ] JSON só via `AddInfiniteApiController` (sem `ConfigureForNodaTime` duplicado); instantes em UTC com `Z`, civis sem fuso inventado
- [ ] Entrada civil acompanhada de fuso IANA quando virar instante; política de ambiguidade explícita
- [ ] DTOs, Mapperly, filtros e OpenAPI compatíveis com o formato JSON real
- [ ] Runtime e design-time usam a mesma configuração Npgsql/NodaTime
- [ ] Atualização de contrato legado e migrations revisadas dentro do escopo autorizado

Configuração:

- [ ] Nenhum segredo/connection string/token commitado (vem do Consul / variável de ambiente)
- [ ] Nada de que a API dependa em `appsettings*.json` — o Consul descarta essas fontes em runtime
- [ ] Novas opções em classe `sealed` (`SectionName` ou opção tipada da lib Consul), registrada uma vez
- [ ] Usings da WebHost em `...Extensoes.*` (os antigos `...Extensions.*` não existem)

Pacotes:

- [ ] Nenhum `Microsoft.EntityFrameworkCore*`, `Npgsql*` ou `NodaTime` referenciado direto na API ou
      no Domain — vêm da `Infinite.Core.Postgres`
- [ ] Nenhum `NodaTime.Serialization.SystemTextJson` ou `Swashbuckle.AspNetCore` na API — vêm da WebHost

Logs:

- [ ] `src/.editorconfig` presente (CA1848, CA2254, CA1727 como `error`)
- [ ] Todo log novo segue o skill `infinite-api-logs`: método `[LoggerMessage]` com EventId em
      `{{Modulo}}/Common/Logs/{{Modulo}}Logs.cs`; handler/service só chama `_logger.{Fato}(...)`

Build:

- [ ] `dotnet build src/Infinite.{{Servico}}.slnx` limpo
