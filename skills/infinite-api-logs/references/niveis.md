# Escolha do nível

O nível é o **único filtro barato** na busca do OpenObserve (`severity_text`). Errar o nível é pior
do que não logar: um `Error` que na verdade é comportamento normal treina o time a ignorar `Error`.

Definições da [documentação .NET](https://learn.microsoft.com/pt-br/dotnet/core/extensions/logging/overview),
com a leitura aplicada às APIs Infinite:

| Nível | Valor | Documentação .NET | Nas APIs Infinite | `severity_number` | Console |
|---|---|---|---|---|---|
| `Trace` | 0 | Mais detalhado; pode ter dado confidencial; desabilitado por padrão e **nunca** em produção | proibido em código commitado | 1 | `trce:` |
| `Debug` | 1 | Depuração e desenvolvimento; volume alto | só durante investigação local, não commitado | 5 | `dbug:` |
| `Information` | 2 | Fluxo geral do aplicativo, valor de longo prazo | mudança de estado que alguém vai reconstruir; ciclo de job | 9 | `info:` |
| `Warning` | 3 | Evento anormal ou inesperado que não derruba o aplicativo | operação barrada com comportamento correto; fallback; retry | 13 | `warn:` |
| `Error` | 4 | Erro/exceção não tratável; falha **da operação atual**, não do aplicativo | operação falhou por bug, dado ou sistema externo | 17 | `fail:` |
| `Critical` | 5 | Falha que exige atenção imediata (perda de dados, disco cheio) | só as libs (API não sobe) | 21 | `crit:` |

Em produção, `Warning` a `Critical` devem gerar **poucas** mensagens: se um `Warning` dispara a
cada requisição normal, ele está no nível errado.

## Regra de decisão

Faça as perguntas nesta ordem e pare na primeira que der "sim":

1. **A API não consegue operar?** → `Critical`. Em código de aplicação, a resposta é sempre não.
2. **Uma operação falhou por culpa da API, de um bug ou de um sistema externo?** → `Error`.
   Tem exceção envolvida? Declare-a como parâmetro `Exception` do método `[LoggerMessage]`.
3. **A operação foi barrada, mas o comportamento está correto?** (regra de negócio, registro
   inexistente, permissão, conflito, fallback acionado) → `Warning`.
4. **O fluxo foi normal, mas mudou estado que alguém vai precisar reconstruir depois?**
   (cancelou, aprovou, estornou, deletou, publicou evento, disparou notificação) → `Information`.
5. **Nenhuma das anteriores** → não logue.

## Confusões comuns

| Situação | Nível certo | Por quê |
|---|---|---|
| Registro não encontrado numa **busca/listagem** | não logar | Lista vazia é resposta legítima. |
| Registro não encontrado num **update/delete por id** | `Warning` | O cliente acha que existe; alguém vai investigar. |
| Campo obrigatório ausente (FluentValidation) | não logar | Erro do cliente, já vira 422 com o detalhe no corpo. |
| Token expirado / 401 | não logar | Fluxo normal de expiração; o middleware já responde. |
| Usuário sem role para a ação (403) | `Warning` | Pode ser configuração errada de role no Zitadel. |
| Serviço externo respondeu 500 e o fluxo parou | `Error` | Uma operação falhou. |
| Serviço externo respondeu 500 e caiu no cache/fallback | `Warning` | A API entregou resposta; ainda assim há degradação a acompanhar. |
| Timeout de integração | `Error` se derruba a operação, `Warning` se há retry | |
| Retry que **funcionou** na segunda tentativa | `Warning` (uma linha, no retry) | Sem ela, a lentidão fica sem explicação. |
| Deadlock / concorrência otimista com reprocesso | `Warning` | Esperado sob carga; vira `Error` se desiste. |
| Mensagem/evento consumido em duplicidade e descartado por idempotência | `Warning` | É o rastro de que a duplicidade existe. |
| Job noturno terminou processando 0 itens | `Information` | Fim de ciclo com o total; 0 pode ser normal. |
| Job noturno não rodou por falta de configuração | `Error` | Ninguém percebe silêncio. |

## Correlação

Dentro de uma requisição HTTP não é preciso fazer nada: o log herda `trace_id` e `span_id` da
`Activity` corrente, e o OpenObserve liga log e trace sozinho. Basta incluir os **ids de negócio**
como placeholders.

Fora de uma requisição (`BackgroundService`, consumer, job) não há `Activity` — cada log fica solto.
Abra um escopo por ciclo/item para que todos os logs do bloco carreguem o mesmo identificador:

```csharp
using (_logger.BeginScope(new Dictionary<string, object> { ["CicloId"] = cicloId }))
{
    // logs deste ciclo saem com CicloId
}
```

`IncludeScopes` já está ligado no pipeline da lib, então o escopo chega ao OpenObserve como campo.

## Configuração de `Logging:LogLevel`

A API não lê `appsettings.json` em runtime (o `AddConsulConfig` descarta essa fonte). O filtro
vive no `config.json` do Consul, com a mesma forma da documentação .NET:

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft": "Warning",
    "Microsoft.Hosting.Lifetime": "Information"
  }
}
```

- `Default` vale para toda categoria sem regra própria; a regra de **prefixo mais longo** vence
  (`Microsoft.Hosting.Lifetime` sobre `Microsoft`).
- A categoria é o nome completo da classe (`ILogger<T>`). Para investigar, suba o nível **só da
  categoria** em análise, por exemplo `"Infinite.Agendamentos.Domain.Comercial": "Debug"` em `qa` —
  nunca `Default` para `Debug`, nunca `Trace` em `prd`.
- Abaixo do mínimo, o log não vai para o console **nem** para o OpenObserve: `Debug` no código é
  código morto em `prd`.
- A configuração do Consul é lida no startup e não recarrega: mudar o nível exige reiniciar a API.
- Mudança de nível é configuração do ambiente; só altere quando a tarefa pedir.
