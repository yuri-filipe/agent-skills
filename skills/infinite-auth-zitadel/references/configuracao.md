# Seção `Autenticacao` e a API de código da lib

## Registro (não mexer)

`Program.cs` e `Startup.cs` não mudam:

```csharp
// Startup.cs
services.AddInfiniteApiController(_config, "Infinite Optics API");   // já chama AddInfiniteAuth

// Program.cs
app.UseInfiniteApi("Infinite Optics API");                           // já insere UseAuthentication()
```

Sem o pacote de API completo:

```csharp
services.AddInfiniteAuth(configuration);   // retorna a ConfiguracaoAutenticacao validada
```

Se a API já chama `app.UseAuthentication()` por conta própria, a lib detecta e não registra o
middleware duas vezes.

## Chaves da seção `Autenticacao`

Vive em `{configPath}/{prd|qa}/config.json` no Consul.

| Chave | Obrigatória | Default | Descrição |
|---|---|---|---|
| `Habilitada` | sim | `false` | Liga/desliga **toda** a autenticação da API. |
| `Autoridade` | quando `Habilitada` | — | URL da instância do Zitadel, sem barra final. Barra final e espaços são aparados. |
| `IdProjeto` | quando `Habilitada` | — | **Project ID desta API.** Define de qual projeto as roles do token valem. |
| `Audiencias` | quando `Habilitada` | — | Audiences aceitas: **Project ID** e/ou Client Id da API. |
| `ExigirHttpsNoMetadata` | não | `true` | Desligue só em ambiente local com Zitadel em HTTP. |
| `ToleranciaRelogioSegundos` | não | `60` | Tolerância de relógio na expiração. Não pode ser negativa. |
| `ClaimDeNome` | não | `preferred_username` | Claim usada como nome do usuário. |
| `AlgoritmosAceitos` | não | `["RS256"]` | Algoritmos de assinatura aceitos. Não pode ficar vazio. |
| `AceitarClaimLegadaDeRoles` | não | `false` | Aceita `urn:zitadel:iam:org:project:roles`, que não identifica o projeto de origem. |
| `OrganizacoesPermitidas` | não | — | Se preenchida, só aceita tokens dessas organizações. |
| `ExigirAutenticacaoPorPadrao` | não | `false` | Exige token em todas as rotas sem `[AllowAnonymous]`. |
| `ValidarAutoridadeNoStartup` | não | `false` | Baixa o discovery no startup e falha se a Autoridade não responder. |
| `DetalharErrosDeToken` | não | `false` | Devolve o motivo técnico da recusa no corpo do 401. **Só em dev.** |
| `ExporSwagger` | não | `null` | Força publicar (`true`) ou esconder (`false`) o Swagger. Ver abaixo. |
| `PoliticasPorRole` | não | — | nome da política → roles aceitas (**ou** entre elas). |
| `PoliticasPorEscopo` | não | — | nome da política → escopos aceitos (**ou** entre eles). |
| `ClientIdSwagger` | não | — | Habilita o botão **Authorize** do Swagger com Authorization Code + PKCE. |
| `EscoposSwagger` | não | `["openid","profile","email"]` | Escopos pedidos pelo Swagger. Obrigatório e não-vazio se `ClientIdSwagger` estiver preenchido. |

Interpolação de chaves compartilhadas do Consul (suportada pela `Infinite.Core.Consul`):

```json
"Autoridade": "https://{{ zitadel_domain }}",
"IdProjeto": "{{ zitadel_project_id }}",
"Audiencias": [ "{{ zitadel_project_id }}" ]
```

## Exposição do Swagger

O Swagger roda como **middleware**, antes da autorização: se publicado com a API protegida, todo o
contrato (rotas, DTOs, campos) fica legível **sem token**.

| `ExporSwagger` | Comportamento |
|---|---|
| ausente (`null`) | **Padrão.** Publica sempre que a autenticação está desligada; com ela ligada, só em `Development`. |
| `true` | Publica sempre. Use em `qa`, onde o botão **Authorize** é útil. |
| `false` | Nunca publica. |

Se a API tem `ClientIdSwagger` configurado e o Swagger "sumiu" em qa, é esta chave.

## Ligar e desligar

- **`"Habilitada": false`** (ou seção ausente): nenhum esquema é registrado, `UseAuthentication()`
  não entra no pipeline, a API se comporta como antes. Sobe um **warning** no log.
- **`"Habilitada": true`**: a API **não sobe** se a seção estiver incompleta. A mensagem diz o que
  falta — é validação de startup intencional, não contorne:
  - `Autenticacao:Autoridade não configurada.`
  - `Autenticacao:Autoridade não é uma URL absoluta válida.`
  - `Autenticacao:Autoridade deve usar HTTPS quando ExigirHttpsNoMetadata está habilitado.`
  - `Autenticacao:IdProjeto não configurado. Sem ele, roles de outros projetos do Zitadel seriam aceitas nesta API.`
  - `Autenticacao:Audiencias não configuradas.` / `... está vazio.`
  - `Autenticacao:AlgoritmosAceitos está vazio.`
  - `Autenticacao:ToleranciaRelogioSegundos não pode ser negativa.` / `Autenticacao:ClaimDeNome não pode ser vazia.`
  - `Autenticacao:OrganizacoesPermitidas está vazio.`
  - `Autenticacao:PoliticasPorRole contém uma política sem nome.`
  - `Autenticacao:PoliticasPorRole a política 'Admin' está sem valores.`
  - `Autenticacao: a política 'X' está declarada em PoliticasPorRole e PoliticasPorEscopo.`
  - `Autenticacao:EscoposSwagger é obrigatório quando ClientIdSwagger está configurado.`

