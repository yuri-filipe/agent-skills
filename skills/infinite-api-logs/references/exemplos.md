# Exemplos antes/depois

Todos os trechos seguem o skill `infinite-dotnet-api` (primary constructor, campos
`private readonly`, mensagens de resposta em `private const string`) e o padrão obrigatório de
log: os métodos `[LoggerMessage]` ficam na classe de logs do módulo
(`{{Modulo}}/Common/Logs/{{Modulo}}Logs.cs`) e o código de negócio **só chama** `_logger.{Fato}(...)`.

Módulos dos exemplos: `Comercial` (bloco `10000–10999`) e `Financeiro` (bloco `11000–11999`).

## 0. Classes de logs dos módulos

```csharp
// Infinite.Agendamentos.Domain/Comercial/Common/Logs/ComercialLogs.cs
namespace Infinite.Agendamentos.Domain.Comercial.Common.Logs;

/// <summary>Logs do módulo Comercial. Bloco de EventIds: 10000–10999.</summary>
internal static partial class ComercialLogs
{
    [LoggerMessage(EventId = 10001, Level = LogLevel.Warning,
        Message = "Cancelamento recusado: agendamento {AgendamentoId} não encontrado.")]
    public static partial void AgendamentoNaoEncontradoParaCancelar(this ILogger logger, long agendamentoId);

    [LoggerMessage(EventId = 10002, Level = LogLevel.Warning,
        Message = "Cancelamento recusado: agendamento {AgendamentoId} está em {StatusAtual}.")]
    public static partial void CancelamentoRecusado(this ILogger logger, long agendamentoId, StatusAgendamento statusAtual);

    [LoggerMessage(EventId = 10003, Level = LogLevel.Information,
        Message = "Agendamento {AgendamentoId} cancelado.")]
    public static partial void AgendamentoCancelado(this ILogger logger, long agendamentoId);

    [LoggerMessage(EventId = 10004, Level = LogLevel.Error,
        Message = "Falha ao atualizar agendamento {AgendamentoId}.")]
    public static partial void FalhaAoAtualizarAgendamento(this ILogger logger, Exception excecao, long agendamentoId);
}
```

```csharp
// Infinite.Agendamentos.Domain/Financeiro/Common/Logs/FinanceiroLogs.cs
namespace Infinite.Agendamentos.Domain.Financeiro.Common.Logs;

/// <summary>Logs do módulo Financeiro. Bloco de EventIds: 11000–11999.</summary>
internal static partial class FinanceiroLogs
{
    [LoggerMessage(EventId = 11001, Level = LogLevel.Error,
        Message = "Faturamento respondeu {StatusCode} ao obter fatura do contrato {ContratoId}.")]
    public static partial void FaturamentoRespondeuErro(this ILogger logger, Exception excecao, int statusCode, long contratoId);

    [LoggerMessage(EventId = 11002, Level = LogLevel.Error,
        Message = "Timeout ao obter fatura do contrato {ContratoId}.")]
    public static partial void TimeoutFaturamento(this ILogger logger, Exception excecao, long contratoId);

    [LoggerMessage(EventId = 11003, Level = LogLevel.Information, Message = "Ciclo de conciliação iniciado.")]
    public static partial void CicloConciliacaoIniciado(this ILogger logger);

    [LoggerMessage(EventId = 11004, Level = LogLevel.Error, Message = "Falha ao conciliar lançamento {LancamentoId}.")]
    public static partial void FalhaAoConciliar(this ILogger logger, Exception excecao, long lancamentoId);

    [LoggerMessage(EventId = 11005, Level = LogLevel.Information,
        Message = "Ciclo de conciliação concluído: {TotalProcessados} processados, {TotalFalhas} falhas.")]
    public static partial void CicloConciliacaoConcluido(this ILogger logger, int totalProcessados, int totalFalhas);

    [LoggerMessage(EventId = 11006, Level = LogLevel.Warning,
        Message = "Divergências no lote {LoteId}: {Resumo}.")]
    public static partial void DivergenciasNoLote(this ILogger logger, long loteId, string resumo);
}
```

Os exemplos abaixo só mostram as **chamadas**; as declarações estão aqui.

---

## 1. Handler de command — CRUD simples: nenhum log

```csharp
public sealed class AdicionarAgendamentoCommandHandler
(
    IWriteRepository<Agendamento> repo,
    AgendamentoMapper mapper
) : IRequestHandler<AdicionarAgendamentoCommand, Response<long>>
{
    // ...
    public async Task<Response<long>> Handle(AdicionarAgendamentoCommand request, CancellationToken cancellationToken)
    {
        Response<long> response = new();

        Agendamento entidade = _mapper.FromAdicionarCommand(request);

        await _repo.AddAsync(entidade, cancellationToken);

        return response.Success(entidade.Id, AdicionadoComSucessoMessage);
    }
}
```

