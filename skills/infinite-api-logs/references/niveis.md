# Escolha do nível

O nível é o **único filtro barato** na busca do OpenObserve (`severity_text`). Errar o nível é pior
do que não logar: um `Error` que na verdade é comportamento normal treina o time a ignorar `Error`.

| Nível | `severity_number` | Prefixo no console | Pergunta que ele responde | Quem olha |
|---|---|---|---|---|
| `Trace` | 1 | `trce:` | — | ninguém; proibido em `prd` |
| `Debug` | 5 | `dbug:` | — | só durante investigação local |
| `Information` | 9 | `info:` | "este estado mudou, e por ordem de quem?" | auditoria, reconstrução de fluxo |
| `Warning` | 13 | `warn:` | "por que o cliente recebeu 400/403/404?" | quem investiga um chamado |
| `Error` | 17 | `fail:` | "o que quebrou?" | alerta / plantão |
| `Critical` | 21 | `crit:` | "a API não sobe" | só a lib |

## Regra de decisão

Faça as perguntas nesta ordem e pare na primeira que der "sim":

1. **A API não consegue operar?** → `Critical`. Em código de aplicação, a resposta é sempre não.
2. **Uma operação falhou por culpa da API, de um bug ou de um sistema externo?** → `Error`.
   Tem exceção envolvida? Passe-a como primeiro argumento.
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

## Nível mínimo em produção

O piso é o padrão do ASP.NET Core (`Information`), ajustável por `Logging:LogLevel` no `config.json`
do Consul. O que não passa do filtro não vai para o console **nem** para o OpenObserve — por isso
`Debug` no código é código morto em `prd`, e não "log desligado que dá para ligar depois".
