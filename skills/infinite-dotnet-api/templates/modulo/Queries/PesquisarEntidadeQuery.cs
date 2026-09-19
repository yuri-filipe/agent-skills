using Infinite.Core.Queries;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Dtos;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;

// Page/PageSize/SortField/SortOrder vêm de PagedQueryBase — não redeclarar.
public sealed record Pesquisar{{Entidade}}Query : PagedQueryBase<Pesquisar{{Entidade}}Dto>
{
    public string {{Campo}} { get; init; } = string.Empty;

    public string? {{CampoOpcional}} { get; init; } = string.Empty;

    public long? Id{{Relacionada}} { get; init; }
}
