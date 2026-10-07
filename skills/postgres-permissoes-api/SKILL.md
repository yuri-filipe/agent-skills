---
name: postgres-permissoes-api
description: "Gera o script SQL que cria o login e as permissões de uma API no PostgreSQL, em QA ou produção. Use ao criar login, usuário, role ou grants de API no Postgres, ou preparar o banco para uma API nova."
---

# Permissões Postgres por API

Gera um script SQL pronto para rodar que cria o login de uma API e aplica o modelo de permissões abaixo. O script é um único bloco `DO`, então roda igual no psql, DBeaver ou pgAdmin, é atômico (falhou, nada é aplicado) e idempotente (pode rodar de novo).

## Fluxo

1. **Pergunte o nome do schema** se ele não veio no pedido. Cada API tem um schema com o seu nome.
2. **Pergunte o ambiente: QA ou produção.** Se pedirem os dois, gere dois scripts.
3. **Não pergunte a senha de produção no chat.** Ela é uma variável no topo do script (`v_senha`), que sai vazia para o usuário preencher na hora de rodar. Senha digitada no chat fica no histórico da conversa e no arquivo gerado.
4. Normalize o nome do schema, copie o template do ambiente, troque `{{SCHEMA}}` e entregue.

Se o usuário já deu schema e ambiente, não pergunte de novo: gere direto.

## Nomes

| Item | Regra | Exemplo (schema `auto_paroquia`) |
|---|---|---|
| Schema | minúsculo, snake_case, sem acento, só `[a-z0-9_]`, começa com letra ou `_`, até 55 caracteres | `auto_paroquia` |
| Login QA | `api_{schema}_qa` | `api_auto_paroquia_qa` |
| Login PRD | `api_{schema}_prd` | `api_auto_paroquia_prd` |
| Senha QA | igual ao nome do login | `api_auto_paroquia_qa` |
| Senha PRD | variável `v_senha` no topo, vazia | preenchida pelo usuário |
| Grupos | `grp_api_qa` e `grp_api_prd` (NOLOGIN) | |

Os underscores do schema são preservados no login. Se o usuário passar o nome já com `api_` na frente ou `_qa`/`_prd` no fim, ou com maiúsculas, espaços ou hífens, normalize e diga em uma linha qual nome ficou. O limite de 55 existe porque o Postgres trunca identificadores em 63 bytes e o login soma 8 caracteres ao schema.

## Modelo de permissões

| Regra | QA | Produção |
|---|---|---|
| Ler todos os schemas do banco | sim | sim |
| Inserir e editar linhas | em todos os schemas | só no schema da própria API |
| Apagar linhas (DELETE, TRUNCATE) | sim | não, em nenhum schema |
| Criar schema | sim | não |
| Criar objeto em schema dos outros | sim | não |
| Alterar ou excluir objeto e schema | só os que o login criou, mais o schema da própria API | não |
| Dono do schema da API | o próprio login | `v_dono` (padrão `postgres`) |
| Objetos futuros | cobertos, de qualquer login de QA e do admin | cobertos quando criados por `v_dono` |

Por que o desenho é este, para responder dúvidas e não "melhorar" o template de um jeito que quebre uma regra:

- **`ALTER` e `DROP` não são concedíveis por `GRANT` no Postgres.** Só o dono do objeto faz. É isso que entrega "só exclui o schema que criou" sem nenhuma linha de código, e é por isso que em produção o login nunca é dono de nada.
- **Default privileges valem por quem cria o objeto.** "Objetos futuros" só funciona porque o script registra `ALTER DEFAULT PRIVILEGES FOR ROLE <criador>`. Em QA os criadores são o login e o admin que rodou o script; em produção é `v_dono`. Objeto criado por outra role não herda nada: rodar o script de novo resolve.
- **Os grants vão para o grupo, não para o login.** Um login novo entra no grupo e passa a enxergar tudo que os anteriores criaram, sem precisar cruzar permissões login a login.
- **Trigger não tem privilégio próprio.** O que existe é `TRIGGER` na tabela e `EXECUTE` na função; o script cobre os dois.
- **`pg_read_all_data` e `pg_write_all_data` não são usados de propósito.** Valem para o servidor inteiro, não só para o banco atual, e `pg_write_all_data` inclui DELETE.
- **Em produção o script audita o resultado real** com `has_*_privilege` e aborta se o login ainda puder criar, apagar ou for dono de algo. Isso pega o que REVOKE no login não resolve, como permissão dada a `PUBLIC`.

