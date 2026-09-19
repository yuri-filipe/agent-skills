---
name: infinite-auth-zitadel
description: "Configura autenticação e autorização Zitadel em APIs com Infinite.Core.WebHost. Use para JWT, roles, policies, Swagger, erros 401/403 e organizações; inclui o contrato com o painel Zitadel."
---

# Autenticação e autorização — Infinite.Core.WebHost + Zitadel

A API é **Resource Provider**. O cliente (SPA/nativo) obtém o access token no Zitadel com
**Authorization Code + PKCE**; a API só **valida o JWT localmente** via JWKS do discovery. A API
**não** faz login, **não** guarda segredo, **não** chama introspection.

```
Usuário ──autentica──▶ Zitadel
Cliente ──code+PKCE──▶ Zitadel ──access token (JWT)──▶ Cliente
Cliente ──Bearer token──▶ API ──valida via JWKS──▶ 200 / 401 / 403
```

## Regras invioláveis

1. **Não escreva código de autenticação na API.** `AddInfiniteApiController` já chama
   `AddInfiniteAuth`, e `UseInfiniteApi` já insere `UseAuthentication()`. Nunca adicione
   `AddJwtBearer`, `AddAuthentication`, `UseAuthentication()` manualmente.
2. **Toda configuração vive no `config.json` do Consul**, seção `Autenticacao` — nunca em
   `appsettings.json`, nunca hard-coded, nunca em variável de ambiente.
3. **`IdProjeto` é obrigatório** quando a autenticação está ligada, e é o Project ID **desta** API.
   Ele define de qual projeto do Zitadel as roles do token valem. Sem ele a API não sobe.
4. **Roles e permissões são artefatos do Zitadel**; políticas são artefatos da API (Consul).
   Nunca crie uma política que aponte para uma role que não exista no painel.
5. **Nomes de artefatos em português**, minúsculo, sem acento, separados por `-`:
   `admin`, `gestor`, `leitor`, `financeiro-aprovador`. O mesmo texto é usado na Key da role no
   Zitadel e em `[Authorize(Roles = "...")]`.
6. **Você não tem acesso ao painel do Zitadel.** Para cada artefato necessário, **liste ao usuário
   o passo a passo do painel** (ver `references/painel-zitadel.md`) e peça os ids gerados
   (Project ID, Client Id, Organization Id) antes de fechar a config.
7. **Alterações na API destino ficam restritas a auth**: seção `Autenticacao` no config.json,
   atributos `[Authorize]`/`[AllowAnonymous]` nos controllers e leitura do usuário via
   `UsuarioAutenticadoExtensions`. Não refatore controller, handler, DI ou pipeline por causa disso.

## Vocabulário dos artefatos

| Artefato (pt) | Onde se cria | Como aparece na API |
|---|---|---|
| **Projeto** | Zitadel → Projects | `Autenticacao:IdProjeto` **e** `Autenticacao:Audiencias` |
| **Aplicação** (SPA/nativa) | Zitadel → Project → Applications | nada na API — Client Id vai no frontend |
| **Aplicação do Swagger** | Zitadel → Project → Applications | `Autenticacao:ClientIdSwagger` |
| **Role** (papel) | Zitadel → Project → Roles | `[Authorize(Roles = "admin")]` |
| **Autorização** (concessão da role ao usuário) | Zitadel → Project → Authorizations | — (chega dentro do token) |
| **Escopo** | Zitadel → Project → Applications (escopos pedidos pelo cliente) | `Autenticacao:PoliticasPorEscopo` |
| **Política por role** | **API** — `Autenticacao:PoliticasPorRole` no Consul | `[Authorize(Policy = "...")]` |
| **Política por escopo** | **API** — `Autenticacao:PoliticasPorEscopo` no Consul | `[Authorize(Policy = "...")]` |
| **Política por organização** | **API** — código, `AdicionarPoliticaDeRolePorOrganizacao` | `[Authorize(Policy = "...")]` |
| **Organização** (tenant) | Zitadel → Organization | `Autenticacao:OrganizacoesPermitidas` |

