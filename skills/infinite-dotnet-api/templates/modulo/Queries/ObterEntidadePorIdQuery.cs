using Infinite.{{Servico}}.Domain.Common.Queries;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Dtos;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;

public sealed record Obter{{Entidade}}PorIdQuery(long Id) : ObterPorIdBaseQuery<{{Entidade}}Dto>(Id);
