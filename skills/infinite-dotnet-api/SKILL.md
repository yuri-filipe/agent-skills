---
name: infinite-dotnet-api
description: "Cria e revisa APIs .NET 10 da Infinite com CQRS, MediatR, EF Core/Postgres, NodaTime e Infinite.Core. Use para serviços, CRUDs, handlers, queries, datas, fusos e JSON."
---

# Estrutura padrão — APIs .NET da Infinite

Padrão estrutural único para **todos os serviços backend**. Este skill descreve **apenas estrutura,
nomenclatura e contratos técnicos** — nunca regras de negócio de um serviço específico.

**Autenticação/autorização está fora deste skill** — use o skill `infinite-auth-zitadel`. Não
invente `[Authorize]`, claims, políticas ou middleware de auth ao seguir este skill.

## Placeholders

Ao aplicar, substitua:

| Placeholder | Significado | Exemplo |
|---|---|---|
| `{{Servico}}` | nome do serviço (PascalCase, sem `Infinite.`) | `Cadastro`, `Agendamentos` |
| `{{servico}}` | mesmo nome em minúsculo/kebab (rotas, Consul, imagem) | `agendamentos` |
| `{{Modulo}}` | módulo funcional dentro do domínio | `Comercial`, `Financeiro` |
| `{{Entidade}}` | entidade no singular | `Agendamento` |
| `{{Entidades}}` | entidade no plural | `Agendamentos` |
| `{{rota}}` | plural em kebab-case para a rota | `agendamentos` |
| `{{schema}}` | schema Postgres em snake_case | `comercial` |
| `{{tabela}}` | tabela em snake_case singular | `agendamento` |
| `{{Campo}}`, `{{CampoOpcional}}`, `{{Relacionada}}`, `{{Filho(s)}}`, `{{tamanho}}` | campos/relacionamentos reais da entidade — os templates são esqueleto, não contrato de dados | — |

## Regras invioláveis

1. **Todo namespace/assembly começa com `Infinite.`** — o resto é livre (`Infinite.Cadastro`,
   `Infinite.Agendamentos`). Não replique nomes de outros serviços.
2. **Três projetos, sempre**: `Infinite.{{Servico}}` (Web API), `Infinite.{{Servico}}.Domain`
   (domínio + aplicação/CQRS), `Infinite.{{Servico}}.Infrastructure` (mappings EF + migrations).
   Solution `.slnx` em `src/`.
3. **Controller nunca acessa repositório, DbContext ou service** — só `Send(command|query)`.
4. **Handler nunca valida manualmente entrada** — validação é FluentValidation resolvida pelo
   pipeline do `AddInfiniteCqrs` (falha vira 422). Handler valida apenas *estado* (existe? já processado?).
5. **Handler nunca lança exceção para fluxo esperado** — retorna `response.NotFound/Invalid/Forbidden/Error`.
   Exceção inesperada é capturada pelo `ErrorHandlingBehavior` (log + 500); não envolva o handler
   em `try/catch` só para isso.
6. **Query usa `IReadRepository<T>`; command usa `IWriteRepository<T>`** (update/delete podem usar
   os dois: ler para localizar, escrever para persistir).
7. **Tudo assíncrono com `CancellationToken`** propagado até o repositório.
8. **Mapeamento é Mapperly** (`[Mapper] partial class`), nunca mapeamento manual em handler.
9. **Um arquivo por tipo**, nome do arquivo == nome do tipo.
10. **Antes de criar qualquer classe, procure uma entidade equivalente no mesmo módulo e copie a
    forma dela.** O código existente vence este documento em caso de divergência.

## Layout de pastas