## Como a lib trata as claims

- `MapInboundClaims = false` — os nomes originais são preservados (`sub`, `scope`,
  `urn:zitadel:...`). **Não** use `ClaimTypes.NameIdentifier` direto; use `User.ObterIdUsuario()`.
- **Só as roles do projeto em `IdProjeto`** são achatadas em `ClaimTypes.Role` (fazendo
  `[Authorize(Roles = "...")]` funcionar) e em `urn:zitadel:iam:org:{idOrg}:project:roles`. Roles de
  outros projetos dos quais o token também é audiência **não valem** nesta API.
- A claim legada `urn:zitadel:iam:org:project:roles` não diz de qual projeto veio, e por isso é
  ignorada salvo `AceitarClaimLegadaDeRoles: true`.
- Qualquer claim `ClaimTypes.Role` que venha **dentro do token** é descartada antes do mapeamento:
  role só é concedida por autorização de projeto do Zitadel.
- 401 e 403 são respondidos pelo pipeline em `application/problem+json`
  (`{ status, title, detail }`), antes do controller. O `detail` do 401 é genérico salvo
  `DetalharErrosDeToken`.
- Token de organização não permitida é recusado com **401** e o motivo vai só para o log
  (`warning`), para não confirmar ao chamador quais organizações existem.

## `UsuarioAutenticadoExtensions`

`using Infinite.Core.WebHost.Extensoes.Autenticacao;`

| Método | Retorno |
|---|---|
| `User.ObterIdUsuario()` | claim `sub` |
| `User.ObterEmail()` | claim `email` |
| `User.ObterIdOrganizacao()` | `urn:zitadel:iam:org:id` (tenant) |
| `User.ObterDominioPrimario()` | `urn:zitadel:iam:org:domain:primary` |
| `User.ObterEscopos()` | `IReadOnlyCollection<string>` da claim `scope` |
| `User.PossuiEscopo("api:read")` | `bool` |
| `User.PossuiAlgumaRole("admin", "gestor")` | `bool` — ao menos uma |
| `User.PossuiRoleNaOrganizacao(idOrg, "admin")` | `bool` — role no contexto da organização |
| `User.PossuiRoleNaOrganizacao(idOrg, "admin", "gestor")` | `bool` — ao menos uma, na organização |

## `IAuditContextAccessor` (auditoria)

`AddInfiniteAuth` registra `IHttpContextAccessor` e `IAuditContextAccessor` (`Infinite.Core.Auditing`)
como singleton, **mesmo com `Habilitada: false`**. `GetCurrent()` lê a requisição no momento da
chamada e devolve `AuditContext` (`IsAuthenticated`, `UserId` = `sub`, `UserName` = `ClaimDeNome`,
`Email`, `OrganizationId` = `urn:zitadel:iam:org:id`) ou `AuditContext.Empty` sem identidade.
O `InfiniteContext` da `Infinite.Core.Postgres` usa esse contrato para gravar usuário e
organização na auditoria de toda `CoreEntity`; a organização nunca é inferida das roles.

## `PoliticaAutorizacaoExtensions`

Políticas declaradas em código, quando não cabem na seção do Consul (valores dinâmicos, org fixa):

```csharp
services.AddAuthorization(options =>
    options.AdicionarPoliticaDeRolePorOrganizacao("AdminAcme", "170086231494557697", "admin", "gestor"));
```

Dentro de um `AuthorizationPolicyBuilder`:

```csharp
politica.ExigirEscopo("api:read", "api:write");   // já exige usuário autenticado
```

Prefira sempre `Autenticacao:PoliticasPorRole` / `PoliticasPorEscopo` — código só quando a política
depende de um id de organização.

## `TiposClaimZitadel`

Use as constantes em vez de strings literais quando precisar montar claim manualmente:

`TiposClaimZitadel.Sujeito`, `.Email`, `.Escopo`, `.IdOrganizacao`, `.DominioPrimario`,
`.RolesDoProjetoLegado`, `.RolesDoProjeto(idProjeto)`, `.RoleNaOrganizacao(idOrganizacao)`,
`.EhClaimDeRoleNaOrganizacao(tipoClaim)`.

## `PadroesZitadel`

`EsquemaAutenticacao` (`"ZITADEL"`), `NomeExibicao`, `CaminhoDiscovery`, `CaminhoAutorizacao`,
`CaminhoToken`, `AlgoritmoPadrao` (`"RS256"`).
