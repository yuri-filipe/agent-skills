---
name: infinite-frontend-api
description: "Consome APIs .NET da Infinite (Infinite.Core.WebHost) em frontends Vue/Angular. Use para clientes HTTP, listagens paginadas, tratamento de erros 400/401/403/404/422 e envio de DTOs."
---

# Contrato HTTP das APIs Infinite para o frontend

As APIs .NET da Infinite usam `InfiniteApiController.Send`, que traduz o resultado do handler em
status e corpo padronizados. Este skill descreve **o que o front recebe e o que ele pode
enviar**, e como consumir isso no cliente HTTP que o projeto já usa.

**Fora do escopo:**

- URL base e config por ambiente: skill `frontend-remote-config` (Vue/Vite) ou a config já adotada
  no Angular.
- Datas, fusos e campos temporais: skill `infinite-frontend-datetime`.
- Build e deploy: skill `infinite-angular-build`.
- Configuração da API ou do Zitadel: skills `infinite-dotnet-api` e `infinite-auth-zitadel`.

Não altere o backend como efeito colateral de uma tarefa de front.

## Regras invioláveis

1. **Reaproveite o cliente HTTP existente** (axios, ofetch, `HttpClient` do Angular,
   `createFetch`). Ajuste interceptors e tipos dele; não crie um segundo cliente paralelo.
2. **Envie exatamente o DTO do endpoint.** A API recusa propriedades desconhecidas.
3. **Autenticação é Bearer no header**, nunca cookie. Não use `withCredentials`/`credentials: 'include'`.
4. **Trate erro pelo status**, não pelo texto da mensagem. Mostre `message` ao usuário e nunca
   exiba stack trace, `traceId` cru ou corpo técnico sem tratamento.
5. **Uma função de leitura de erro** para todo o app (template em `templates/api-contract.ts`),
   em vez de `if (err.response.data.xxx)` espalhado pelas telas.
6. **O código existente vence este documento**: se o projeto já trata esse contrato de forma
   coerente, siga o padrão dele e só corrija o que diverge da API real.

## Rotas

Padrão dos controllers: `v1/{recurso-no-plural-kebab}`, sob o `apiPath` do serviço no gateway.

| Operação | Requisição | Sucesso |
|---|---|---|
| Listar/pesquisar | `GET /v1/pedidos?page=1&pageSize=20&...filtros` | 200, **array** + headers de paginação |
| Obter por id | `GET /v1/pedidos/{id}` | 200, objeto do DTO de detalhe |
| Criar | `POST /v1/pedidos` com o command no corpo | 200, normalmente o **id** criado (número) |
| Atualizar | `PUT /v1/pedidos/{id}` com o command no corpo | 200, normalmente `true` |
| Excluir | `DELETE /v1/pedidos/{id}` | 200, normalmente `true` (exclusão lógica no backend) |

O corpo de sucesso é o **valor puro**, sem envelope `{ data, success }`. Confira o tipo real no
Swagger da API ou no handler antes de tipar. Em `PUT`, o id da rota prevalece; mande no corpo só o
que o command de atualização declara.

## Listagem paginada

Parâmetros de query comuns a toda listagem:

| Parâmetro | Default | Regra |
|---|---|---|
| `page` | `1` | começa em 1 |
| `pageSize` | `20` | máximo **200**; acima disso a API limita |
| `search` | vazio | só tem efeito se o endpoint implementar busca livre |
| `sortField` | `Id` | nome da **propriedade da entidade** no backend (sem diferenciar maiúsculas); nome desconhecido ordena por `Id` |
| `sortOrder` | `asc` | `desc` inverte; qualquer outro valor é ascendente |

Filtros específicos entram como parâmetros de query com o nome das propriedades da query no
backend (`?nome=abc&idCliente=5`). Omita filtros vazios em vez de mandar `""` ou `null`.

A resposta é **só o array de itens**. A paginação vem nos headers:

| Header | Conteúdo |
|---|---|
| `X-Total-Count` | total de registros no filtro |
| `X-Page` | página efetiva |
| `X-Page-Size` | tamanho efetivo |
| `X-Total-Pages` | total de páginas |

A WebHost expõe esses headers no CORS (`Access-Control-Expose-Headers`), então o JS os lê
também quando front e API estão em domínios diferentes. Se o header vier `null` no código mas
aparecer no DevTools, a API usa uma versão da `Infinite.Core.WebHost` anterior a essa correção:

- **não invente o total** a partir do tamanho do array como se fosse o total real;
- informe que a API precisa atualizar a `Infinite.Core.WebHost`;
- até lá, use navegação "próxima página" (tem próxima se `items.length === pageSize`).

## Erros

