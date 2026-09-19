---
name: infinite-api-logs
description: "Adiciona e revisa logs estruturados de ILogger em APIs .NET com Infinite.Core.WebHost. Use para handlers, integrações e workers; não configura observabilidade nem altera regras de negócio."
---

# Logs de aplicação — APIs .NET da Infinite

A API já exporta logs, métricas e traces por OTLP: `AddInfiniteApiController` chama
`AdicionarInfiniteObservabilidade`, e todo `ILogger` cai no console **e** no OpenObserve com
`trace_id` correlacionado. **Nada precisa ser registrado, instalado ou configurado.** Este skill faz
uma coisa só: decidir **onde** falta log, em **qual nível**, com **qual mensagem** — e aplicar.

## Escopo

**Faz**: injeta `ILogger<T>`, adiciona chamadas `Log*` nos pontos certos, corrige nível e mensagem de
logs existentes, remove log ruidoso ou perigoso.

**Não faz**: seção `Observabilidade` do Consul, `Logging:LogLevel`, pacotes NuGet, `UseSerilog`,
middleware de log, dashboards, métricas, spans. Nada de `Console.WriteLine`, `Debug.WriteLine` ou
`Trace.Write`. Não refatora handler, não muda contrato, não altera regra de negócio, não troca
`Response<T>` por exceção para "ter o que logar".

## Regras invioláveis

1. **Log é para depurar erro e fluxo inconsistente.** Se a linha não responde *"por que isso
   falhou/não aconteceu?"* ou *"quem mudou este estado?"*, ela não entra.
2. **Nunca logue o que a plataforma já loga** (tabela abaixo). Log duplicado é ruído com custo de
   storage e atrapalha a busca no OpenObserve.
3. **Orçamento**: no máximo **1 a 3** chamadas de log por handler/service. A maioria dos handlers de
   CRUD fica com **zero**. Se um arquivo passou de 3, algo ali está sendo logado à toa.
4. **Sempre template estruturado** com placeholders nomeados: `logger.LogWarning("... {Id}.", id)`.
   Nunca interpolação (`$"..."`), nunca concatenação — o OpenObserve indexa o placeholder como campo.
5. **Nunca logue dado sensível ou payload inteiro**: senha, token, `Authorization`, CPF/CNPJ,
   e-mail, telefone, endereço, dados de cartão, corpo do request, entidade completa. Logue **ids**.
6. **Exceção vai como primeiro argumento**: `logger.LogError(excecao, "...")`. Nunca
   `LogError(excecao.Message)` — perde stack trace e tipo.
7. **Não relogue exceção que vai subir.** Se o `catch` faz rethrow (ou não existe), quem loga é o
   `Send` do `InfiniteApiController`. Logar e relançar gera a mesma falha duas vezes no OpenObserve.
8. **Mensagem em português, impessoal, terminando em ponto**, descrevendo o fato — sem "Erro:",
   sem "ATENÇÃO", sem emoji, sem `{0}`.
9. **Não invente identificador**: use os ids que já existem no escopo (`request.Id`, `entidade.Id`,
   `command.Codigo`). Se não há id, o log provavelmente não serve para depurar nada.
10. **O código existente vence este documento**: se o serviço já tem um padrão de log coerente,
    siga o dele.

## O que a plataforma já loga — não duplicar

| Já coberto | Por quem |
|---|---|
| Requisição recebida: rota, método, status, duração | Instrumentação AspNetCore (métrica + trace) |
| Exceção não tratada que sobe do handler (com `TraceId`) | `InfiniteApiController.Send` → `LogError` |
| Request cancelada pelo cliente (499) | `InfiniteApiController.Send` → `LogInformation` |
| Erro de validação do FluentValidation (422) | `Send` devolve o corpo; **não é log** |
| Query, `SaveChanges`, conexão de banco | Trace do EF Core / Npgsql |
| Chamada HTTP de saída: URL, status, duração | Trace do HttpClient |
| Startup sem autenticação / sem observabilidade | `ServicoInicializacao*` → `LogWarning` |
| Token recusado por organização não permitida | `MapeadorRolesZitadel` → `LogWarning` |
| Falha de exportação da telemetria | `DiagnosticoOpenTelemetry` → `LogError` |
| CPU, memória, GC, threadpool, RPS, latência, taxa de erro | Métricas |

Corolário: **"entrando no handler X" / "saindo do handler X" nunca entra** — o trace já mostra o
span, com duração, e o log dentro dele já carrega o `trace_id`.

## Onde logar

