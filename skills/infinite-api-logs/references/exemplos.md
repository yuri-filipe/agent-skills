# Exemplos antes/depois

Todos os trechos seguem o padrão do skill `infinite-dotnet-api`: `sealed class`, primary constructor,
campos `private readonly`, mensagens em `private const string`.

---

## 1. Handler de command — CRUD simples: nenhum log

```csharp
public sealed class AdicionarAgendamentoCommandHandler
(
    IWriteRepository<Agendamento> repo,
    AgendamentoMapper mapper
) : IRequestHandler<AdicionarAgendamentoCommand, Response<long>>
{
    public async Task<Response<long>> Handle(AdicionarAgendamentoCommand request, CancellationToken cancellationToken)
    {
        Response<long> response = new();

        Agendamento entidade = _mapper.FromAdicionarCommand(request);

        await _repo.AddAsync(entidade, cancellationToken);

        return response.Success(entidade.Id, AdicionadoComSucessoMessage);
    }
}
```

**Não injete `ILogger` aqui.** Sucesso está na métrica, o insert está no trace do EF, e uma exceção
sobe para o `ErrorHandlingBehavior`, que já a loga e responde 500. Um `LogInformation("Agendamento adicionado.")` seria a
quarta cópia do mesmo fato.

---

## 2. Handler de command — decisão de negócio: `Warning` nos desvios

### Antes

```csharp
public async Task<Response<bool>> Handle(CancelarAgendamentoCommand request, CancellationToken cancellationToken)
{
    Response<bool> response = new();

    var agendamento = await _repo.GetByIdAsync(request.Id, cancellationToken);

    if (agendamento is null)
        return response.NotFound(NaoEncontradoMessage);

    if (agendamento.Status != StatusAgendamento.Confirmado)
        return response.Invalid(StatusInvalidoMessage);

    agendamento.Cancelar(request.Motivo);

    await _repo.UpdateAsync(agendamento, cancellationToken);

    return response.Success(true, CanceladoComSucessoMessage);
}
```

Quando o cliente reclama que "não consegue cancelar", não há nada no OpenObserve além de um 400: não
se sabe qual agendamento, nem em que status ele estava.

### Depois

```csharp
public sealed class CancelarAgendamentoCommandHandler
(
    IWriteRepository<Agendamento> repo,
    ILogger<CancelarAgendamentoCommandHandler> logger
) : IRequestHandler<CancelarAgendamentoCommand, Response<bool>>
{
    private readonly IWriteRepository<Agendamento> _repo = repo;
    private readonly ILogger<CancelarAgendamentoCommandHandler> _logger = logger;

    public async Task<Response<bool>> Handle(CancelarAgendamentoCommand request, CancellationToken cancellationToken)
    {
        Response<bool> response = new();

        var agendamento = await _repo.GetByIdAsync(request.Id, cancellationToken);

        if (agendamento is null)
        {
            _logger.LogWarning("Cancelamento recusado: agendamento {AgendamentoId} não encontrado.", request.Id);

            return response.NotFound(NaoEncontradoMessage);
        }

        if (agendamento.Status != StatusAgendamento.Confirmado)
        {
            _logger.LogWarning(
                "Cancelamento recusado: agendamento {AgendamentoId} está em {StatusAtual}.",
                agendamento.Id, agendamento.Status);

            return response.Invalid(StatusInvalidoMessage);
        }

        agendamento.Cancelar(request.Motivo);

        await _repo.UpdateAsync(agendamento, cancellationToken);

        _logger.LogInformation("Agendamento {AgendamentoId} cancelado.", agendamento.Id);

        return response.Success(true, CanceladoComSucessoMessage);
    }
}
```

Três logs — o teto. O `Information` final se justifica porque cancelar é irreversível e alguém vai
perguntar quando aconteceu; o `Adicionar` do exemplo 1 não tem essa propriedade.

---

## 3. Handler de query — quase sempre zero

```csharp
public async Task<ResponsePaged<PesquisarAgendamentoDto>> Handle(PesquisarAgendamentoQuery request, CancellationToken cancellationToken)
```

Lista vazia é resposta normal: **não logue**. Query lenta é trace e métrica, não log. Só entra log
numa query se ela cair num `catch` (exemplo 4) ou depender de integração externa (exemplo 5).

---

## 4. `catch` que engole a exceção

### Antes

