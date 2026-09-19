---
name: infinite-api-logs
description: "Adiciona e revisa logs em APIs .NET da Infinite no padrão Microsoft: [LoggerMessage] gerado em compilação, EventId e templates estruturados. Use para handlers, integrações e workers."
---

# Logs de aplicação — APIs .NET da Infinite

A API já exporta logs, métricas e traces por OTLP: `AddInfiniteApiController` chama
`AdicionarInfiniteObservabilidade`, e todo `ILogger` cai no console **e** no OpenObserve com
`trace_id` correlacionado. **Nada precisa ser registrado, instalado ou configurado.** Este skill decide
**onde** falta log, em **qual nível**, com **qual mensagem** — e aplica no padrão obrigatório abaixo.

O padrão segue a documentação oficial
[Registro em log em C# — .NET](https://learn.microsoft.com/pt-br/dotnet/core/extensions/logging/overview):
logging gerado em tempo de compilação, categoria pela classe, EventId, modelo de mensagem
estruturado, exceção pela sobrecarga própria e nível configurado por categoria. As libs
`Infinite.Core.*` já seguem esse padrão (`MensagensLog`).

## Escopo

**Faz**: mantém a classe de logs do módulo (`Common/Observabilidade`), declara nela os métodos `[LoggerMessage]`, injeta `ILogger<T>`, chama os métodos nos pontos certos,
corrige nível e mensagem, converte chamadas `Log*` antigas, remove log ruidoso ou perigoso e mantém o
`.editorconfig` que obriga o padrão.

**Não faz**: seção `Observabilidade` do Consul, pacotes NuGet, `UseSerilog`, middleware de log,
dashboards, métricas, spans. Nada de `Console.WriteLine`, `Debug.WriteLine` ou `Trace.Write`. Não
refatora handler, não muda contrato, não altera regra de negócio, não troca `Response<T>` por
exceção para "ter o que logar". `Logging:LogLevel` só quando a tarefa pedir (ver
`references/niveis.md`).

## Padrão obrigatório

1. **Todo log é um método `[LoggerMessage]`** gerado em compilação. É proibido chamar
   `LogTrace/LogDebug/LogInformation/LogWarning/LogError/LogCritical` ou `Log(LogLevel, ...)`
   diretamente. O build recusa (CA1848).
2. **Os métodos ficam numa classe de logs separada, uma por módulo**:
   `{{Modulo}}/Common/Observabilidade/{{Modulo}}Logs.cs` no projeto Domain — `internal static partial class`
   com métodos de extensão `public static partial void {Fato}(this ILogger logger, ...)`.
   Handlers, services, workers e demais classes **só chamam** `_logger.{Fato}(...)`; nenhum
   `[LoggerMessage]`, template ou nível fica espalhado no código de negócio. Template em
   `templates/ModuloLogs.cs`.
   **Nunca** use pasta chamada `Log` ou `Logs`: o `.gitignore` padrão do Visual Studio ignora
   `[Ll]og/` e `[Ll]ogs/`, e o arquivo some do commit sem aviso (o build local passa, o da
   pipeline quebra). `Observabilidade` é o mesmo nome usado pela WebHost (`Observabilidade/MensagensLog.cs`).
3. **Categoria = a classe que registra**: injete `ILogger<PropriaClasse>` pelo primary constructor
   (último parâmetro) com campo `private readonly`. A classe de logs recebe esse logger e não
   muda a categoria. Nunca `ILoggerFactory.CreateLogger("texto")` em código de aplicação.
4. **`EventId` obrigatório, único no serviço e estável.** Faixas:
   `1000–4999` são das libs (`Infinite.Core.WebHost` 1000–3999, `Infinite.Core` 4000–4999);
   serviços usam `10000+`, **um bloco de 1000 por módulo**, declarado no cabeçalho da classe de logs
   do módulo. Módulo novo = maior bloco existente + 1000. Dentro do bloco, ids sequenciais. Nunca
   reutilize nem renumere: EventId é contrato de filtro e alerta. O gerador **não** acusa id
   repetido; confira com o comando de "Como aplicar".
5. **Modelo de mensagem constante** com placeholders nomeados em **PascalCase** e com nome de
   negócio (`{AgendamentoId}`, `{StatusAtual}`). Nada de interpolação `$"..."`, concatenação ou
   `{0}`. O build recusa template não constante (CA2254) e placeholder fora de PascalCase (CA1727).
6. **Exceção é parâmetro `Exception`** do método, fora do template. Nunca `excecao.Message` na
   mensagem: perde tipo, stack e `InnerException`.
7. **Log é síncrono e barato.** Nenhum `await`, I/O ou serialização para montar argumentos. Se um
   argumento for caro de calcular, proteja com `_logger.IsEnabled(LogLevel.X)`.
8. **Escopo só com dicionário**: `_logger.BeginScope(new Dictionary<string, object> { ["CicloId"] = id })`.
   A sobrecarga com template também é barrada pelo CA1848.

```csharp
// Infinite.Agendamentos.Domain/Comercial/Common/Observabilidade/ComercialLogs.cs
namespace Infinite.Agendamentos.Domain.Comercial.Common.Observabilidade;

/// <summary>Logs do módulo Comercial. Bloco de EventIds: 10000–10999.</summary>
internal static partial class ComercialLogs
{
    [LoggerMessage(EventId = 10001, Level = LogLevel.Warning,
        Message = "Cancelamento recusado: agendamento {AgendamentoId} está em {StatusAtual}.")]
    public static partial void CancelamentoRecusado(this ILogger logger, long agendamentoId, StatusAgendamento statusAtual);
}
```

```csharp
// No handler: só a chamada.
using Infinite.Agendamentos.Domain.Comercial.Common.Observabilidade;

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
        // ...
        _logger.CancelamentoRecusado(agendamento.Id, agendamento.Status);
        // ...
    }
}
```

O gerador cria o corpo, verifica `IsEnabled` antes de formatar, liga cada placeholder ao
parâmetro de mesmo nome (compile error se faltar um) e usa o nome do método como `EventName`.
Nome do método = o fato, sem prefixo `Log` (`CancelamentoRecusado`, `FalhaAoConciliar`), como no
`MensagensLog` das libs.

### Regras de conteúdo

1. **Log é para depurar erro e fluxo inconsistente.** Se a linha não responde *"por que isso
   falhou/não aconteceu?"* ou *"quem mudou este estado?"*, ela não entra.
2. **Nunca logue o que a plataforma já loga** (tabela abaixo).
3. **Orçamento**: no máximo **1 a 3** logs por handler/service. A maioria dos handlers de CRUD fica
   com **zero** — e sem `ILogger` injetado.
4. **Nunca logue dado sensível ou payload inteiro**: senha, token, `Authorization`, CPF/CNPJ,
   e-mail, telefone, endereço, dados de cartão, corpo do request, entidade completa. Logue **ids**.
5. **Não relogue exceção que vai subir.** Se o `catch` faz rethrow (ou não existe), quem loga é o
   `ErrorHandlingBehavior` (EventId 4000). Logar e relançar duplica a falha no OpenObserve.
6. **Mensagem em português, impessoal, terminando em ponto**, com o desfecho explícito — sem
   "Erro:", sem "ATENÇÃO", sem emoji.
7. **Mesmo nome de placeholder para o mesmo dado** em toda a API: quem busca `agendamento_id=123`
   no OpenObserve precisa achar todos os pontos do fluxo.
8. **Não invente identificador**: use os ids que já existem no escopo.
9. **O código existente vence este documento** em nomes e mensagens; o padrão obrigatório acima não
   é opcional.

## Imposição no build

Todo serviço tem `src/.editorconfig` (template em `templates/editorconfig`) com:

```ini
[*.cs]
# LoggerMessage em vez de Log*/BeginScope com template
dotnet_diagnostic.CA1848.severity = error
# template de log constante
dotnet_diagnostic.CA2254.severity = error
# placeholders em PascalCase
dotnet_diagnostic.CA1727.severity = error
```

Em serviço existente sem esse arquivo: converta primeiro as chamadas `Log*` do serviço inteiro para
métodos nas classes `{{Modulo}}Logs` (o orçamento de logs as mantém poucas) e só então adicione o `.editorconfig`,
para o build não quebrar no meio do caminho. Se a tarefa for pontual, converta os arquivos tocados
e informe quantas chamadas antigas restam.

## O que a plataforma já loga — não duplicar

| Já coberto | Por quem |
|---|---|
| Requisição recebida: rota, método, status, duração | Instrumentação AspNetCore (métrica + trace) |
| Exceção não tratada no handler, com as `InnerException` | `ErrorHandlingBehavior` → EventId **4000** `Error`; a resposta vira 500 |
| Exceção que escapa do pipeline MediatR (com `TraceId`) | `InfiniteApiController.Send` → EventId **3001** `Error` |
| Request cancelada pelo cliente (499) | `Send` → EventId **3000** `Information` |
| Erro de validação do FluentValidation (422) | `Send` devolve o corpo; **não é log** |
| Query, `SaveChanges`, conexão de banco | Trace do EF Core / Npgsql |
| Chamada HTTP de saída: URL, status, duração | Trace do HttpClient |
| Startup sem autenticação / sem observabilidade | WebHost → EventIds 1000 / 2000 `Warning` |
| Token recusado por organização não permitida | WebHost → EventId 1002 `Warning` |
| Falha de exportação da telemetria | WebHost → EventId 2002 `Error` |
| CPU, memória, GC, threadpool, RPS, latência, taxa de erro | Métricas |

Corolário: **"entrando no handler X" / "saindo do handler X" nunca entra** — o trace já mostra o
span, com duração, e o log dentro dele já carrega o `trace_id`.

## Onde logar

| Gatilho no código | Nível | O que a mensagem precisa ter |
|---|---|---|
| `response.NotFound(...)` por **estado** (registro não existe, referência quebrada) | `Warning` | entidade + id procurado |
| `response.Invalid(...)` / `Forbidden(...)` por **regra de negócio** | `Warning` | id + a condição que barrou |
| `response.Error(...)` sem exceção (falha construída na mão) | `Error` | id + causa |
| `catch` que **engole** a exceção ou a converte em `response.Error` | `Error` | exceção + id da operação |
| `catch` de fallback (segue o fluxo degradado) | `Warning` | exceção + o que assumiu no lugar |
| Integração externa respondeu erro, timeout ou retorno inesperado | `Error` (quebra o fluxo) / `Warning` (tem fallback ou retry) | serviço + operação + status/código, **nunca o payload** |
| Retry, circuit breaker aberto, degradação | `Warning` | serviço + tentativa/estado |
| Mudança de estado irreversível ou de auditoria (cancelar, aprovar, estornar, deletar, publicar evento, enviar mensagem) | `Information` | ação + id |
| Conflito de concorrência / idempotência | `Warning` | id + estado encontrado |
| `BackgroundService`, job, consumer: início e fim do ciclo | `Information` | ciclo + quantidade processada |
| `BackgroundService`, job, consumer: item que falhou no meio do lote | `Error` | exceção + id do item; o lote continua |
| Configuração opcional ausente com default assumido no startup | `Warning` | chave + valor assumido |

`Trace` e `Debug` **não são commitados**. `Critical` é das libs (API não sobe) — não use em código de
aplicação. Tabela de decisão completa em `references/niveis.md`.

## Onde NÃO logar

- Entrada/saída de handler, controller, método ou repositório.
- Sucesso de CRUD simples (`Adicionado com sucesso`) — o 200 já está na métrica e no trace.
- Antes/depois de `SaveChanges`, `AddAsync`, `ToListAsync` — o trace do EF cobre.
- Dentro de laço, por item, quando a coleção pode ser grande: logue o **agregado** no fim.
- Falha de validação de campo (FluentValidation) — é erro do cliente, já vira 422.
- `NotFound` de busca/filtro que legitimamente pode não achar nada.
- Contagem de coisas ("quantas vezes X aconteceu") — isso é **métrica**, não log.

## Como aplicar

1. **Mapeie o que já existe**:
   ```bash
   grep -rnE "\.Log(Trace|Debug|Information|Warning|Error|Critical)\(|\.Log\(LogLevel|Console\.WriteLine" --include=*.cs src/
   ```
   ```bash
   grep -rn "EventId =|Bloco de EventIds" --include=*.cs src/
   ```
2. **Ache os candidatos**:
   ```bash
   grep -rn "response\.NotFound\|response\.Invalid\|response\.Forbidden\|response\.Error" --include=*.cs src/
   ```
   ```bash
   grep -rn "catch (\|: BackgroundService\|IHostedService\|Refit\|HttpClient" --include=*.cs src/
   ```
3. **Filtre pela tabela "Onde logar"** e descarte o que cai em "Onde NÃO logar".
4. **Apresente a lista antes de editar** — arquivo, EventId, nível, método e mensagem —, e só aplique
   depois do aceite. Em revisão, liste também o que vai **sair** e o que vai ser **convertido**.
5. **Aplique**: métodos `[LoggerMessage]` na classe `{{Modulo}}Logs` do módulo (crie
   `{{Modulo}}/Common/Observabilidade/` a partir de `templates/ModuloLogs.cs` se não existir), `ILogger<T>`
   no construtor da classe que registra e só a chamada `_logger.{Fato}(...)` no ponto escolhido.
6. **Garanta o `src/.editorconfig`** (seção "Imposição no build").
7. **Compile**: `dotnet build src/Infinite.{{Servico}}.slnx` — sem CA1848/CA2254/CA1727 — e confira
   que nenhum EventId se repete (saída vazia):
   ```bash
   grep -rhoE "EventId = [0-9]+" --include=*.cs src/ | sort | uniq -d
   ```
8. **Feche pelo `references/checklist.md`.**

## Referências

- `references/niveis.md` — níveis segundo a documentação .NET, decisão, correlação/escopos e
  configuração de `Logging:LogLevel` no Consul.
- `references/exemplos.md` — antes/depois em handler de command, query, `catch`, integração
  externa, `BackgroundService` e controller, todos em `[LoggerMessage]`.
- `references/checklist.md` — revisão de PR e critérios para **remover** log existente.
- `templates/editorconfig` — regras de analisador que tornam o padrão obrigatório.
- `templates/ModuloLogs.cs` — classe de logs do módulo (`{{Modulo}}/Common/Observabilidade/{{Modulo}}Logs.cs`).
