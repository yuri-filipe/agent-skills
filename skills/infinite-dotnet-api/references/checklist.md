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
- [ ] Retorna `Success/NotFound/Invalid/Error` — sem `throw` para fluxo esperado
- [ ] Sem validação de entrada manual (é do pipeline) e sem mapeamento manual (é do Mapperly)
- [ ] Listagem usa `PaginatedAsync` com argumentos nomeados e predicate do `Filters`

Dto / Mapper / Validator / Filter:

- [ ] DTO de listagem separado do DTO de detalhe
- [ ] Mapper `[Mapper] partial class` com métodos `partial`, sem corpo escrito à mão
- [ ] Mapper registrado como Singleton em `Startup.AddMappers`
- [ ] Validator base genérico sobre `I{{Entidade}}Writable`, incluído via `Include(...)`
- [ ] Validators de Id usam `.GreaterThan(0)`
- [ ] Filtro usa `PredicateBuilder.True<T>()` + `.And(...)`, checando null/empty antes de cada filtro

Persistência:

- [ ] Entidade herda `CoreEntity` e não redeclara `Id`
- [ ] Mapping herda `CoreTableMapping<T>` com `("{{tabela}}", "{{schema}}")` em snake_case
- [ ] `ConfigureAdditionalProperties` só com o que foge do default
- [ ] FK entre módulos opcional quando fizer sentido, com `DeleteBehavior.Restrict`
- [ ] Migration gerada por `dotnet ef` após a mudança de entidade/mapping

Configuração:

- [ ] Nenhum segredo/connection string/token commitado (vem do Consul / variável de ambiente)
- [ ] Novas opções em classe `sealed` com `SectionName`, registrada no `Startup`

Build:

- [ ] `dotnet build src/Infinite.{{Servico}}.slnx` limpo
