using Infinite.Core.Postgres.Interfaces;
using Infinite.Core.Response;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Dtos;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Mappers;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.QueryHandlers;

public sealed class Obter{{Entidade}}PorIdQueryHandler
(
    IReadRepository<{{Entidade}}> repo,
    {{Entidade}}Mapper mapper
) : IRequestHandler<Obter{{Entidade}}PorIdQuery, Response<{{Entidade}}Dto>>
{
    private readonly IReadRepository<{{Entidade}}> _repo = repo;
    private readonly {{Entidade}}Mapper _mapper = mapper;

    private const string NaoEncontradoMessage = "{{Entidade}} não encontrado(a).";

    public async Task<Response<{{Entidade}}Dto>> Handle(Obter{{Entidade}}PorIdQuery request, CancellationToken cancellationToken)
    {
        Response<{{Entidade}}Dto> response = new();

        {{Entidade}}? entidade = await _repo.Query
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entidade is null)
            return response.NotFound(NaoEncontradoMessage);

        return response.Success(_mapper.To{{Entidade}}Dto(entidade));
    }
}