**Não injete `ILogger` aqui nem crie método na classe de logs.** Sucesso está na métrica, o insert está no
trace do EF, e uma exceção sobe para o `ErrorHandlingBehavior` (EventId 4000), que já a registra e
responde 500.

---

## 2. Handler de command — decisão de negócio: `Warning` nos desvios

### Antes

```csharp
if (agendamento is null)
    return response.NotFound(NaoEncontradoMessage);

if (agendamento.Status != StatusAgendamento.Confirmado)
    return response.Invalid(StatusInvalidoMessage);
```

Quando o cliente reclama que "não consegue cancelar", não há nada no OpenObserve além de um 400.

### Antes (padrão antigo, agora barrado pelo build)

```csharp
_logger.LogWarning("Cancelamento recusado: agendamento {AgendamentoId} não encontrado.", request.Id); // CA1848
```

Também não declare `[LoggerMessage]` dentro do handler: o método vai para `ComercialLogs`.

### Depois

```csharp
public sealed class CancelarAgendamentoCommandHandler
(
    IReadRepository<Agendamento> readRepository,
    IWriteRepository<Agendamento> writeRepository,
    ILogger<CancelarAgendamentoCommandHandler> logger
) : IRequestHandler<CancelarAgendamentoCommand, Response<bool>>
{
    private readonly IReadRepository<Agendamento> _readRepository = readRepository;
    private readonly IWriteRepository<Agendamento> _writeRepository = writeRepository;
    private readonly ILogger<CancelarAgendamentoCommandHandler> _logger = logger;

    public async Task<Response<bool>> Handle(CancelarAgendamentoCommand request, CancellationToken cancellationToken)
    {
        Response<bool> response = new();

        var agendamento = await _readRepository.Query
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (agendamento is null)
        {
            _logger.AgendamentoNaoEncontradoParaCancelar(request.Id);

            return response.NotFound(NaoEncontradoMessage);
        }

        if (agendamento.Status != StatusAgendamento.Confirmado)
        {
            _logger.CancelamentoRecusado(agendamento.Id, agendamento.Status);

            return response.Invalid(StatusInvalidoMessage);
        }

        agendamento.Cancelar(request.Motivo);

        await _writeRepository.UpdateAsync(agendamento, cancellationToken);

        _logger.AgendamentoCancelado(agendamento.Id);

        return response.Success(true, CanceladoComSucessoMessage);
    }
}
```

Três logs — o teto. O `Information` final se justifica porque cancelar é irreversível e alguém vai
perguntar quando aconteceu; o `Adicionar` do exemplo 1 não tem essa propriedade.

---

## 3. Handler de query — quase sempre zero

Lista vazia é resposta normal: **não logue**. Query lenta é trace e métrica, não log. Só entra log
numa query se ela cair num `catch` (exemplo 4) ou depender de integração externa (exemplo 5).

---

## 4. `catch` que engole a exceção

### Antes

```csharp
catch (Exception)
{
    return response.Error(FalhaAoAtualizarMessage);
}
```

A exceção morre ali: nem o `ErrorHandlingBehavior` a vê, nem o OpenObserve.

### Depois

```csharp
catch (DbUpdateException excecao)
{
    _logger.FalhaAoAtualizarAgendamento(excecao, entidade.Id);

    return response.Error(FalhaAoAtualizarMessage);
}
```

O parâmetro `Exception` é anexado pelo gerador (tipo, mensagem, stack e `InnerException`); ele não
aparece no template. Se o `catch` fizesse `throw;`, **não haveria log aqui** — o
`ErrorHandlingBehavior` já registra a exceção e devolve 500.

---

## 5. Integração externa (Refit / HttpClient)

O trace do `HttpClient` já mostra URL, status e duração. O log entra para dizer **em qual operação de
negócio** aquilo quebrou e o que a API fez a seguir.

```csharp
public sealed class ServicoFaturamento
(
    IApiFaturamento api,
    ILogger<ServicoFaturamento> logger
)
{
    private readonly IApiFaturamento _api = api;
    private readonly ILogger<ServicoFaturamento> _logger = logger;

    public async Task<FaturaDto?> ObterFaturaAsync(long contratoId, CancellationToken cancellationToken)
    {
        try
        {
            return await _api.ObterFaturaAsync(contratoId, cancellationToken);
        }
        catch (ApiException excecao)
        {
            _logger.FaturamentoRespondeuErro(excecao, (int)excecao.StatusCode, contratoId);

            return null;
        }
        catch (TaskCanceledException excecao) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.TimeoutFaturamento(excecao, contratoId);

            return null;
        }
    }
}
```

