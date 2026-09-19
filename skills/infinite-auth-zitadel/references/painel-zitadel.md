# Painel do Zitadel — artefato por artefato

Você não tem acesso ao console. Para cada artefato necessário, **emita o bloco correspondente ao
usuário**, no formato mostrado no SKILL.md, e peça de volta os ids gerados.

Ordem de criação: **Projeto → Roles → Aplicação → Token Settings → Autorizações → (Swagger) →
(Organizações)**.

---

## A1. Projeto

**Console → Projects → Create New Project**

| Campo | Valor |
|---|---|
| Name | nome do produto, ex.: `Infinite` |

Depois de criar, o topo da tela mostra **Resource Id / Project ID** (ex.: `223281939119866113`).

**Devolver para a API:** o Project ID → `Autenticacao:IdProjeto` **e** `Autenticacao:Audiencias`.

### A1.1 Flags do projeto (aba **General**)

| Flag | Ligar? | Por quê |
|---|---|---|
| **Assert Roles on Authentication** | ✅ sim | inclui as roles nas informações do usuário |
| **Check authorization on Authentication** | ✅ recomendado | só permite login de quem tem authorization no projeto |

Sem **Assert Roles**, nenhuma role chega e todo `[Authorize(Roles=...)]` devolve **403**.

---

## A2. Role (papel)

**Projeto → aba Roles → New** — um por role da matriz.

| Campo | Valor |
|---|---|
| **Key** | o identificador usado no código: `admin`, `gestor`, `leitor`, `financeiro-aprovador` |
| **Display Name** | nome legível: `Administrador`, `Aprovador financeiro` |
| **Group** | opcional, agrupa para atribuir várias de uma vez: `financeiro` |

A **Key** é exatamente o texto que aparece em `[Authorize(Roles = "...")]` e nos valores de
`Autenticacao:PoliticasPorRole`. Minúsculo, sem acento, `-` como separador.

**Devolver para a API:** nada — a Key já é conhecida.

---

## A3. Autorização (conceder role ao usuário)

**Projeto → aba Authorizations** (em algumas versões, **Role Assignments**) **→ New**

1. Buscar o usuário (ou service user).
2. Selecionar as roles.
3. Salvar.

Sem isso, o usuário autentica mas o token vem sem roles → **403**.

---

## A4. Aplicação do cliente (SPA / nativa)

**Projeto → Applications → New**

| Campo | Valor |
|---|---|
| **Name** | ex.: `optics-web` |
| **Type** | `User Agent` para SPA (Vue/React) · `Native` para mobile/desktop |
| **Authentication Method** | **PKCE** |
| **Redirect URIs** | `https://app.acme.com.br/callback` e `http://localhost:5173/callback` (dev) |
| **Post Logout URIs** | `https://app.acme.com.br` |

**Devolver:** o **Client Id** — vai no **frontend**, nunca na API.

---

## A5. Token Settings da aplicação — passo crítico

**Aplicação criada → aba Token Settings**

| Opção | Valor | Consequência de errar |
|---|---|---|
| **Auth Token Type** | **`JWT`** | em `Bearer` o token é **opaco** → a API não valida → **401 em toda request** |
| **Add user roles to the access token** | ✅ | sem isso o access token vem sem roles → **403 sempre** |
| **User roles inside ID Token** | opcional | útil para o frontend decidir o que exibir |
| **User Info inside ID Token** | opcional | perfil no ID token |

Salvar e **gerar um token novo** — o token antigo continua opaco.

---

## A6. Audience correta

O access token já traz como `aud` o **Project ID** do projeto da aplicação e o Client Id.

- **Frontend e API no mesmo projeto** (caso normal): basta o Project ID em `Autenticacao:Audiencias`.
- **API em outro projeto**: o frontend precisa pedir também o escopo abaixo, senão a API responde
  **401 Invalid audience**:

  ```
  urn:zitadel:iam:org:project:id:{PROJECT_ID_DA_API}:aud
  ```

Para aceitar Project ID e Client Id, liste os dois em `Audiencias`. Já `IdProjeto` é **sempre e
somente** o Project ID: é ele que define de qual projeto as roles do token valem nesta API.

---

## A7. Aplicação do Swagger (opcional)

Só se a API vai usar o botão **Authorize** do Swagger.

**Projeto → Applications → New**

| Campo | Valor |
|---|---|
| **Name** | ex.: `optics-swagger` |
| **Type** | `User Agent` |
| **Authentication Method** | **PKCE** |
| **Redirect URI** | raiz do Swagger + `/oauth2-redirect.html` — o Swagger da lib fica na raiz, então `https://api.acme.com.br/oauth2-redirect.html` |

Repita o **A5** (Token Settings → JWT + roles no access token) nesta aplicação também.

**Devolver para a API:** o **Client Id** → `Autenticacao:ClientIdSwagger`.

Em ambiente que não seja `Development`, lembre de ligar `"ExporSwagger": true` — senão o Swagger
não é publicado e o botão Authorize não aparece.

---

## A8. Escopos

O Zitadel não tem uma tela de "criar escopo" para escopos de API — o cliente pede os escopos na
autenticação e eles chegam na claim `scope` do token. Os padrão são `openid`, `profile`, `email`.

Para escopos próprios (`api:read`, `api:write`), o cliente precisa incluí-los na requisição de
autorização; a API então os agrupa em `Autenticacao:PoliticasPorEscopo`. Se o escopo não for solicitado
pelo cliente, a política devolve **403**.

Na dúvida entre role e escopo: **role = quem é o usuário; escopo = o que o cliente pediu
permissão para fazer**. Para autorização de negócio, use **role**.

---

## A9. Organização (multi-tenant)

**Console → Organization** — o **Resource Id** da organização (ex.: `170086231494557697`).

**Devolver para a API:**

- lista de ids → `Autenticacao:OrganizacoesPermitidas` (tokens de outras orgs passam a receber
  **401**);
- id individual → `AdicionarPoliticaDeRolePorOrganizacao("AdminAcme", "170086231494557697", "admin")`.
