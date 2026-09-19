// Destino: Infinite.{{Servico}}.Domain/Common/Commands/DeletarBaseCommand.cs
using Infinite.Core.Response;
using MediatR;

namespace Infinite.{{Servico}}.Domain.Common.Commands;

public record DeletarBaseCommand(long Id) : IRequest<Response<bool>>;
