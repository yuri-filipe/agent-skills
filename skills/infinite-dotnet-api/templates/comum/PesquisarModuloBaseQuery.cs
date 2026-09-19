// Destino: Infinite.{{Servico}}.Domain/{{Modulo}}/Common/Queries/Pesquisar{{Modulo}}BaseQuery.cs
// Use apenas quando várias entidades do mesmo módulo compartilham os mesmos filtros.
using Infinite.Core.Queries;
using NodaTime;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.Common.Queries;

public abstract record Pesquisar{{Modulo}}BaseQuery<TResponse> : PagedQueryBase<TResponse>
{
    public string {{CampoComum}} { get; init; } = string.Empty;

    // Limites de instantes UTC. Para filtros por dia civil, use LocalDate + fuso explícito.
    public Instant? DataInicio { get; init; }

    public Instant? DataFim { get; init; }
}
