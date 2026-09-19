using Infinite.Core.Postgres.Interfaces;
using Infinite.Core.Response;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Mappers;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using MediatR;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.CommandHandlers;

public sealed class Adicionar{{Entidade}}CommandHandler
(
    IWriteRepository<{{Entidade}}> repo,
    {{Entidade}}Mapper mapper
) : IRequestHandler<Adicionar{{Entidade}}Command, Response<long>>
{
    private readonly IWriteRepository<{{Entidade}}> _repo = repo;
    private readonly {{Entidade}}Mapper _mapper = mapper;

    private const string AdicionadoComSucessoMessage = "{{Entidade}} adicionado(a) com sucesso.";

    public async Task<Response<long>> Handle(Adicionar{{Entidade}}Command request, CancellationToken cancellationToken)
    {
        Response<long> response = new();

        {{Entidade}} entidade = _mapper.FromAdicionarCommand(request);

        await _repo.AddAsync(entidade, cancellationToken);

        return response.Success(entidade.Id, AdicionadoComSucessoMessage);
    }
}