Se pedirem uma regra diferente (por exemplo DELETE em uma tabela de produção), não reescreva o modelo: use o bloco de exceção comentado no fim do template de produção. Se a mudança for no modelo em si, avise qual regra da tabela acima ela quebra antes de gerar.

## Saída

- Copie o template do ambiente **exatamente como está**, trocando apenas `{{SCHEMA}}` pelo nome normalizado. Os dois templates foram testados em Postgres real contra cada regra da tabela; alteração de improviso tira essa garantia.
- Entregue como arquivo `permissoes_{schema}_{ambiente}.sql` (ex.: `permissoes_auto_paroquia_qa.sql`). Sem ferramenta de arquivo, entregue em um bloco de código `sql`.
- Depois do script, no máximo estas linhas:
  - rodar conectado **no banco do ambiente**, com o superusuário `postgres`;
  - produção: preencher `v_senha` no topo antes de rodar e não salvar o arquivo com a senha;
  - produção: a API não pode rodar `Database.Migrate()` nem `Remove()`/`ExecuteDelete()` com este login. Migration roda com `v_dono`; exclusão vira soft delete.
- Não explique o script linha a linha: ele já é comentado.

## Template QA

```sql
-- =====================================================================
-- Permissões Postgres | API: {{SCHEMA}} | Ambiente: QA
-- Login: api_{{SCHEMA}}_qa | Grupo: grp_api_qa
--
-- Como rodar: conectado NO BANCO DE QA, com um superusuário (postgres).
-- Idempotente: pode rodar de novo sem erro e sem duplicar nada.
-- Atômico: se qualquer passo falhar, nada é aplicado.
-- =====================================================================
DO $permissoes$
DECLARE
    -- ========================= VARIÁVEIS =========================
    v_schema text := '{{SCHEMA}}';           -- schema da API
    v_login  text := 'api_{{SCHEMA}}_qa';    -- login da API
    v_senha  text := 'api_{{SCHEMA}}_qa';    -- QA: a senha é o próprio nome do login
    v_grupo  text := 'grp_api_qa';           -- grupo NOLOGIN que carrega as permissões
    -- =============================================================
    v_admin  text := current_user;
    v_db     text := current_database();
    r        record;
BEGIN
    -- 0. Pré-condições -------------------------------------------------
    IF NOT (SELECT rolsuper FROM pg_roles WHERE rolname = current_user) THEN
        RAISE EXCEPTION 'Rode com um superusuário (ex.: postgres). Usuário atual: %', current_user;
    END IF;
    IF v_schema !~ '^[a-z_][a-z0-9_]*$' OR length(v_login) > 63 THEN
        RAISE EXCEPTION 'Nome de schema inválido ou longo demais: %', v_schema;
    END IF;

    -- 1. Grupo e login -------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_grupo) THEN
        EXECUTE format('CREATE ROLE %I NOLOGIN', v_grupo);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_login) THEN
        EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', v_login, v_senha);
    ELSE
        EXECUTE format('ALTER ROLE %I LOGIN PASSWORD %L', v_login, v_senha);
    END IF;
    EXECUTE format('ALTER ROLE %I NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT', v_login);
    IF NOT pg_has_role(v_login, v_grupo, 'MEMBER') THEN
        EXECUTE format('GRANT %I TO %I', v_grupo, v_login);
    END IF;

    -- 2. Banco: conectar e CRIAR SCHEMAS --------------------------------
    EXECUTE format('GRANT CONNECT, CREATE, TEMPORARY ON DATABASE %I TO %I', v_db, v_grupo);

    -- 3. Schema da API: o login é o dono --------------------------------
    --    Dono = pode alterar e excluir. É por isso que cada login só
    --    consegue excluir os schemas que ele mesmo criou (ou o da sua API).
    IF to_regnamespace(quote_ident(v_schema)) IS NULL THEN
        EXECUTE format('CREATE SCHEMA %I AUTHORIZATION %I', v_schema, v_login);
    ELSE
        EXECUTE format('ALTER SCHEMA %I OWNER TO %I', v_schema, v_login);

        -- Objetos que já existiam no schema passam para o login,
        -- senão a migration da API falha com "must be owner of table".
        FOR r IN
            SELECT c.oid::regclass::text AS obj
              FROM pg_class c
             WHERE c.relnamespace = to_regnamespace(quote_ident(v_schema))
               AND c.relkind IN ('r','p','v','m','f','S')
               AND c.relowner <> v_login::regrole
               AND NOT EXISTS (SELECT 1 FROM pg_depend d            -- pula sequence de coluna e objeto de extensão
                                WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid
                                  AND d.deptype IN ('a','i','e'))
        LOOP
            EXECUTE format('ALTER TABLE %s OWNER TO %I', r.obj, v_login);
        END LOOP;

        FOR r IN
            SELECT p.oid::regprocedure::text AS obj
              FROM pg_proc p
             WHERE p.pronamespace = to_regnamespace(quote_ident(v_schema))
               AND p.proowner <> v_login::regrole
               AND NOT EXISTS (SELECT 1 FROM pg_depend d
                                WHERE d.classid = 'pg_proc'::regclass AND d.objid = p.oid AND d.deptype = 'e')
        LOOP
            EXECUTE format('ALTER ROUTINE %s OWNER TO %I', r.obj, v_login);
        END LOOP;

        FOR r IN
            SELECT t.oid::regtype::text AS obj
              FROM pg_type t
             WHERE t.typnamespace = to_regnamespace(quote_ident(v_schema))
               AND t.typtype IN ('e','d','r')                        -- enum, domain, range
               AND t.typowner <> v_login::regrole
               AND NOT EXISTS (SELECT 1 FROM pg_depend d
                                WHERE d.classid = 'pg_type'::regclass AND d.objid = t.oid AND d.deptype = 'e')
        LOOP
            EXECUTE format('ALTER TYPE %s OWNER TO %I', r.obj, v_login);
        END LOOP;
    END IF;

    -- 4. search_path: schema da API primeiro ----------------------------
    EXECUTE format('ALTER ROLE %I IN DATABASE %I SET search_path = %I, public', v_login, v_db, v_schema);

    -- 5. Tudo que JÁ EXISTE, em todos os schemas do banco ---------------
    --    Ler, inserir, editar, apagar linha e criar objeto novo.
    --    Alterar/excluir objeto dos outros NÃO: isso exige ser o dono.
    FOR r IN
        SELECT nspname FROM pg_namespace
         WHERE nspname !~ '^pg_' AND nspname <> 'information_schema'
    LOOP
        EXECUTE format('GRANT USAGE, CREATE ON SCHEMA %I TO %I', r.nspname, v_grupo);
        EXECUTE format('GRANT ALL ON ALL TABLES    IN SCHEMA %I TO %I', r.nspname, v_grupo);
        EXECUTE format('GRANT ALL ON ALL SEQUENCES IN SCHEMA %I TO %I', r.nspname, v_grupo);
        EXECUTE format('GRANT EXECUTE ON ALL ROUTINES IN SCHEMA %I TO %I', r.nspname, v_grupo);
    END LOOP;

    -- 6. Tudo que FOR CRIADO NO FUTURO ----------------------------------
    --    Default privileges valem por "quem cria". Então registramos a regra
    --    para este login e para o admin que rodou o script. Sem IN SCHEMA,
    --    a regra vale para qualquer schema do banco, inclusive os futuros.
    FOR r IN SELECT unnest(ARRAY[v_login, v_admin]) AS criador
    LOOP
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT USAGE, CREATE ON SCHEMAS TO %I', r.criador, v_grupo);
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT ALL ON TABLES    TO %I', r.criador, v_grupo);
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT ALL ON SEQUENCES TO %I', r.criador, v_grupo);
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT EXECUTE ON ROUTINES TO %I', r.criador, v_grupo);
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT USAGE ON TYPES TO %I', r.criador, v_grupo);
    END LOOP;

    RAISE NOTICE 'OK: login % pronto no banco % (schema %, grupo %).', v_login, v_db, v_schema, v_grupo;
END
$permissoes$;
```