- `excecao.Content` (corpo da resposta) **não** vai para o log: costuma trazer dado de cliente.
- O `when (!cancellationToken.IsCancellationRequested)` evita registrar como timeout o cancelamento
  do próprio cliente, que o `Send` já trata como 499.
- Com fallback (cache, valor default), o nível cai para `Warning` e a mensagem diz o que foi
  assumido: `"... contrato {ContratoId}; usando a última fatura em cache."`

---

## 6. `BackgroundService` / job / consumer

Fora do pipeline HTTP não existe trace, métrica de request nem o `catch` do
`ErrorHandlingBehavior`/`Send`. É o único lugar onde início e fim de ciclo valem `Information`.

```csharp
public sealed class ConciliacaoWorker(/* ... */, ILogger<ConciliacaoWorker> logger) : BackgroundService
{
    private readonly ILogger<ConciliacaoWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Escopo com dicionário: todos os logs do ciclo levam CicloId. BeginScope com
            // template é barrado pelo CA1848.
            using (_logger.BeginScope(new Dictionary<string, object> { ["CicloId"] = Guid.NewGuid() }))
            {
                _logger.CicloConciliacaoIniciado();

                var pendentes = await ObterPendentesAsync(stoppingToken);
                var falhas = 0;

                foreach (var item in pendentes)
                {
                    try
                    {
                        await ConciliarAsync(item, stoppingToken);
                    }
                    catch (Exception excecao) when (excecao is not OperationCanceledException)
                    {
                        falhas++;
                        _logger.FalhaAoConciliar(excecao, item.Id);
                    }
                }

                _logger.CicloConciliacaoConcluido(pendentes.Count, falhas);
            }

            await Task.Delay(_intervalo, stoppingToken);
        }
    }
}
```

Log por item **só na falha**. Sucesso por item vira o agregado do fim.

---

## 7. Controller

O `InfiniteApiController` já recebe `ILogger` e o usa no `Send`. **Controller não loga** — não tem
contexto de negócio, e qualquer linha ali duplica a métrica de request.

```csharp
public sealed class AgendamentosController(IMediator mediator, ILogger<AgendamentosController> logger)
    : InfiniteApiController(mediator, logger)
{
    [HttpPost]
    public Task<IActionResult> Post([FromBody] AdicionarAgendamentoCommand command) => Send(command);
}
```

Exceção única: um endpoint que **não** passa pelo `Send` (upload, streaming, webhook de terceiro).
Aí valem as mesmas regras dos handlers: o método vai para a classe de logs do módulo e o
controller só o chama.

---

## 8. Argumento caro

O método gerado só formata se o nível estiver habilitado, mas **os argumentos são avaliados antes
da chamada**. Para um argumento caro, proteja:

```csharp
if (_logger.IsEnabled(LogLevel.Warning))
    _logger.DivergenciasNoLote(lote.Id, CalcularResumoDivergencias(lote));
```

---

## 9. Formulações de mensagem

| Ruim | Bom | Motivo |
|---|---|---|
| `_logger.LogWarning("Agendamento {AgendamentoId}...", id)` | `_logger.AgendamentoNaoEncontradoParaCancelar(id)`, declarado em `ComercialLogs` | Padrão obrigatório (CA1848). |
| `[LoggerMessage]` declarado dentro do handler/service | método na classe `{{Modulo}}Logs` do módulo | Código de negócio só chama o log. |
| `$"Agendamento {id} não encontrado."` | `"Agendamento {AgendamentoId} não encontrado."` | Interpolação não vira campo indexado (CA2254). |
| `{agendamentoId}` / `{0}` | `{AgendamentoId}` | PascalCase e nome de negócio (CA1727). |
| `"Erro"` / `"Falhou"` | `"Falha ao conciliar lançamento {LancamentoId}."` | Sem sujeito, o log não serve para buscar. |
| `"Processando..."` | (remover) | Não responde nenhuma pergunta. |
| `"Request: {@Command}"` | `"... contrato {ContratoId}."` | Objeto inteiro carrega dado pessoal. |
| `"Falha: {Mensagem}", ex.Message` | parâmetro `Exception excecao` no método | Sem a exceção não há stack trace nem tipo. |
| `"Usuário {Email} sem permissão."` | `"Usuário {UsuarioId} sem permissão para {Acao}."` | E-mail é dado pessoal. |
| EventId repetido ou sem EventId | próximo id livre do bloco do módulo | EventId é contrato de filtro e alerta. |
