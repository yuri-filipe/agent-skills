# Verificação e diagnóstico

## Matriz de verificação

Rode antes de entregar. Cada linha é um teste manual.

| Cenário | Esperado |
|---|---|
| `GET /health` sem token | `200` (anônimo por padrão) |
| `GET /{rota-protegida}` sem token | `401` + `WWW-Authenticate: Bearer` |
| Token válido | `200` |
| Token expirado, de outra audience ou de organização não permitida | `401` |
| Token válido, sem a role exigida | `403` |
| Token com a role certa, mas **de outro projeto** | `403` |
| Rota marcada `[AllowAnonymous]` sem token | `200` |
| `GET /swagger/v1/swagger.json` em prd com auth ligada | `404` |
| `"Habilitada": false` | API sobe aberta, com **warning** no log |
| `"Habilitada": true` sem `Autoridade` ou `IdProjeto` | API **não sobe**, com a mensagem da chave faltante |

```bash
curl -i https://api.acme.com.br/health
```

```bash
curl -i https://api.acme.com.br/pedidos
```

```bash
curl -i -H "Authorization: Bearer $TOKEN" https://api.acme.com.br/pedidos
```

Para inspecionar o token, cole o JWT em https://jwt.io e confira:

- `aud` contém o Project ID configurado em `Audiencias`;
- existe a claim `urn:zitadel:iam:org:project:{idProjeto}:roles` — com **o mesmo `idProjeto` da
  config** — e com as roles esperadas;
- `urn:zitadel:iam:org:id` bate com `OrganizacoesPermitidas`, se essa chave estiver em uso.

Se o "token" colado não tiver três partes separadas por `.`, ele é **opaco** — o problema é o
**Auth Token Type** da aplicação no Zitadel.

## Diagnóstico

| Sintoma | Causa provável | Correção |
|---|---|---|
| 401 com qualquer token | **Auth Token Type** ainda em `Bearer` (token opaco) | Aplicação → Token Settings → `JWT`, gerar token novo |
| 401 `Invalid audience` | `Audiencias` sem o Project ID, ou falta o escopo `urn:zitadel:iam:org:project:id:{projectId}:aud` | Ajustar `Audiencias` ou pedir o escopo no frontend |
| 401 sem detalhe nenhum no corpo | Comportamento padrão — o motivo não é exposto a quem não está autenticado | Ligar `DetalharErrosDeToken` **só em dev**, ou ler o log |
| 401 em token de outra organização | `OrganizacoesPermitidas` não inclui a org | Adicionar o Resource Id da organização. O motivo está no `warning` do log |
| 403 mesmo com a role concedida | Falta **Add user roles to the access token**, falta **Assert Roles on Authentication**, ou o usuário não tem Authorization no projeto | Corrigir no painel e gerar token novo |
| 403 e a claim de roles no token é de outro `projectId` | `IdProjeto` não bate com o projeto que emitiu as roles | Corrigir `IdProjeto` para o Project ID desta API |
| 403 e o token só traz `urn:zitadel:iam:org:project:roles` (sem id) | Instância emitindo a claim legada | `AceitarClaimLegadaDeRoles: true`, ou atualizar a instância |
| 403 em política de escopo | O cliente não solicitou o escopo na autenticação | Incluir o escopo na requisição do frontend |
| API não sobe: `InvalidOperationException: Autenticacao:...` | Seção do Consul incompleta | A mensagem diz qual chave |
| API sobe sem exigir token | `Habilitada: false` ou `ExigirAutenticacaoPorPadrao` não ligado | Ajustar a seção |
| Swagger sumiu em qa/prd | Padrão com auth ligada fora de `Development` | `"ExporSwagger": true` |
| Erro de CORS no navegador antes do 401 | Origem do frontend fora de `Cors:AllowedOrigins` | Adicionar a origem |
| Frontend que usava cookie parou | As políticas de CORS não usam mais `AllowCredentials` | A autenticação é por Bearer no header |
| Botão Authorize do Swagger não aparece | `ClientIdSwagger` não configurado | Criar a aplicação do Swagger e preencher a chave |
| Swagger redireciona e falha | Redirect URI da aplicação do Swagger errado | Deve ser a raiz da API + `/oauth2-redirect.html` |
| 500 sem detalhe, só `traceId` | Comportamento padrão — a mensagem da exceção não vai no corpo | Buscar o `traceId` no log da API |
| `UseAuthentication` registrado duas vezes | — | A lib já detecta; não adicione manualmente |
