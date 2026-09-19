// Destino: Infinite.{{Servico}}.Domain/{{Modulo}}/Common/Queries/Pesquisar{{Modulo}}BaseQuery.cs
// Use apenas quando várias entidades do mesmo módulo compartilham os mesmos filtros.
using Infinite.Core.Queries;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.Common.Queries;

public abstract record Pesquisar{{Modulo}}BaseQuery<TResponse> : PagedQueryBase<TResponse>
{
    public string {{CampoComum}} { get; init; } = string.Empty;

    public DateTime? DataInicio { get; init; }

    public DateTime? DataFim { get; init; }
}
