using Infinite.Core.Response;
using MediatR;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

public sealed class Atualizar{{Entidade}}Command : IRequest<Response<bool>>, I{{Entidade}}Writable
{
    public long Id { get; set; }

    public string {{Campo}} { get; set; } = string.Empty;

    public string? {{CampoOpcional}} { get; set; } = string.Empty;

    public long Id{{Relacionada}} { get; set; }
}
