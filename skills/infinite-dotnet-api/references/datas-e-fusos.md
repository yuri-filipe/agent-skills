# Datas, fusos e contrato API/frontend

## Responsabilidades

- PostgreSQL/Infinite.Core.Postgres: persistir instantes e valores civis com NodaTime.
- API: validar a entrada, resolver o fuso relevante ao negócio e transportar instantes UTC.
- Frontend: apresentar no fuso escolhido e acompanhar de fuso os horários civis que virarão eventos.

Este é o padrão para novos serviços e alterações temporais autorizadas. Em um serviço existente,
inspecione versões, contratos e consumidores antes de trocar tipos públicos; não migre toda a API
como efeito colateral de um CRUD sem relação com datas. Confirme o contrato do pacote efetivamente
instalado: a alteração local da lib não comprova que uma nova versão foi publicada no feed.

## Tipos e persistência

| Significado | Tipo | Coluna |
|---|---|---|
| Quando aconteceu | `Instant` | `timestamptz` |
| Dia civil/nascimento | `LocalDate` | `date` |
| Hora civil/funcionamento | `LocalTime` | `time without time zone` |
| Data/hora civil ainda sem instante | `LocalDateTime` | `timestamp without time zone` |

Variantes nullable usam os mesmos mapeamentos. `timestamptz` não guarda o fuso original;
quando o negócio precisa dele, persista `TimeZoneId` IANA em outra propriedade da entidade.
Use o plugin nativo do Npgsql, sem conversores EF paralelos para os mesmos tipos. Runtime e
design-time devem passar pelo configurador compartilhado da lib.

Na versão com NodaTime, `CoreEntity` tem `Id`, `Excluido`, `UsuarioInclusao`, `UsuarioAlteracao`,
`DataInclusao` e `DataAlteracao`; as duas datas são `Instant?`. O contexto gera a auditoria,
com um instante por salvamento. DTOs devem refletir esses tipos quando o contrato permitir.
`AutoProject` copia tipos compatíveis; não converte `Instant` para `DateTime` automaticamente.
Configure conversões necessárias explicitamente no Mapperly e teste o contrato resultante.

`TimeZoneBrasilia` foi removido da lib. Não use nem recrie esse helper na persistência.
Tipos .NET legados continuam aceitos: para `timestamptz`, `DateTime.Kind` deve ser `Utc` e
`DateTimeOffset.Offset` deve ser zero. A API não deve inferir UTC de uma entrada sem fuso.

## JSON na API

Referencie `NodaTime.Serialization.SystemTextJson` no projeto Web API, com versão estável
compatível com NodaTime. Depois de `AddInfiniteApiController`, registre:

```csharp
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
    options.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
```

Para Minimal APIs, configure `ConfigureHttpJsonOptions`/`SerializerOptions` correspondentes.
Não adicione registro JSON à lib Postgres, nem presuma que opções MVC configuram Minimal APIs.

| Tipo | Exemplo JSON |
|---|---|
| `Instant` | `"2026-09-20T17:00:00Z"` |
| `LocalDate` | `"2026-09-20"` |
| `LocalTime` | `"14:00:00"` |
| `LocalDateTime` | `"2026-09-20T14:00:00"` |

Não use formato localizado como contrato HTTP. Configure o OpenAPI para refletir strings e
formatos reais, sem expor as propriedades internas das structs NodaTime. `Instant` usa `date-time`,
`LocalDate` usa `date`; para tipos civis de hora/data-hora mantenha exemplos sem offset e não
prometa um instante RFC 3339. Confira o documento gerado pela versão do gerador instalada.

## Entrada civil e conversão

Para um formulário de evento, o contrato pode ser:

```json
{ "date": "2026-09-20", "time": "14:00:00", "timeZone": "America/Sao_Paulo" }
```

Use `LocalDate`, `LocalTime` e `string` no command, preservando os nomes e a estrutura do
contrato existente. Valide fuso não vazio e reconhecido por
`DateTimeZoneProviders.Tzdb.GetZoneOrNull` no validator FluentValidation. Quando o negócio
exigir rejeitar horários ambíguos/inexistentes, valide também o mapeamento civil no fuso e
retorne o erro pelo fluxo existente. Não transforme entrada inválida em exceção não tratada.

Depois da validação, a conversão na camada de aplicação segue a política escolhida:

```csharp
var zone = DateTimeZoneProviders.Tzdb[request.TimeZone];
var local = request.Date.At(request.Time);
var instant = local.InZoneLeniently(zone).ToInstant();
// 2026-09-20 14:00 em America/Sao_Paulo -> 2026-09-20T17:00:00Z
```

`InZoneLeniently` escolhe a primeira ocorrência em uma sobreposição e desloca pela duração
da lacuna quando o horário não existe. Esse exemplo não impõe uma política universal:
`InZoneStrictly` rejeita ambos os casos. Não use o fuso da máquina como substituto.
Mantenha o mapeamento estrutural no Mapperly; a resolução de fuso é regra da aplicação.

Para filtros por dia civil, receba `LocalDate` e o fuso relevante e calcule os limites UTC
de cada dia nesse fuso. Prefira início inclusivo/fim exclusivo; não some sempre 24 horas
para obter o dia seguinte, nem invente um fim em 23:59:59.999.

## Compatibilidade e verificação

- Alinhe Microsoft.EntityFrameworkCore.* na mesma versão estável 10.0.x e Npgsql/provider/plugin
  na linha compatível; consulte o grafo real, incluindo Design e testes.
- A troca isolada `DateTime?` -> `Instant?` em colunas já `timestamptz` não exige regravar
  instantes. Gere/revise migration e snapshot do consumidor. Colunas antigas sem fuso exigem
  conhecer a semântica dos dados antes de qualquer conversão.
- Valide JSON, nullable, auditoria, projeções e modelos runtime/design-time. Distinga testes
  em memória/metadados de gravação/leitura real; respeite o escopo de testes pedido na tarefa.
- Verifique com o frontend o mesmo instante em fusos diferentes, eventos no fuso do local,
  data de nascimento inalterada e entrada com fuso explícito.

Fontes: [Npgsql/NodaTime](https://www.npgsql.org/efcore/mapping/nodatime.html),
[serialização NodaTime](https://nodatime.org/3.3.x/userguide/serialization),
[conversões de fuso](https://nodatime.org/3.3.x/userguide/zoneddatetime).
