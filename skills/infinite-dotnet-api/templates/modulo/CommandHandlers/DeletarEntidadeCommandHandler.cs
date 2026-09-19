using Infinite.Core.Postgres.Interfaces;
using Infinite.Core.Response;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using MediatR;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.CommandHandlers;

public sealed class Deletar{{Entidade}}CommandHandler
(
    IReadRepository<{{Entidade}}> readRepository,
    IWriteRepository<{{Entidade}}> writeRepository
) : IRequestHandler<Deletar{{Entidade}}Command, Response<bool>>
{
    private readonly IReadRepository<{{Entidade}}> _readRepository = readRepository;
    private readonly IWriteRepository<{{Entidade}}> _writeRepository = writeRepository;

    private const string NaoEncontradoMessage = "{{Entidade}} não encontrado(a) para exclusão.";
    private const string ExcluidoComSucessoMessage = "{{Entidade}} excluído(a) com sucesso.";

    public async Task<Response<bool>> Handle(Deletar{{Entidade}}Command request, CancellationToken cancellationToken)
    {
        var response = new Response<bool>();

        var entidade = await _readRepository.FindAsync([request.Id], cancellationToken);

        if (entidade is null)
            return response.NotFound(NaoEncontradoMessage);

        await _writeRepository.DeleteAsync(entidade, cancellationToken);

        return response.Success(true, ExcluidoComSucessoMessage);
    }
}
