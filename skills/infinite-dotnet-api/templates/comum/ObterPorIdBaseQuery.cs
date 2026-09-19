// Destino: Infinite.{{Servico}}.Domain/Common/Queries/ObterPorIdBaseQuery.cs
using Infinite.Core.Response;
using MediatR;

namespace Infinite.{{Servico}}.Domain.Common.Queries;

public record ObterPorIdBaseQuery<T>(long Id) : IRequest<Response<T>>;