```text
{{repo}}/
  azure-pipelines.yml
  Dockerfile
  src/
    nuget.config
    Infinite.{{Servico}}.slnx
    Infinite.{{Servico}}/                      # Web API
      Program.cs
      Startup.cs
      appsettings.json
      appsettings.Development.json
      Properties/launchSettings.json
      Controllers/{{Modulo}}/{{Entidades}}Controller.cs
      Postgres/InfiniteContextFactory.cs
    Infinite.{{Servico}}.Domain/
      Common/
        Commands/DeletarBaseCommand.cs
        Queries/ObterPorIdBaseQuery.cs
      {{Modulo}}/
        {{Entidades}}/
          Commands/
          CommandHandlers/
          Queries/
          QueryHandlers/
          Dtos/
          Mappers/
          Validators/
          Filters/
          Models/
          Enums/
        Services/                              # serviços de módulo (opcional)
    Infinite.{{Servico}}.Infrastructure/
      Mappings/{{Modulo}}/{{Entidade}}Mapping.cs
      Migrations/
```

Regras de pasta:

- **Nunca** crie pasta fora dessa lista. Sem `Interfaces/`, `Helpers/`, `Utils/`, `Repositories/`,
  `Application/`, `Core/`. Interfaces moram junto do que descrevem (`I{{Entidade}}Writable` em
  `Commands/`, `I{{Servico}}Service` em `Services/`).
- Pasta só existe se tiver conteúdo — exceto quando o `.csproj` declara `<Folder Include=... />`.
- `Models/` fica em `{{Modulo}}/{{Entidades}}/Models/`. Se várias features do mesmo módulo
  compartilham as entidades, promova para `{{Modulo}}/Models/` — mas escolha **um** dos dois por
  módulo e mantenha.
- `Filters/` contém só extension methods de predicate; `Enums/` só enums do módulo/entidade.
- `Services/` é para orquestração reutilizada por vários handlers ou integração externa (Refit).
  Não crie service para CRUD simples.

## Nomenclatura de arquivos

| Tipo | Arquivo | Pasta |
|---|---|---|
| Command criar | `Adicionar{{Entidade}}Command.cs` | `Commands` |
| Command atualizar | `Atualizar{{Entidade}}Command.cs` | `Commands` |
| Command excluir | `Deletar{{Entidade}}Command.cs` | `Commands` |
| Contrato compartilhado de escrita | `I{{Entidade}}Writable.cs` | `Commands` |
| Handler de command | `<NomeDoCommand>Handler.cs` | `CommandHandlers` |
| Query paginada | `Pesquisar{{Entidade}}Query.cs` | `Queries` |
| Query por id | `Obter{{Entidade}}PorIdQuery.cs` | `Queries` |
| Handler de query | `<NomeDaQuery>Handler.cs` | `QueryHandlers` |
| DTO de detalhe | `{{Entidade}}Dto.cs` | `Dtos` |
| DTO de listagem | `Pesquisar{{Entidade}}Dto.cs` | `Dtos` |
| Mapper | `{{Entidade}}Mapper.cs` | `Mappers` |
| Filtro | `Pesquisar{{Entidade}}FilterExtension.cs` | `Filters` |
| Validator base | `{{Entidade}}BaseValidator.cs` | `Validators` |
| Validator | `<NomeDoCommandOuQuery>Validator.cs` | `Validators` |
| Entidade | `{{Entidade}}.cs` | `Models` |
| Mapping EF | `{{Entidade}}Mapping.cs` | `Infrastructure/Mappings/{{Modulo}}` |
| Controller | `{{Entidades}}Controller.cs` | `Controllers/{{Modulo}}` |

Verbos padronizados em português: **Adicionar / Atualizar / Deletar / Pesquisar / Obter…PorId**.
Não misture com Create/Update/Delete/Get/Search. Para ações fora do CRUD, use verbo de negócio no
infinitivo (`Cancelar{{Entidade}}Command`, `Processar{{Entidade}}Command`).

## Convenções de código