| Gatilho no código | Nível | O que a mensagem precisa ter |
|---|---|---|
| `response.NotFound(...)` por **estado** (registro não existe, referência quebrada) | `Warning` | entidade + id procurado |
| `response.Invalid(...)` / `Forbidden(...)` por **regra de negócio** (já cancelado, saldo insuficiente, fora da janela) | `Warning` | id + a condição que barrou |
| `response.Error(...)` sem exceção (falha construída na mão) | `Error` | id + causa |
| `catch` que **engole** a exceção ou a converte em `response.Error` | `Error` | exceção + id da operação |
| `catch` de fallback (segue o fluxo degradado) | `Warning` | exceção + o que assumiu no lugar |
| Integração externa respondeu erro, timeout ou retorno inesperado | `Error` (quebra o fluxo) / `Warning` (tem fallback ou retry) | serviço + operação + status/código, **nunca o payload** |
| Retry, circuit breaker aberto, degradação | `Warning` | serviço + tentativa/estado |
| Mudança de estado irreversível ou de auditoria (cancelar, aprovar, estornar, deletar, publicar evento, enviar mensagem) | `Information` | ação + id + id do usuário/origem |
| Conflito de concorrência / idempotência (já processado, versão desatualizada) | `Warning` | id + estado encontrado |
| `BackgroundService`, job, consumer: início e fim do ciclo | `Information` | ciclo + quantidade processada |
| `BackgroundService`, job, consumer: item que falhou no meio do lote | `Error` | exceção + id do item; o lote continua |
| Configuração opcional ausente com default assumido no startup | `Warning` | chave + valor assumido |

`Trace` e `Debug` **não são commitados**. Se precisar de um durante a investigação, remova antes de
fechar. `Critical` é da lib (API não sobe) — não use em código de aplicação.

## Onde NÃO logar

- Entrada/saída de handler, controller, método ou repositório.
- Sucesso de CRUD simples (`Adicionado com sucesso`) — o 200/201 já está na métrica e no trace.
- Antes/depois de `SaveChanges`, `AddAsync`, `ToListAsync` — o trace do EF cobre.
- Dentro de laço, por item, quando a coleção pode ser grande: logue o **agregado** no fim
  (`{Total} processados, {Falhas} falhas.`).
- Falha de validação de campo (FluentValidation) — é erro do cliente, já vira 422.
- `NotFound` de busca/filtro que legitimamente pode não achar nada (lista vazia é resposta normal).
- Contagem de coisas ("quantas vezes X aconteceu") — isso é **métrica**, não log.
- Reformular em log o que já vai no corpo da resposta ao cliente.

## Como escrever

```csharp
public sealed class CancelarAgendamentoCommandHandler
(
    IWriteRepository<Agendamento> repo,
    ILogger<CancelarAgendamentoCommandHandler> logger
) : IRequestHandler<CancelarAgendamentoCommand, Response<bool>>
{
    private readonly IWriteRepository<Agendamento> _repo = repo;
    private readonly ILogger<CancelarAgendamentoCommandHandler> _logger = logger;
```

- `ILogger<T>` entra pelo **primary constructor**, sempre como **último parâmetro**, com campo
  `private readonly` — igual ao resto do serviço.
- Placeholders em **PascalCase** e com nome de negócio: `{AgendamentoId}`, `{StatusAtual}`,
  `{TotalProcessados}` — não `{0}`, não `{id}`, não `{valor}`.
- **Mesmo nome de placeholder para o mesmo dado** em toda a API: quem busca `agendamento_id=123`
  no OpenObserve precisa achar todos os pontos do fluxo.
- Uma frase, um fato, com o desfecho explícito:
  `"Cancelamento recusado: agendamento {AgendamentoId} já está em {StatusAtual}."`

Detalhamento, exemplos antes/depois e checklist: `references/`.

## Como aplicar

1. **Mapeie o que já existe** antes de escrever qualquer linha:
   ```bash
   grep -rn "logger\.Log\|Console\.WriteLine" --include=*.cs src/
   ```
2. **Ache os candidatos** (não vá arquivo por arquivo):
   ```bash
   grep -rn "response\.NotFound\|response\.Invalid\|response\.Forbidden\|response\.Error" --include=*.cs src/
   ```
   ```bash
   grep -rn "catch (\|: BackgroundService\|IHostedService\|Refit\|HttpClient" --include=*.cs src/
   ```
3. **Filtre pela tabela "Onde logar"** e descarte tudo que cai em "Onde NÃO logar". Vale mais deixar
   um ponto sem log do que encher o OpenObserve.
4. **Apresente a lista antes de editar** — arquivo, linha, nível e mensagem proposta —, e só aplique
   depois do aceite. Em revisão de logs existentes, liste também o que vai **sair**.
5. **Aplique** injetando `ILogger<T>` onde faltar e inserindo as chamadas.
6. **Compile**: `dotnet build src/Infinite.{{Servico}}.slnx`.
7. **Feche pelo `references/checklist.md`.**

## Referências

- `references/niveis.md` — tabela de decisão de nível, severidade no OpenObserve, como se busca cada
  um e o que fazer quando o nível está em dúvida.
- `references/exemplos.md` — antes/depois em handler de command, handler de query, service de
  integração externa, `BackgroundService` e controller.
- `references/checklist.md` — revisão de PR e critérios para **remover** log existente.