> "Permissão" não é um objeto do Zitadel. Uma permissão é sempre **uma role** (ou um escopo)
> agrupada por uma **política** na API. Se o usuário pedir "criar a permissão X", crie a role `x` no
> Zitadel e, se ela for combinada com outras, uma política na seção `Autenticacao`.

## Fluxo de trabalho

Siga na ordem. Não pule o passo 2 — é onde o usuário age no painel.

### 1. Levantar

Pergunte/deduza, e confirme com o usuário antes de escrever:

- A API já usa `AddInfiniteApiController` + `UseInfiniteApi`? (se não, é pré-requisito)
- Qual o **Project ID** da API no Zitadel? (obrigatório — sem ele a API não sobe)
- Quais rotas são públicas? (`/health` já é anônimo por padrão)
- Quais roles/permissões existem no domínio e quais endpoints cada uma libera?
- É multi-tenant por organização?
- Vai usar o botão **Authorize** do Swagger? Em qual ambiente?

Monte a matriz **endpoint → artefato exigido** antes de codar.

### 2. Emitir a lista de artefatos do Zitadel

Para **cada** artefato da matriz, apresente ao usuário um bloco no formato:

```
Artefato: role `financeiro-aprovador`
Onde: Console Zitadel → Projects → {Projeto} → aba Roles → New
Preencher:
  Key          = financeiro-aprovador   ← é este texto que vai no [Authorize]
  Display Name = Aprovador financeiro
  Group        = financeiro             (opcional)
Depois: aba Authorizations → New → conceder a role aos usuários
Me devolva: nada (a Key já é conhecida)
```

O passo a passo completo de cada tipo de artefato está em **`references/painel-zitadel.md`** —
leia esse arquivo antes de escrever os blocos. Sempre destaque os dois passos que quebram tudo:

- **Auth Token Type = `JWT`** na aplicação (senão o token é opaco → **401 em toda request**).
- **Add user roles to the access token** (senão `[Authorize(Roles=...)]` → **403 sempre**).

Peça explicitamente os valores que só existem depois de criar: **Project ID**, **Client Id do
Swagger**, **Organization Id**. Sem eles, deixe a config com placeholder e diga o que falta.

### 3. Configurar a seção `Autenticacao` no Consul

Escreva a seção em `{configPath}/{prd|qa}/config.json`. Use
`templates/config-autenticacao.json` como base e `references/configuracao.md` para o significado de
cada chave.

```json
"Autenticacao": {
  "Habilitada": true,
  "Autoridade": "https://acme-xyz123.zitadel.cloud",
  "IdProjeto": "223281939119866113",
  "Audiencias": [ "223281939119866113" ],
  "ExigirAutenticacaoPorPadrao": true,
  "PoliticasPorRole": { "Financeiro": [ "financeiro-aprovador", "admin" ] },
  "PoliticasPorEscopo": { "ApiLeitura": [ "api:read" ] },
  "ClientIdSwagger": "223281939119866114@optics",
  "EscoposSwagger": [ "openid", "profile", "email" ]
}
```

Regras:

- `Habilitada: true` faz a API **falhar no startup** se algo estiver faltando — isso é intencional,
  não contorne.
- `IdProjeto` é o **Project ID**, nunca o Client Id, mesmo quando `Audiencias` lista os dois.
- Um nome de política **não pode** estar em `PoliticasPorRole` e `PoliticasPorEscopo` ao mesmo tempo.
- Prefira interpolação de chaves compartilhadas do Consul quando o valor é do ambiente:
  `"Autoridade": "https://{{ zitadel_domain }}"`.
- `Cors:AllowedOrigins` precisa conter a origem do frontend, senão o navegador mostra erro de CORS
  antes do 401.
- Em `qa`/`prd`, o Swagger só é publicado com `"ExporSwagger": true` — ver `references/configuracao.md`.
- O template traz todas as chaves opcionais; **remova as que a API não usa**
  (`PoliticasPorEscopo`, `OrganizacoesPermitidas`, `ClientIdSwagger`) em vez de deixá-las vazias —
  vazias fazem a API falhar no startup.

### 4. Proteger os endpoints

