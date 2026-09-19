---
name: infinite-frontend-datetime
description: "Aplica o contrato de datas e fusos dos frontends Infinite com APIs NodaTime. Use para DTOs, formulários, filtros e exibição de datas em Vue/Angular; não cobre build."
---

# Datas e fusos no frontend Infinite

O backend persiste instantes; a API transporta ISO-8601 UTC; o frontend apresenta no fuso
relevante. Aplique estas regras aos campos envolvidos na tarefa. Inspecione o contrato real
antes de alterar nomes ou formatos e preserve os demais fluxos da aplicação.

## Identificar o significado do campo

| Significado | Contrato HTTP | Tratamento no front |
|---|---|---|
| Auditoria, criação, alteração, ocorrência (`Instant`) | String UTC com `Z` | Formatar no fuso escolhido |
| Evento ligado a um local | Instante UTC e identificador IANA do evento | Usar o fuso do evento quando essa for a regra do negócio |
| Nascimento/dia civil (`LocalDate`) | `YYYY-MM-DD` | Manter o dia; não converter para instante |
| Hora civil (`LocalTime`) | `HH:mm:ss`, com frações se necessárias | Manter a hora; não adicionar data/fuso artificial |
| Data/hora civil (`LocalDateTime`) | ISO sem offset | Não tratar como instante até conhecer o fuso |

Modele valores JSON como `string`/`string | null` nos DTOs TypeScript, conforme o contrato.
NodaTime é uma decisão do backend; não requer uma biblioteca equivalente no frontend.
Mantenha os valores recebidos no estado/DTO e derive a apresentação. Não converta globalmente
toda string de data para `Date` em interceptors: isso altera datas civis, perde precisão de
microssegundos e pode modificar valores em um salvamento sem edição.

## Exibição de instantes

Para campos no horário de quem está olhando, a ordem padrão é preferência válida do perfil,
fuso do navegador e UTC. Para um evento vinculado a um estabelecimento, respeite o fuso do
evento mesmo que o usuário esteja viajando. Não crie preferência de perfil nem coluna de fuso
na API sem isso fazer parte da tarefa; consuma o que o contrato já fornece.

```typescript
const timeZone = user.timeZone
  ?? Intl.DateTimeFormat().resolvedOptions().timeZone
  ?? 'UTC'

const display = new Intl.DateTimeFormat('pt-BR', {
  timeZone,
  dateStyle: 'short',
  timeStyle: 'short',
}).format(new Date(response.scheduledAt))
```

O exemplo assume instante e fusos válidos. Trate null/vazio/data inválida segundo o padrão
de UI existente. Valide identificadores de fuso antes de formatar; um identificador inválido
pode causar `RangeError`. Em SSR, resolva o fuso do navegador no cliente; não use o fuso do
servidor como se fosse do usuário.

Use a locale definida no produto (`pt-BR` no exemplo) e o fuso explicitamente. Não subtraia
três horas, não use offset fixo para representar `America/Sao_Paulo` e não converta duas vezes.
`new Date(isoUtc)` serve para formatar um instante válido; evite reserializá-lo apenas para exibir.

Para `LocalDate`, preserve os componentes da string; `new Date('2026-09-20')` seguido de
formatação local pode mostrar o dia anterior. Para tipos civis, não acrescente `Z` e não chame
`toISOString()` para fabricar um instante. Reutilize componentes e utilitários existentes,
ajustando sua configuração temporal em vez de criar outra camada de datas sem necessidade.

## Formulários e envio à API

`datetime-local` representa data/hora civil e não contém timezone. Quando o formulário cria
um evento, envie data/hora e o fuso IANA escolhido pelo negócio/usuário, usando o contrato da API:

```json
{
  "date": "2026-09-20",
  "time": "14:00:00",
  "timeZone": "America/Sao_Paulo"
}
```

A API resolve `LocalDate + LocalTime + TimeZoneId` para `Instant`. Não use
`new Date(valorDoDatetimeLocal).toISOString()` quando o fuso escolhido pode diferir do navegador;
isso interpreta silenciosamente o valor no fuso da máquina. Não mande `20/09/2026 14:00` como
contrato técnico nem anexe `Z` ao valor civil. Se a API existente recebe somente um instante,
respeite esse contrato e use a conversão com fuso explícito já adotada pelo projeto; não altere
o backend como efeito colateral de uma tarefa de UI.

Na edição, preencha o campo civil a partir do instante no fuso correspondente; não use
`isoUtc.slice(0, 16)` para preencher um controle que representa hora local. Preserve o instante
original ao salvar sem alteração, inclusive suas frações. Datas de nascimento e horas de
funcionamento permanecem civis; não recebem uma conversão UTC automática.

Em horários repetidos ou inexistentes por transições, apresente a validação retornada pela API.
A política estrita ou leniente pertence à aplicação; não implemente uma resolução diferente
no navegador. Filtros por dia mantêm o dia e o fuso relevantes, conforme o contrato,
em vez de derivar limites no fuso da máquina ou somar sempre 24 horas.

## Verificar o comportamento alterado

- Mesmo instante exibido em `America/Sao_Paulo` e em outro fuso, sem alterar o valor enviado.
- Evento mantendo o horário do local ao ser visto por usuário em outro fuso.
- Data civil perto da virada do dia permanecendo no mesmo dia; null tratado sem Invalid Date.
- Formulário com fuso escolhido diferente do navegador e edição sem conversão dupla.
- Salvamento sem edição preservando o valor original e sua precisão.

Use checks de build/tipos e testes apropriados ao projeto. Respeite pedidos para não iniciar
servidores ou ambientes e relate quais verificações foram efetivamente executadas.

Referências: [Intl.DateTimeFormat](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl/DateTimeFormat),
[datetime-local](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/datetime-local).
