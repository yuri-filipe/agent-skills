using Infinite.Core.Response;
using MediatR;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

public sealed record Deletar{{Entidade}}Command(long Id) : IRequest<Response<bool>>;