```csharp
try
{
    await _repo.UpdateAsync(entidade, cancellationToken);
}
catch (Exception)
{
    return response.Error(FalhaAoAtualizarMessage);
}
```

A exceção morre ali: nem o `ErrorHandlingBehavior` a vê, nem o OpenObserve.

### Depois

```csharp
try
{
    await _repo.UpdateAsync(entidade, cancellationToken);
}
catch (DbUpdateException excecao)
{
    _logger.LogError(excecao, "Falha ao atualizar agendamento {AgendamentoId}.", entidade.Id);

    return response.Error(FalhaAoAtualizarMessage);
}
```

Se em vez de `response.Error` o `catch` fizesse `throw;`, **não haveria log aqui** — o
`ErrorHandlingBehavior` do pipeline MediatR já registra a exceção (e as internas) e devolve 500.

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
            _logger.LogError(
                excecao,
                "Faturamento respondeu {StatusCode} ao obter fatura do contrato {ContratoId}.",
                (int)excecao.StatusCode, contratoId);

            return null;
        }
        catch (TaskCanceledException excecao) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(excecao, "Timeout ao obter fatura do contrato {ContratoId}.", contratoId);

            return null;
        }
    }
}
```

- `excecao.Content` (corpo da resposta) **não** vai para o log: costuma trazer dado de cliente.
- O `when (!cancellationToken.IsCancellationRequested)` evita registrar como timeout o cancelamento
  do próprio cliente, que o `Send` já trata como 499.
- Se houvesse fallback (cache, valor default), o nível cairia para `Warning` e a mensagem diria o que
  foi assumido: `"... contrato {ContratoId}; usando a última fatura em cache."`

---

## 6. `BackgroundService` / job / consumer

Fora do pipeline HTTP não existe trace, métrica de request nem o `catch` do `ErrorHandlingBehavior`/`Send`. É o único lugar
onde início e fim de ciclo valem `Information`.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var cicloId = Guid.NewGuid();

        using (_logger.BeginScope(new Dictionary<string, object> { ["CicloId"] = cicloId }))
        {
            _logger.LogInformation("Ciclo de conciliação iniciado.");

            var pendentes = await ObterPendentesAsync(stoppingToken);
            var falhas = 0;

            foreach (var item in pendentes)
            {
                try
                {
                    await ConciliarAsync(item, stoppingToken);
                }
                catch (Exception excecao)
                {
                    falhas++;

                    _logger.LogError(excecao, "Falha ao conciliar lançamento {LancamentoId}.", item.Id);
                }
            }

            _logger.LogInformation(
                "Ciclo de conciliação concluído: {TotalProcessados} processados, {TotalFalhas} falhas.",
                pendentes.Count, falhas);
        }

        await Task.Delay(_intervalo, stoppingToken);
    }
}
```

Log por item **só na falha**. Sucesso por item vira o agregado do fim.

---

## 7. Controller

O `InfiniteApiController` já recebe `ILogger` e o usa no `Send`. **Controller não loga** — não tem
contexto de negócio para isso, e qualquer linha ali duplica a métrica de request.

```csharp
public sealed class AgendamentosController(IMediator mediator, ILogger<AgendamentosController> logger)
    : InfiniteApiController(mediator, logger)
{
    [HttpPost]
    public Task<IActionResult> Post([FromBody] AdicionarAgendamentoCommand command) => Send(command);
}
```

Exceção única: um endpoint que **não** passa pelo `Send` (upload, streaming, webhook de terceiro).
Aí valem as mesmas regras dos handlers.

---

## 8. Formulações de mensagem

| Ruim | Bom | Motivo |
|---|---|---|
| `$"Agendamento {id} não encontrado."` | `"Agendamento {AgendamentoId} não encontrado.", id` | Interpolação não vira campo indexado. |
| `"Erro"` / `"Falhou"` | `"Falha ao conciliar lançamento {LancamentoId}."` | Sem sujeito, o log não serve para buscar. |
| `"Processando..."` | (remover) | Não responde nenhuma pergunta. |
| `"Request: {@Command}"` | `"... contrato {ContratoId}."` | Objeto inteiro carrega dado pessoal. |
| `LogError("Falha: " + ex.Message)` | `LogError(ex, "Falha ao ...")` | Sem a exceção não há stack trace nem tipo. |
| `"Usuário {Email} sem permissão."` | `"Usuário {UsuarioId} sem permissão para {Acao}."` | E-mail é dado pessoal. |
