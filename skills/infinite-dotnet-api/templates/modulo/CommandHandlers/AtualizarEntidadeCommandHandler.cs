using Infinite.Core.Postgres.Interfaces;
using Infinite.Core.Response;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Mappers;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.CommandHandlers;

public sealed class Atualizar{{Entidade}}CommandHandler
(
    IReadRepository<{{Entidade}}> readRepository,
    IWriteRepository<{{Entidade}}> writeRepository,
    {{Entidade}}Mapper mapper
) : IRequestHandler<Atualizar{{Entidade}}Command, Response<bool>>
{
    private readonly IReadRepository<{{Entidade}}> _readRepository = readRepository;
    private readonly IWriteRepository<{{Entidade}}> _writeRepository = writeRepository;
    private readonly {{Entidade}}Mapper _mapper = mapper;

    private const string NaoEncontradoMessage = "{{Entidade}} não encontrado(a) para atualização.";
    private const string AtualizadoComSucessoMessage = "{{Entidade}} atualizado(a) com sucesso.";

    public async Task<Response<bool>> Handle(Atualizar{{Entidade}}Command request, CancellationToken cancellationToken)
    {
        Response<bool> response = new();

        {{Entidade}}? entidade = await _readRepository.Query
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entidade is null)
            return response.NotFound(NaoEncontradoMessage);

        _mapper.Update{{Entidade}}FromCommand(request, entidade);

        await _writeRepository.UpdateAsync(entidade, cancellationToken);

        return response.Success(true, AtualizadoComSucessoMessage);
    }
}