## Template produção

```sql
-- =====================================================================
-- Permissões Postgres | API: {{SCHEMA}} | Ambiente: PRODUÇÃO
-- Login: api_{{SCHEMA}}_prd | Grupo: grp_api_prd
--
-- Como rodar: conectado NO BANCO DE PRODUÇÃO, com o superusuário postgres.
-- Idempotente: pode rodar de novo sem erro e sem duplicar nada.
-- Atômico: se qualquer passo ou a auditoria final falhar, nada é aplicado.
--
-- O que o login pode: ler todos os schemas do banco; inserir e editar
-- linhas no schema da própria API.
-- O que o login NÃO pode: apagar linha (DELETE/TRUNCATE) e mexer em
-- estrutura (CREATE/ALTER/DROP). Estrutura só com o dono (v_dono).
-- =====================================================================
DO $permissoes$
DECLARE
    -- ========================= VARIÁVEIS =========================
    v_schema text := '{{SCHEMA}}';            -- schema da API
    v_login  text := 'api_{{SCHEMA}}_prd';    -- login da API

    -- >>> SENHA DE PRODUÇÃO: preencha aqui antes de rodar. <<<
    -- Não salve nem versione este arquivo com a senha preenchida.
    -- Deixe '' para manter a senha atual quando o login já existir.
    v_senha  text := '';

    v_grupo  text := 'grp_api_prd';           -- grupo NOLOGIN: leitura em todos os schemas
    v_dono   text := 'postgres';              -- quem cria/migra a estrutura em produção
    -- =============================================================
    v_db     text := current_database();
    v_erros  text[] := '{}';
    v_lista  text;
    r        record;
BEGIN
    -- 0. Pré-condições -------------------------------------------------
    IF NOT (SELECT rolsuper FROM pg_roles WHERE rolname = current_user) THEN
        RAISE EXCEPTION 'Rode com um superusuário (ex.: postgres). Usuário atual: %', current_user;
    END IF;
    IF v_schema !~ '^[a-z_][a-z0-9_]*$' OR length(v_login) > 63 THEN
        RAISE EXCEPTION 'Nome de schema inválido ou longo demais: %', v_schema;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_dono) THEN
        RAISE EXCEPTION 'A role dona da estrutura (v_dono = %) não existe.', v_dono;
    END IF;

    -- 1. Grupo e login -------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_grupo) THEN
        EXECUTE format('CREATE ROLE %I NOLOGIN', v_grupo);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_login) THEN
        IF v_senha = '' THEN
            RAISE EXCEPTION 'Preencha v_senha no topo do script: o login % ainda não existe.', v_login;
        END IF;
        EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', v_login, v_senha);
    ELSIF v_senha <> '' THEN
        EXECUTE format('ALTER ROLE %I LOGIN PASSWORD %L', v_login, v_senha);
    END IF;
    EXECUTE format('ALTER ROLE %I NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT', v_login);
    IF NOT pg_has_role(v_login, v_grupo, 'MEMBER') THEN
        EXECUTE format('GRANT %I TO %I', v_grupo, v_login);
    END IF;

    -- 2. Banco: só conectar. Criar schema, não. -------------------------
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', v_db, v_grupo);
    EXECUTE format('REVOKE CREATE ON DATABASE %I FROM %I, %I', v_db, v_login, v_grupo);

    -- 3. Schema da API: o dono é v_dono, nunca o login ------------------
    IF to_regnamespace(quote_ident(v_schema)) IS NULL THEN
        EXECUTE format('CREATE SCHEMA %I AUTHORIZATION %I', v_schema, v_dono);
    END IF;

    -- 4. search_path: schema da API primeiro ----------------------------
    EXECUTE format('ALTER ROLE %I IN DATABASE %I SET search_path = %I, public', v_login, v_db, v_schema);

    -- 5. LEITURA em todos os schemas que já existem (via grupo) ---------
    --    Os REVOKE garantem a regra mesmo se alguém concedeu algo antes.
    FOR r IN
        SELECT nspname FROM pg_namespace
         WHERE nspname !~ '^pg_' AND nspname <> 'information_schema'
    LOOP
        EXECUTE format('REVOKE CREATE ON SCHEMA %I FROM %I, %I', r.nspname, v_login, v_grupo);
        EXECUTE format('REVOKE DELETE, TRUNCATE ON ALL TABLES IN SCHEMA %I FROM %I, %I', r.nspname, v_login, v_grupo);
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', r.nspname, v_grupo);
        EXECUTE format('GRANT SELECT ON ALL TABLES    IN SCHEMA %I TO %I', r.nspname, v_grupo);
        EXECUTE format('GRANT SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I', r.nspname, v_grupo);
    END LOOP;

    -- 6. ESCRITA (inserir e editar) só no schema da própria API ---------
    EXECUTE format('GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA %I TO %I', v_schema, v_login);
    EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I', v_schema, v_login);
    EXECUTE format('GRANT EXECUTE ON ALL ROUTINES IN SCHEMA %I TO %I', v_schema, v_login);

    -- 7. Tudo que v_dono CRIAR NO FUTURO --------------------------------
    --    Default privileges valem por "quem cria". Objeto criado por outra
    --    role não herda nada: rode este script de novo para cobrir.
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT USAGE ON SCHEMAS TO %I', v_dono, v_grupo);
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT SELECT ON TABLES    TO %I', v_dono, v_grupo);
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I GRANT SELECT ON SEQUENCES TO %I', v_dono, v_grupo);
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT, INSERT, UPDATE ON TABLES TO %I', v_dono, v_schema, v_login);
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO %I', v_dono, v_schema, v_login);
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT EXECUTE ON ROUTINES TO %I', v_dono, v_schema, v_login);

    -- 8. AUDITORIA: confere o resultado real, não a intenção ------------
    --    Pega o que o script não consegue consertar sozinho: permissão
    --    dada a PUBLIC, login dono de objeto, login em outro grupo.
    IF has_database_privilege(v_login, v_db, 'CREATE') THEN
        v_erros := v_erros || format('pode criar schema no banco %s', v_db);
    END IF;

    SELECT string_agg(nspname, ', ') INTO v_lista
      FROM pg_namespace
     WHERE nspname !~ '^pg_' AND nspname <> 'information_schema'
       AND has_schema_privilege(v_login, oid, 'CREATE');
    IF v_lista IS NOT NULL THEN
        v_erros := v_erros || ('pode criar objeto nos schemas: ' || v_lista);
    END IF;

    SELECT string_agg(obj, ', ') INTO v_lista FROM (
        SELECT c.oid::regclass::text AS obj
          FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
           AND c.relkind IN ('r','p','v','m','f')
           AND (has_table_privilege(v_login, c.oid, 'DELETE') OR has_table_privilege(v_login, c.oid, 'TRUNCATE'))
         LIMIT 15
    ) x;
    IF v_lista IS NOT NULL THEN
        v_erros := v_erros || ('pode apagar linhas (DELETE/TRUNCATE) em (até 15): ' || v_lista);
    END IF;

    SELECT string_agg(obj, ', ') INTO v_lista FROM (
        SELECT 'schema ' || nspname AS obj FROM pg_namespace WHERE nspowner = v_login::regrole
        UNION ALL
        SELECT c.oid::regclass::text FROM pg_class c
         WHERE c.relowner = v_login::regrole AND c.relkind IN ('r','p','v','m','f','S')
        UNION ALL
        SELECT p.oid::regprocedure::text FROM pg_proc p WHERE p.proowner = v_login::regrole
        LIMIT 15
    ) x;
    IF v_lista IS NOT NULL THEN
        v_erros := v_erros || ('é dono (pode alterar/excluir) de (até 15): ' || v_lista);
    END IF;

    SELECT string_agg(m.roleid::regrole::text, ', ') INTO v_lista
      FROM pg_auth_members m
     WHERE m.member = v_login::regrole AND m.roleid <> v_grupo::regrole;
    IF v_lista IS NOT NULL THEN
        v_erros := v_erros || ('é membro de outras roles: ' || v_lista);
    END IF;

    IF cardinality(v_erros) > 0 THEN
        RAISE EXCEPTION E'AUDITORIA FALHOU, nada foi aplicado. O login % ainda:\n - %',
            v_login, array_to_string(v_erros, E'\n - ');
    END IF;

    RAISE NOTICE 'OK: login % pronto no banco % (schema %, grupo %). Auditoria sem violações.', v_login, v_db, v_schema, v_grupo;
END
$permissoes$;

-- ---------------------------------------------------------------------
-- EXCEÇÃO DE DELETE (opcional). A regra é zero DELETE em produção.
-- Se uma tabela específica precisar (ex.: outbox), descomente e ajuste.
-- Fica DEPOIS do bloco de propósito: cada execução revoga todo DELETE
-- e audita; só então esta linha concede de novo a exceção consciente.
-- ---------------------------------------------------------------------
-- GRANT DELETE ON {{SCHEMA}}.nome_da_tabela TO api_{{SCHEMA}}_prd;
```

## Quando algo der errado

| Sintoma | Causa | O que fazer |
|---|---|---|
| `Rode com um superusuário` | o script foi rodado com um login comum | rodar com `postgres`; `ALTER DEFAULT PRIVILEGES FOR ROLE` exige isso |
| `Preencha v_senha` | produção, login novo, senha vazia | preencher a variável no topo |
| `AUDITORIA FALHOU` | o login de produção ainda tem um poder proibido; a mensagem lista qual e onde | corrigir a origem (grant a `PUBLIC`, login dono de objeto, login em outro grupo) e rodar de novo |
| `permission denied for table` em tabela nova | a tabela foi criada por uma role que não é o login (QA) nem `v_dono` (produção) | rodar o script de novo |
| `must be owner of table` em QA | a API tentou alterar tabela de outro schema | esperado: alterar estrutura dos outros não é permitido |
| `permission denied` em DELETE na produção | regra do modelo | soft delete, ou o bloco de exceção para a tabela específica |

Alvo: PostgreSQL 18. Templates testados no PostgreSQL 16.15; usam só sintaxe disponível desde a versão 14.