**Commands**: `sealed class`, propriedades `get; set;`, `IRequest<Response<long>>` para criação e
`IRequest<Response<bool>>` para atualização. Delete é `sealed record Deletar{{Entidade}}Command(long Id)`.
Quando adicionar e atualizar compartilham campos, extraia `I{{Entidade}}Writable` e implemente nos dois
— é o que permite um único validator base genérico.

**Queries**: `sealed record` com `init`. Paginada herda `PagedQueryBase<Pesquisar{{Entidade}}Dto>`;
por id herda `ObterPorIdBaseQuery<{{Entidade}}Dto>`.

**Handlers**: `public sealed class`, **primary constructor** + campos `private readonly` atribuídos
a partir dele, mensagens em `private const string ...Message`, `Response<T> response = new();` na
primeira linha, retorno via `response.Success(...)`.

**Controllers**: primary constructor `(IMediator mediator, ILogger<T> logger) : InfiniteApiController(mediator, logger)`,
`[Route("v1/{{rota}}")]`, `[ApiController]`, métodos `Find / GetById / Post / Put / Delete` retornando
`Task<IActionResult>` via `Send(...)`. No `Put`, atribua `command.Id = id;` antes do `Send`.

Ver `templates/` para os arquivos completos e `references/` para detalhes.

## Como aplicar

**Datas, fusos e contrato com o frontend** → leia [references/datas-e-fusos.md](references/datas-e-fusos.md)
ao criar um serviço ou alterar campos temporais, DTOs, filtros, JSON ou mappings. O padrão é
`Instant` para instantes, transporte ISO-8601 UTC com `Z` e fuso escolhido na aplicação/frontend.
`LocalDate`, `LocalTime` e `LocalDateTime` representam valores civis, sem conversão implícita de fuso.
O JSON (NodaTime e regras estritas) já é configurado por `AddInfiniteApiController`; não registre de novo.

**Configuração** → tudo vem do Consul: `AddConsulConfig` descarta `appsettings*.json` em runtime.
Seções mínimas em `templates/servico/consul-config.json`.

**Novo serviço** → siga `references/novo-servico.md` e copie `templates/servico/`.

**Novo CRUD** → copie `templates/modulo/` inteiro trocando os placeholders, e então:
1. Entidade em `Models/` herdando `CoreEntity`.
2. Mapping em `Infrastructure/Mappings/{{Modulo}}/` herdando `CoreTableMapping<{{Entidade}}>`.
3. Registre o mapper em `Startup.AddMappers` como **Singleton**.
4. Controller em `Controllers/{{Modulo}}/`.
5. Migration via `dotnet ef` (ver `references/novo-servico.md`).
6. `dotnet build src/Infinite.{{Servico}}.slnx`.

Nada mais precisa ser registrado: handlers e validators são varridos por assembly, e os
`{{Entidade}}Mapping` por assembly do `AddInfiniteContext`.

## Referências

- [references/datas-e-fusos.md](references/datas-e-fusos.md) — NodaTime, JSON UTC, entrada civil
  com fuso IANA, auditoria e compatibilidade com consumidores existentes.

- `references/libs-infinite.md` — superfície das libs `Infinite.Core.*` (Response e contrato HTTP,
  repositórios, paginação, auditoria, entidade e mapping base, JSON, Consul).
- `references/novo-servico.md` — bootstrap de um serviço do zero, config (Consul), pipeline, EF.
- `references/checklist.md` — checklist de revisão/PR.
- `templates/servico/` — Program, Startup, csproj, slnx, nuget.config, appsettings, launchSettings,
  context factory (lendo Consul), pipeline e `consul-config.json` com as seções mínimas.
- `templates/modulo/` — CRUD completo com placeholders (Commands, CommandHandlers, Queries,
  QueryHandlers, Dtos, Mappers, Validators, Filters, Models, Mapping EF, Controller).
- `templates/comum/` — bases compartilhadas (`ObterPorIdBaseQuery`, `DeletarBaseCommand`,
  query base por módulo) e o padrão de integração externa (Options + Refit + service).
