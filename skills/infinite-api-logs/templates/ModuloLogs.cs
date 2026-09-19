// Destino: Infinite.{{Servico}}.Domain/{{Modulo}}/Common/Observabilidade/{{Modulo}}Logs.cs
// Uma classe por módulo. Handlers, services e workers só chamam: _logger.{Fato}(...).
using Microsoft.Extensions.Logging;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.Common.Observabilidade;

/// <summary>
/// Logs do módulo {{Modulo}}, gerados em tempo de compilação (<see cref="LoggerMessageAttribute"/>).
/// </summary>
/// <remarks>
/// Bloco de EventIds: {{bloco}}000–{{bloco}}999. Os EventIds são contrato: dashboards e alertas
/// filtram por eles. Não reutilize nem renumere um id; acrescente o próximo livre ao final.
/// </remarks>
internal static partial class {{Modulo}}Logs
{
    // Fato barrado por regra de negócio: Warning com o id e a condição.
    [LoggerMessage(EventId = {{bloco}}001, Level = LogLevel.Warning,
        Message = "{{Acao}} recusado(a): {{entidade}} {{{Entidade}}Id} está em {StatusAtual}.")]
    public static partial void {{Acao}}Recusado(this ILogger logger, long {{entidade}}Id, string statusAtual);

    // Falha com exceção: o parâmetro Exception é anexado pelo gerador, fora do template.
    [LoggerMessage(EventId = {{bloco}}002, Level = LogLevel.Error,
        Message = "Falha ao {{verbo}} {{entidade}} {{{Entidade}}Id}.")]
    public static partial void FalhaAo{{Verbo}}(this ILogger logger, Exception excecao, long {{entidade}}Id);

    // Mudança de estado irreversível que alguém vai reconstruir: Information.
    [LoggerMessage(EventId = {{bloco}}003, Level = LogLevel.Information,
        Message = "{{Entidade}} {{{Entidade}}Id} {{participio}}.")]
    public static partial void {{Entidade}}{{Participio}}(this ILogger logger, long {{entidade}}Id);
}
