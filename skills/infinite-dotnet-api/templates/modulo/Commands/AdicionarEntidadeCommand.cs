using Infinite.Core.Response;
using MediatR;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

public sealed class Adicionar{{Entidade}}Command : IRequest<Response<long>>, I{{Entidade}}Writable
{
    public string {{Campo}} { get; set; } = string.Empty;

    public string? {{CampoOpcional}} { get; set; } = string.Empty;

    public long Id{{Relacionada}} { get; set; }
}
