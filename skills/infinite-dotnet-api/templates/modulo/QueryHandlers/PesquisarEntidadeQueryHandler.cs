using Infinite.Core.Postgres.Interfaces;
using Infinite.Core.Response;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Dtos;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Filters;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Mappers;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;
using MediatR;
using System.Linq.Expressions;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.QueryHandlers;

public sealed class Pesquisar{{Entidade}}QueryHandler
(
    IReadRepository<{{Entidade}}> repo,
    {{Entidade}}Mapper mapper
) : IRequestHandler<Pesquisar{{Entidade}}Query, ResponsePaged<Pesquisar{{Entidade}}Dto>>
{
    private readonly IReadRepository<{{Entidade}}> _repo = repo;
    private readonly {{Entidade}}Mapper _mapper = mapper;

    public async Task<ResponsePaged<Pesquisar{{Entidade}}Dto>> Handle(Pesquisar{{Entidade}}Query query, CancellationToken cancellationToken)
    {
        Expression<Func<{{Entidade}}, bool>> predicate = query.GerarFiltro();

        return await _repo.PaginatedAsync(
            request: query,
            mapper: _mapper.ToPesquisarDto,
            predicate: predicate,
            cancellationToken: cancellationToken
        );
    }
}