| Status | Quando | Corpo |
|---|---|---|
| 400 | regra de negócio (`Invalid`) | `{ title, message, effect }` |
| 400 | JSON malformado, tipo errado, campo desconhecido, data sem fuso | ProblemDetails do ASP.NET: `{ type, title, status, errors: { "$.campo": ["..."] }, traceId }` |
| 401 | sem token, token expirado/inválido, organização não permitida | `application/problem+json`: `{ status, title, detail }` |
| 403 | autenticado sem a role/política exigida, ou `Forbidden` do handler | problem+json do pipeline **ou** `{ title, message, effect }` do handler |
| 404 | registro inexistente (`NotFound`) | `{ title, message, effect }`; rota inexistente vem sem corpo |
| 422 | validação de campos (FluentValidation) | `{ title, message, effect, errors: [{ field, message }] }` |
| 499 | a própria requisição foi cancelada | sem corpo; ignore, não é erro para o usuário |
| 500 | falha inesperada | `{ title, message, effect }`, às vezes com `traceId` |

`effect` é numérico: `1` Success, `2` Created, `3` Error, `4` NotFound, `5` Invalid,
`6` Forbidden, `7` Validation. Prefira o status HTTP para decidir o fluxo.

Os dois formatos de 400 existem de verdade. O primeiro é uma decisão do negócio a mostrar ao
usuário. O segundo indica que **o front enviou algo fora do contrato**: é bug de integração, a
ser corrigido no front, não uma mensagem de UI.

### Erros de campo (422)

`field` vem com o nome da propriedade **do command no backend, em PascalCase**, e com caminho em
coleções: `Nome`, `Endereco.Cep`, `Itens[0].Quantidade`. Os campos do formulário costumam estar
em camelCase. Compare sem diferenciar maiúsculas e minúsculas e, se o formulário usa outro nome,
mantenha um mapa explícito. Um erro sem campo correspondente vai para um aviso geral do formulário,
nunca é descartado.

### 401 e 403

- **401**: o token não vale. Renove pelo cliente OIDC já usado no projeto e repita **uma vez**; se
  falhar de novo, leve ao login. Não entre em laço de refresh.
- **403**: o usuário está autenticado mas não tem permissão. Mostre a mensagem de acesso negado.
  **Não** redirecione para o login nem tente renovar o token.
- Sem o detalhe do 401 no corpo (é genérico por padrão), diagnostique pelo token (audience, roles)
  com a skill `infinite-auth-zitadel`.

## Envio (JSON estrito)

A API rejeita com 400:

- **propriedade que não existe** no command. Isso inclui mandar `id`, campos de auditoria
  (`dataInclusao`, `usuarioInclusao`…) ou campos de exibição no `POST`. Não reenvie o DTO recebido
  no `GET` como corpo do `PUT`; monte o payload com os campos do command;
- chave duplicada;
- parâmetro obrigatório do construtor ausente em commands `record`;
- data/hora legada sem `Z` ou offset (ver `infinite-frontend-datetime`).

Propriedades trafegam em **camelCase**. Campos opcionais sem valor: omita-os ou envie `null`
conforme o tipo do command. Não envie `""` onde o backend espera número ou data.

## Autenticação no cliente

- O token é o **access token JWT** emitido pelo Zitadel para a aplicação do front (Authorization
  Code + PKCE), enviado em `Authorization: Bearer <token>`. Não envie o ID token.
- Anexe o header no interceptor do cliente, só para a URL base da API (não vaze o token para
  terceiros).
- `/health` é anônimo.
- Erro de CORS no navegador antes de qualquer 401 significa que a origem do front não está em
  `Cors:AllowedOrigins` da API: é config do Consul, não do front.

## Como aplicar

1. Localize o cliente HTTP e os interceptors existentes:
   ```bash
   grep -rn "axios.create\|ofetch.create\|createFetch\|HttpInterceptor\|provideHttpClient" src/
   ```
2. Confira o contrato real do endpoint (Swagger da API em qa ou o command/DTO no backend).
3. Adapte `templates/api-contract.ts` ao cliente do projeto: tipos de erro, `lerErroApi` e
   `lerPaginacao`. Coloque-o junto dos utilitários de API já existentes.
4. Tipos TypeScript dos DTOs espelham os DTOs do backend em camelCase; datas como `string`.
5. Listagens: parâmetros de query + headers de paginação (com o fallback para header ausente).
6. Formulários: mapeie 422 para os campos, 400 de negócio para aviso geral, e trate 400 de
   contrato como bug.
7. Rode o type-check/build do projeto e relate o que foi verificado.

## Checklist

- [ ] Um único cliente HTTP, com Bearer no interceptor e sem `withCredentials`
- [ ] Payload de `POST`/`PUT` com exatamente os campos do command
- [ ] Listagem lê a paginação dos headers e trata header ausente (API com WebHost antiga) sem inventar total
- [ ] `sortField` com nome de propriedade da entidade; `pageSize` ≤ 200
- [ ] 422 mapeado para campos sem diferenciar maiúsculas; erro sem campo aparece no formulário
- [ ] 401 renova uma vez e depois vai ao login; 403 mostra acesso negado
- [ ] 499/cancelamento ignorado; 500 mostra mensagem genérica
- [ ] Nenhum `traceId`, stack ou corpo técnico exibido cru ao usuário
