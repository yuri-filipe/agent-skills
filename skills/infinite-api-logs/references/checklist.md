# Checklist

## Antes de fechar a alteração

- [ ] Toda chamada de log responde "por que falhou?", "por que foi barrado?" ou "quem mudou este
      estado?". Nenhuma é narração de progresso.
- [ ] Nenhum ponto da tabela "O que a plataforma já loga" foi duplicado.
- [ ] Nenhum arquivo passou de **3** chamadas de log; handlers de CRUD simples ficaram com **zero**.
- [ ] Todo log é um método `[LoggerMessage]` na classe `{{Modulo}}/Common/Logs/{{Modulo}}Logs.cs`;
      handlers/services/workers só chamam `_logger.{Fato}(...)` — nenhum `[LoggerMessage]`,
      `Log*` ou `Log(LogLevel, ...)` no código de negócio.
- [ ] Cada método tem `EventId` novo, no bloco do módulo declarado no cabeçalho da classe de logs,
      sem reaproveitar nem renumerar; `grep -rhoE "EventId = [0-9]+" --include=*.cs src/ | sort | uniq -d`
      sai vazio.
- [ ] Modelo de mensagem constante com placeholder nomeado — nenhuma interpolação `$"..."`,
      concatenação ou `{0}`.
- [ ] Placeholders em PascalCase, e o **mesmo dado usa o mesmo nome** em todos os arquivos tocados.
- [ ] Exceção entra como parâmetro `Exception` do método, nunca como `excecao.Message` no template.
- [ ] Nenhum argumento caro calculado sem `IsEnabled`; nenhum `await`/I/O para montar log.
- [ ] Nenhum `catch` loga e faz `throw;` (duplicaria o log do `ErrorHandlingBehavior`).
- [ ] Nenhum log contém senha, token, header `Authorization`, CPF/CNPJ, e-mail, telefone, endereço,
      dado de cartão, corpo de request ou entidade serializada.
- [ ] Nenhum `LogTrace`/`LogDebug` sobrou; nenhum `Console.WriteLine`/`Debug.WriteLine` foi
      introduzido.
- [ ] `ILogger<T>` entra pelo primary constructor, como último parâmetro, com campo
      `private readonly` — e o `T` é a própria classe.
- [ ] Log por item dentro de laço só em falha; sucesso vira agregado.
- [ ] `BackgroundService`/consumer abre `BeginScope` com dicionário e o identificador do ciclo.
- [ ] `src/.editorconfig` com CA1848, CA2254 e CA1727 como `error` (ou, em migração parcial, a
      contagem de chamadas antigas restantes informada).
- [ ] Nada fora do escopo foi tocado: sem mudança de contrato, de regra de negócio, de DI, de Consul
      ou de pacote.
- [ ] `dotnet build src/Infinite.{{Servico}}.slnx` passa sem CA1848/CA2254/CA1727.

## Quando o pedido é "revisar os logs" — o que sai

Remova (não rebaixe o nível) toda chamada que se encaixe em:

| Sintoma | Ação |
|---|---|
| `"Iniciando ..."` / `"Finalizando ..."` de método ou handler | remover |
| Log imediatamente antes ou depois de `SaveChanges`/`AddAsync`/`ToListAsync` | remover |
| `"... com sucesso"` em CRUD simples | remover |
| Log de erro de validação de campo | remover |
| Log dentro de laço, por item, no caminho de sucesso | trocar pelo agregado |
| `LogError` seguido de `throw;` | remover o log, manter o `throw` |
| Mesma falha logada no service **e** no handler que o chama | manter só a de baixo (mais próxima da causa) |
| Log com objeto inteiro ou dado pessoal | reescrever com ids |
| `LogDebug`/`LogTrace` commitado | remover |
| Chamada `Log*` direta que continua necessária | converter para `[LoggerMessage]` com EventId novo |
| `Error` em situação que é comportamento correto (404 de negócio, 403, retry) | rebaixar para `Warning` |
| `Information` em falha que derrubou a operação | subir para `Error` |

Antes de aplicar remoções, apresente a lista completa (arquivo, linha, motivo) e espere o aceite.

## Depois de aplicar

Vale conferir no OpenObserve, no stream de logs do serviço:

1. `severity_text` do novo log aparece com o nível esperado.
2. Os placeholders viraram **campos** (`agendamento_id`, `status_atual`) — o OpenObserve troca ponto
   e maiúscula por underscore/minúscula.
3. Buscar pelo id de negócio traz todos os pontos do fluxo.
4. O log aparece com `trace_id` preenchido (ou `CicloId`, em job).
5. O EventId aparece no registro e filtra só este ponto do código.