Com `ExigirAutenticacaoPorPadrao: true`, **toda rota já exige token**. Só marque o que foge disso:

```csharp
public class PedidosController(IMediator mediator, ILogger<PedidosController> logger)
    : InfiniteApiController(mediator, logger)
{
    [HttpGet]                                   // só token válido (fallback policy)
    public Task<IActionResult> Listar() => Send(new ListarPedidosQuery());

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]                // role do Zitadel
    public Task<IActionResult> Excluir(Guid id) => Send(new ExcluirPedidoCommand(id));

    [HttpPost]
    [Authorize(Policy = "Financeiro")]          // política declarada na seção Autenticacao
    public Task<IActionResult> Criar(CriarPedidoCommand command) => Send(command);

    [HttpGet("publico")]
    [AllowAnonymous]                            // rota pública
    public Task<IActionResult> Publico() => Send(new ConsultaPublicaQuery());
}
```

- Não coloque `[Authorize]` sem parâmetro em controller quando o fallback já cobre.
- `[Authorize(Roles = "a,b")]` = "a **ou** b". Para "a **e** b", use política.
- Não faça verificação de permissão dentro de handler — a autorização é do pipeline.

### 5. Ler o usuário autenticado

Só quando o domínio precisar (auditoria, filtro por tenant):

```csharp
using Infinite.Core.WebHost.Extensoes.Autenticacao;

var idUsuario = User.ObterIdUsuario();       // sub
var email     = User.ObterEmail();
var idOrg     = User.ObterIdOrganizacao();   // tenant
var escopos   = User.ObterEscopos();

if (User.PossuiEscopo("api:read")) { }
if (User.PossuiAlgumaRole("admin", "gestor")) { }
if (User.PossuiRoleNaOrganizacao(idOrg!, "admin")) { }
```

**Auditoria já é automática**: toda entidade `CoreEntity` grava `UsuarioInclusao/Alteracao` (`sub`)
e `OrganizacaoInclusao/Alteracao` (`urn:zitadel:iam:org:id`) no `SaveChanges`. Não preencha
esses campos nem crie middleware para isso.

Para usar o usuário no domínio (handler, service), injete `IAuditContextAccessor`
(`Infinite.Core.Auditing`, registrado pela lib mesmo com auth desligada):

```csharp
var atual = auditContextAccessor.GetCurrent();
// IsAuthenticated, UserId (sub), UserName (ClaimDeNome), Email, OrganizationId
```

Sem identidade autenticada ele devolve `AuditContext.Empty`. `IHttpContextAccessor` também é
registrado sempre, mas o Domain não deve depender de ASP.NET. **Não** repasse `ClaimsPrincipal`
nem id de usuário como parâmetro de command/query vindo do body.

### 6. Verificar e entregar

Rode a matriz de verificação de `references/verificacao.md` e entregue ao usuário:

1. o diff da API (config.json + atributos nos controllers);
2. a lista de artefatos que ele ainda precisa criar/confirmar no painel;
3. os `curl` de verificação.

## Migrando uma API da versão anterior da lib

Se a API já tinha a seção **`AuthConfig`** (nomes em inglês), ela precisa migrar. O mapa completo
de chaves, namespaces e métodos está na seção **8. Migração** do README da lib. O essencial:

- seção `AuthConfig` → `Autenticacao`, e todas as chaves para os nomes em português;
- **`IdProjeto` é novo e obrigatório**;
- `using Infinite.Core.WebHost.Extensions.Authentication` → `...Extensoes.Autenticacao`;
- `User.GetUserId()` → `User.ObterIdUsuario()`, e os demais atalhos equivalentes;
- roles de outros projetos deixam de valer → quem dependia disso passa a receber `403`;
- Swagger some em `qa`/`prd` com auth ligada, salvo `ExporSwagger: true`.

## Referências

- `references/painel-zitadel.md` — passo a passo do painel, artefato por artefato.
- `references/configuracao.md` — todas as chaves da seção e a API de código da lib.
- `references/verificacao.md` — matriz de verificação e diagnóstico de 401/403.
- `templates/config-autenticacao.json` — seção pronta para colar no Consul.
