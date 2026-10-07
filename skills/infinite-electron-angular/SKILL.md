---
name: infinite-electron-angular
description: "Cria apps desktop Electron + Angular no padrão do Infinite.Tools, com persistência em JSON local, SQLite ou PostgreSQL. Use ao criar um projeto desktop ou adicionar tela, IPC ou persistência nele."
---

# Electron + Angular desktop (padrão Infinite.Tools)

Padrão extraído do Infinite.Tools (Electron 43, Angular 22, Angular Material 22, TypeScript 6,
electron-builder 26, Windows 11 x64). Ao criar um projeto novo, use as versões estáveis atuais
dessas libs; os números acima são a referência do que foi validado, não um pin.

Vários itens aqui existem porque já quebraram em produção. A seção "Armadilhas" explica cada
um; não remova um deles sem entender o motivo.

## 1. Perguntas antes de gerar qualquer arquivo

Pergunte (use a ferramenta de perguntas se houver; senão, em texto) e só então gere:

1. **Nome do app**: nome de exibição (`productName`, ex. "Infinite Tools"), nome do pacote em
   kebab-case (`name`, ex. `infinite-tools`) e `appId` (ex. `br.com.infinite.tools`).
2. **Onde os dados da aplicação serão salvos?**
   - **JSON local** (forma do Infinite.Tools — padrão quando não for usar banco)
   - **SQLite** (arquivo local, relacional)
   - **PostgreSQL** (servidor, dados compartilhados entre máquinas)
3. **Só se PostgreSQL: qual o nome do schema** onde as tabelas da aplicação serão criadas?
   Valide contra `^[a-z_][a-z0-9_]{0,62}$` e recuse `public`, `pg_*` e `information_schema`.
   SQLite não tem schema (é um arquivo por banco): não pergunte, não emule com prefixo de tabela.

Consequências da resposta 2:

| Escolha | Dados de domínio | Preferências locais (tema etc.) | Extra obrigatório |
|---|---|---|---|
| JSON local | `settings.json` (+ um arquivo por domínio pesado) | `settings.json` | nada |
| SQLite | `<name>.db` no `userData` | `settings.json` | `better-sqlite3`, `postinstall`, `asarUnpack` |
| PostgreSQL | schema informado | `settings.json` | `pg`, **seção "Banco de dados" em Configurações** para a string de conexão, `database.json` cifrado |

Em todos os casos o `settings.json` (seção 6) continua existindo: é ele que guarda o que é da
máquina/usuário. No Postgres isso não é opcional: a string de conexão não pode morar dentro do
banco que ela abre.

## 2. Estrutura

```
<projeto>/
  electron/                 processo principal (TS -> dist-electron/, CommonJS)
    main.ts                 janela, protocolo app://, registro de IPC, ciclo de vida
    preload.ts              contextBridge: única superfície exposta ao renderer
    settings.ts             persistência JSON (seção 6)
    <dominio>.ts            um arquivo por domínio (hosts.ts, oracle.ts, consul.ts...)
    db/                     só com SQLite ou Postgres (seções 7 e 8)
  src/
    main.ts  index.html  styles.scss  _fonts.scss (gerado)
    types/api.d.ts          contrato tipado do preload + declare global Window
    app/
      app.ts app.html app.scss app.config.ts app.routes.ts
      core/                 services (providedIn root) e funções puras
        electron.service.ts settings.service.ts status.service.ts models.ts
      features/<tela>/      um diretório por tela; diálogos ao lado da tela
      shared/               componentes reutilizados (status-overlay...)
  scripts/generate-fonts.mjs
  public/favicon.ico
  angular.json package.json tsconfig.json tsconfig.app.json tsconfig.electron.json
```

`.gitignore`: `node_modules/ dist/ dist-electron/ release/ *.log`.

## 3. Arquivos de configuração

### package.json (partes que importam)

```json
{
  "name": "<name>",
  "productName": "<Product Name>",
  "version": "1.0.0",
  "private": true,
  "main": "dist-electron/main.js",
  "scripts": {
    "start": "ng serve",
    "build": "ng build",
    "build:electron": "tsc -p tsconfig.electron.json",
    "build:all": "npm run build && npm run build:electron",
    "electron": "npm run build:all && electron .",
    "dist": "npm run build:all && electron-builder --win --x64",
    "fonts": "node scripts/generate-fonts.mjs",
    "format": "prettier --write ./src"
  },
  "build": {
    "appId": "<appId>",
    "productName": "<Product Name>",
    "directories": { "output": "release" },
    "files": ["dist-electron/**/*", "dist/<name>/browser/**/*", "package.json"],
    "win": { "target": [{ "target": "nsis", "arch": ["x64"] }] },
    "nsis": {
      "oneClick": false,
      "perMachine": false,
      "allowToChangeInstallationDirectory": true,
      "shortcutName": "<Product Name>",
      "artifactName": "<ProductName>-Setup-${version}.exe"
    }
  }
}
```

- Dependências de runtime do renderer: `@angular/{cdk,common,compiler,core,forms,material,platform-browser,router}`, `rxjs`, `tslib`.
- Dev: `@angular/build`, `@angular/cli`, `@angular/compiler-cli`, `@types/node`, `electron`, `electron-builder`, `typescript`, `@fontsource/roboto`, `material-icons` (as duas últimas só alimentam o gerador de fontes).
- Tudo que o **processo principal** importa em runtime (`pg`, `better-sqlite3`, `oracledb`) vai em `dependencies`: o electron-builder só empacota `dependencies`.
- `dist/<name>/browser` precisa bater com o nome do projeto no `angular.json` e com `RENDERER_ROOT` no `main.ts`. Se um mudar, mudam os três.

### tsconfig.electron.json

```json
{
  "compilerOptions": {
    "target": "ES2022", "module": "commonjs", "moduleResolution": "node10",
    "ignoreDeprecations": "6.0", "outDir": "dist-electron", "rootDir": "electron",
    "strict": true, "esModuleInterop": true, "skipLibCheck": true,
    "sourceMap": false, "types": ["node"]
  },
  "include": ["electron/**/*.ts"]
}
```

O `tsconfig.json` raiz é o do `ng new` (strict, `module: preserve`) e **não** inclui `electron/`.

### angular.json

Gere com `ng new <name> --style=scss --routing --ssr=false` e ajuste em `build.configurations.production`:

```json
"optimization": { "scripts": true, "styles": { "minify": true, "inlineCritical": false }, "fonts": true },
"budgets": [
  { "type": "initial", "maximumWarning": "2MB", "maximumError": "4MB" },
  { "type": "anyComponentStyle", "maximumWarning": "8kB", "maximumError": "16kB" }
]
```

`inlineCritical: false` é obrigatório (Armadilha 2). O budget inicial é maior porque as fontes vão embutidas no CSS.

## 4. Processo principal

### electron/main.ts

```ts
import { app, BrowserWindow, dialog, ipcMain, protocol, shell } from 'electron';
import * as fs from 'fs';
import * as path from 'path';
import * as settings from './settings';

let mainWindow: BrowserWindow | null = null;
const isDev = !app.isPackaged;
const RENDERER_ROOT = path.join(__dirname, '..', 'dist', '<name>', 'browser');

// Precisa rodar ANTES do app.whenReady().
protocol.registerSchemesAsPrivileged([
  { scheme: 'app', privileges: { standard: true, secure: true, supportFetchAPI: true, stream: true } },
]);

const MIME_TYPES: Record<string, string> = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.map': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg',
  '.gif': 'image/gif', '.ico': 'image/x-icon', '.woff': 'font/woff', '.woff2': 'font/woff2',
  '.ttf': 'font/ttf', '.txt': 'text/plain; charset=utf-8',
};

const CSP = [
  "default-src 'self' app:", "script-src 'self' app:", "style-src 'self' app: 'unsafe-inline'",
  "font-src 'self' app: data:", "img-src 'self' app: data: blob:", "connect-src 'self' app:",
  "object-src 'none'", "base-uri 'self'", "form-action 'none'",
].join('; ');

function registerAppProtocol(): void {
  protocol.handle('app', async (request) => {
    let pathname = decodeURIComponent(new URL(request.url).pathname);
    if (pathname === '/' || pathname === '') pathname = '/index.html';
    const filePath = path.normalize(path.join(RENDERER_ROOT, pathname));
    if (!filePath.startsWith(RENDERER_ROOT)) return new Response('Forbidden', { status: 403 });

    const ext = path.extname(filePath).toLowerCase();
    const serve = async (target: string, targetExt: string): Promise<Response> => {
      const data = await fs.promises.readFile(target);
      return new Response(new Uint8Array(data), {
        status: 200,
        headers: {
          'Content-Type': MIME_TYPES[targetExt] ?? 'application/octet-stream',
          'Content-Security-Policy': CSP,
          'Cache-Control': 'no-cache',
        },
      });
    };
    try {
      return await serve(filePath, ext);
    } catch (err) {
      // Asset ausente = 404 de verdade. Só caminho sem extensão cai no index.html.
      if (ext !== '') {
        console.error('[app://] asset não encontrado:', pathname, err);
        return new Response('Not Found', { status: 404 });
      }
      return await serve(path.join(RENDERER_ROOT, 'index.html'), '.html');
    }
  });
}

function createWindow(): void {
  mainWindow = new BrowserWindow({
    width: 1440, height: 900, minWidth: 1100, minHeight: 700,
    show: false, autoHideMenuBar: true, title: '<Product Name>', backgroundColor: '#fafafa',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true, nodeIntegration: false, sandbox: false, spellcheck: false,
    },
  });
  void mainWindow.loadURL('app://local/index.html');
  mainWindow.once('ready-to-show', () => mainWindow?.show());
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('http://') || url.startsWith('https://')) void shell.openExternal(url);
    return { action: 'deny' };
  });
  mainWindow.on('closed', () => (mainWindow = null));
  if (isDev) mainWindow.webContents.openDevTools({ mode: 'detach' });
}

function registerIpc(): void {
  ipcMain.handle('app:getVersion', () => app.getVersion());
  ipcMain.handle('settings:load', () => settings.loadData());
  ipcMain.handle('settings:save', (event, data: settings.AppData) => {
    settings.saveData(data);
    for (const win of BrowserWindow.getAllWindows()) {
      if (win.webContents.id !== event.sender.id) win.webContents.send('settings:changed', data);
    }
  });
  ipcMain.handle('file:saveText', async (_e, defaultName: string, content: string) => {
    const res = await dialog.showSaveDialog(mainWindow!, { title: 'Salvar arquivo', defaultPath: defaultName });
    if (res.canceled || !res.filePath) return { canceled: true };
    fs.writeFileSync(res.filePath, content, 'utf-8');
    return { canceled: false, path: res.filePath };
  });
  // + handlers de cada domínio, agrupados por comentário de seção
}

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (!mainWindow) return;
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.show();
    mainWindow.focus();
  });
  app.whenReady().then(() => {
    try { settings.migrateIfNeeded(); } catch (err) { console.error('[settings] falha ao migrar:', err); }
    // SQLite/Postgres: inicializar o banco aqui (seções 7 e 8), antes de registerIpc().
    registerAppProtocol();
    registerIpc();
    createWindow();
  });
  app.on('window-all-closed', () => app.quit());
}
```

### electron/preload.ts

```ts
import { contextBridge, ipcRenderer, type IpcRendererEvent } from 'electron';

const api = {
  app: { getVersion: () => ipcRenderer.invoke('app:getVersion') },
  settings: {
    load: () => ipcRenderer.invoke('settings:load'),
    save: (data: unknown) => ipcRenderer.invoke('settings:save', data),
    onChanged: (listener: (data: unknown) => void) => {
      const handler = (_e: IpcRendererEvent, data: unknown) => listener(data);
      ipcRenderer.on('settings:changed', handler);
      return () => ipcRenderer.off('settings:changed', handler);
    },
  },
  file: {
    saveText: (defaultName: string, content: string) => ipcRenderer.invoke('file:saveText', defaultName, content),
  },
};

contextBridge.exposeInMainWorld('<camelName>', api);
```

Regras do IPC:

- Canal sempre `dominio:acao`; sempre `ipcMain.handle` + `ipcRenderer.invoke` (Promise). `send/on` só para eventos main -> renderer, e o `on*` do preload devolve a função de remoção.
- O preload nunca expõe `ipcRenderer` cru nem um canal genérico (`invoke(channel, ...)`, `db:query(sql)`). Cada operação é uma função nomeada.
- Segredo (senha, token, string de conexão) entra pelo IPC e **nunca volta**: o renderer recebe só `hasX: boolean` e metadados.
- Valide argumentos no handler (`typeof`, `Array.isArray`): o renderer não é confiável.

### src/types/api.d.ts

Espelha o preload com tipos reais (o preload usa `unknown`) e termina com:

```ts
declare global {
    interface Window { <camelName>: <PascalName>Api; }
}
```

Os tipos de dados ficam duplicados entre `electron/*.ts` e `src/types/api.d.ts` de propósito: são dois programas TypeScript com tsconfig diferentes. Ao mudar um, mude o outro no mesmo commit.

## 5. Renderer (Angular)

### core/electron.service.ts

```ts
@Injectable({ providedIn: "root" })
export class ElectronService {
    get api(): <PascalName>Api {
        const api = window.<camelName>;
        if (!api) throw new Error("API do Electron indisponível. Execute o app pelo Electron, não pelo navegador.");
        return api;
    }
}

/** Remove o prefixo técnico que o IPC do Electron adiciona às mensagens de erro. */
export function cleanIpcError(message: string): string {
    return message.replace(/^Error invoking remote method '[^']+':\s*(Error:\s*)?/, "");
}
```

Todo `catch` de chamada IPC que mostra mensagem ao usuário passa por `cleanIpcError`.

### app.config.ts

```ts
export const appConfig: ApplicationConfig = {
    providers: [
        provideBrowserGlobalErrorListeners(),
        provideRouter(routes, withHashLocation()),
        { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: "outline", subscriptSizing: "dynamic" } },
    ],
};
```

### Shell e rotas

- `app.routes.ts`: toda tela com `loadComponent` (lazy) e `title`; `{ path: "**", redirectTo: "" }` no fim.
- `app.ts`: `mat-sidenav-container` com lista `NAV_ITEMS` (`path`, `label`, `icon`); `BreakpointObserver` em `(max-width: 1024px)` alterna a sidenav entre `side` e `over`; toolbar com título da página e botão de tema; `<app-status-overlay />` no fim.
- Tema escuro: `effect` que faz `document.documentElement.classList.toggle("dark-mode", config().isDarkMode)`.
- Versão no rodapé da sidenav vem de `api.app.getVersion()`, nunca texto fixo no HTML.
- `StatusService` (signal `{ kind: idle|loading|success|error|warning, message }` + `cancel()`) e `StatusOverlayComponent` (backdrop com spinner e botão cancelar; snackbar para error/warning/success).

### styles.scss

```scss
@use "@angular/material" as mat;
@use "@angular/material/core/theming/typography" as mat-typography;
@use "fonts";

html {
    color-scheme: light;
    @include mat.theme((color: (primary: mat.$azure-palette, tertiary: mat.$violet-palette), typography: Roboto, density: -2));
}
html.dark-mode { color-scheme: dark; }
@include mat.system-classes();
html, body { height: 100%; margin: 0; }
body {
    @include mat-typography.body-medium();
    background: var(--mat-sys-surface-container-low);
    color: var(--mat-sys-on-surface);
}
h1 { @include mat-typography.headline-small(); }
h2 { @include mat-typography.title-large(); }
h3 { @include mat-typography.title-medium(); }
p, label { @include mat-typography.body-medium(); }
small { @include mat-typography.body-small(); }
input, textarea, select, button { font-family: inherit; }
```

Mais as classes de layout globais reutilizadas por todas as telas: `.page`, `.page-header`, `.page-title`, `.page-subtitle`, `.filters-grid`, `.actions-row`, `.spacer`, `.full-width`, `.table-container` (overflow + header sticky), `.empty-state`. CSS por componente fica mínimo; cor e tamanho sempre por token `--mat-sys-*`, nunca valor cravado.

### scripts/generate-fonts.mjs

```js
import { readFileSync, writeFileSync } from "node:fs";
const b64 = (p) => readFileSync(new URL(`../node_modules/${p}`, import.meta.url)).toString("base64");
const face = (family, weight, display, file) =>
    `@font-face {\n    font-family: "${family}";\n    font-style: normal;\n    font-weight: ${weight};\n` +
    `    font-display: ${display};\n    src: url(data:font/woff2;base64,${b64(file)}) format("woff2");\n}\n`;
const icons =
    `.material-icons {\n    font-family: "Material Icons";\n    font-weight: normal;\n    font-style: normal;\n` +
    `    font-size: 24px;\n    line-height: 1;\n    letter-spacing: normal;\n    text-transform: none;\n` +
    `    display: inline-block;\n    white-space: nowrap;\n    word-wrap: normal;\n    direction: ltr;\n` +
    `    -webkit-font-smoothing: antialiased;\n    font-feature-settings: "liga";\n}\n`;
writeFileSync(
    new URL("../src/_fonts.scss", import.meta.url),
    "// GERADO AUTOMATICAMENTE por scripts/generate-fonts.mjs — não editar à mão.\n\n" +
        [
            face("Material Icons", 400, "block", "material-icons/iconfont/material-icons.woff2"),
            ...[400, 500, 700].map((w) => face("Roboto", w, "swap", `@fontsource/roboto/files/roboto-latin-${w}-normal.woff2`)),
            icons,
        ].join("\n"),
);
```

Rode `npm run fonts` uma vez e commite o `_fonts.scss`.

### Convenções Angular

- Standalone sempre; não escrever `standalone: true` nem `changeDetection: OnPush` (são o padrão).
- Estado em `signal`/`computed`; `input()`, `output()`, `model()`; `inject()` em vez de construtor.
- Controle de fluxo nativo (`@if`, `@for` com `track`); `class`/`style` bindings em vez de `ngClass`/`ngStyle`; `host: {}` em vez de `@HostBinding`/`@HostListener`.
- Membros usados só pelo template são `protected`; os imutáveis são `readonly`.
- Reactive Forms com `fb.nonNullable.group`. Diálogos em `MatDialog`, largura `min(Xpx, 96vw)`.
- Tabelas `mat-table`, paginação `mat-paginator`, filtros `mat-chip`. Todo botão de ícone tem `aria-label`.
- Sem `any`; `unknown` quando o tipo é incerto.
- Prettier só em `./src`: 4 espaços, aspas duplas, largura 100, `endOfLine: crlf`. `electron/` usa 2 espaços e aspas simples.

## 6. Persistência JSON local (padrão)

É a forma do Infinite.Tools e a escolha quando não houver banco. Com SQLite/Postgres ela continua existindo, mas só para preferências locais.

`electron/settings.ts`:

```ts
export const SCHEMA_VERSION = 1;
export interface AppConfig { isDarkMode: boolean }
export interface AppData { schemaVersion: number; config: AppConfig /* + coleções do domínio */ }
const DEFAULT_DATA: AppData = { schemaVersion: SCHEMA_VERSION, config: { isDarkMode: false } };

export const settingsPath = () => path.join(app.getPath('userData'), 'settings.json');

/** Whitelist: campo novo do AppData precisa ser lido aqui, senão é gravado e descartado na leitura seguinte. */
function normalize(parsed: Partial<AppData>): AppData {
  return { schemaVersion: SCHEMA_VERSION, config: { ...DEFAULT_DATA.config, ...(parsed.config ?? {}) } };
}

export function loadData(): AppData {
  try { return normalize(JSON.parse(fs.readFileSync(settingsPath(), 'utf-8'))); }
  catch { return structuredClone(DEFAULT_DATA); }
}

export function saveData(data: AppData): void {
  const file = settingsPath();
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const tmp = file + '.tmp';                       // gravação atômica: tmp + rename
  fs.writeFileSync(tmp, JSON.stringify({ ...data, schemaVersion: SCHEMA_VERSION }, null, 2), 'utf-8');
  fs.renameSync(tmp, file);
}
```

Regras que vêm junto:

- **`migrateIfNeeded()`** no boot: se `schemaVersion` do arquivo for menor que o atual, copia para `settings.backup-v<n>-<timestamp>.json` e só então regrava normalizado. Se o backup falhar, aborta a migração. Arquivo ilegível não é tocado.
- **Instância única** (`requestSingleInstanceLock`): cada gravação escreve o snapshot inteiro; dois processos se sobrescreveriam.
- **Várias janelas**: `fs.watch` no **diretório** do `userData` (não no arquivo: o `rename` troca o inode e o watcher do arquivo morre), debounce de ~120 ms, evento `settings:changed` para todas as janelas menos a que gravou. Só implemente se o app abrir mais de uma janela ou se o arquivo for editado por fora.
- **Um arquivo por domínio pesado**: dado que cresce rápido ou é gravado a cada tecla (ex.: `flows.json`) fica em arquivo próprio com o mesmo padrão (`normalize` whitelist, gravação atômica, `schemaVersion` próprio) e debounce de ~600 ms no renderer.
- **Segredos** não entram no `settings.json`: arquivo próprio, cifrado com `safeStorage` (seção 8 mostra o padrão), fora de export/import.
- `SettingsService` no renderer: um `signal` por campo, `applyData()` para carregar, `snapshot()` para montar o objeto, `persist()` chamando `settings.save(snapshot())`; assina `onChanged` uma única vez.

Limite honesto: isto é last-write-wins de arquivo inteiro. Serve bem para um usuário, uma máquina, até alguns MB. Consulta relacional, volume grande ou dado compartilhado entre máquinas pedem SQLite ou Postgres.

## 7. Persistência SQLite

- Dependência: `better-sqlite3` (+ `@types/better-sqlite3` em dev). É módulo nativo, então:
  - `"postinstall": "electron-builder install-app-deps"` em `scripts` (recompila para a ABI do Electron; sem isso `npm run electron` falha com `NODE_MODULE_VERSION`);
  - `"asarUnpack": ["**/node_modules/better-sqlite3/**"]` em `build` (um `.node` não carrega de dentro do asar).
- Arquivo: `path.join(app.getPath('userData'), '<name>.db')`.

`electron/db/sqlite.ts`:

```ts
import Database from 'better-sqlite3';

const MIGRATIONS: string[] = [
  // índice + 1 = versão. Nunca edite uma entrada já publicada: acrescente outra.
  `CREATE TABLE exemplo (id INTEGER PRIMARY KEY, nome TEXT NOT NULL, criado_em TEXT NOT NULL DEFAULT (datetime('now')));`,
];

let db: Database.Database | null = null;

export function openDatabase(): Database.Database {
  if (db) return db;
  const file = dbPath();
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const conn = new Database(file);
  conn.pragma('journal_mode = WAL');
  conn.pragma('foreign_keys = ON');

  const current = conn.pragma('user_version', { simple: true }) as number;
  if (current < MIGRATIONS.length) {
    if (current > 0) {
      conn.pragma('wal_checkpoint(TRUNCATE)');
      fs.copyFileSync(file, `${file}.backup-v${current}-${Date.now()}`);
    }
    conn.transaction(() => {
      for (let v = current; v < MIGRATIONS.length; v++) conn.exec(MIGRATIONS[v]);
      conn.pragma(`user_version = ${MIGRATIONS.length}`);
    })();
  }
  db = conn;
  return conn;
}

export function closeDatabase(): void { db?.close(); db = null; }
```

- `openDatabase()` no `whenReady` antes de `registerIpc()`; `closeDatabase()` em `app.on('will-quit')`.
- Acesso em `electron/repositories/<entidade>.ts`: funções com SQL parametrizado (`db.prepare('... WHERE id = ?').get(id)`); nunca concatenar valor na SQL.
- IPC por operação (`clientes:list`, `clientes:save`, `clientes:remove`), não snapshot inteiro. O service do renderer mantém um `signal` e recarrega depois de gravar.
- `better-sqlite3` é síncrono e bloqueia o processo principal: consulta pesada precisa de índice, paginação ou worker.
- Mantém a instância única. Datas em ISO-8601 texto; booleano em `INTEGER` 0/1.
- Alternativa sem módulo nativo: `node:sqlite` embutido no Node. Só adote depois de confirmar que `require('node:sqlite')` funciona na versão do Electron do projeto; não foi validado no Infinite.Tools.

## 8. Persistência PostgreSQL

Dependência: `pg` (+ `@types/pg` em dev). JavaScript puro: sem rebuild, sem `asarUnpack`.

### 8.1 Schema

`electron/db/schema.ts` exporta a constante com a resposta da pergunta 3:

```ts
export const DB_SCHEMA = '<schema>';
if (!/^[a-z_][a-z0-9_]{0,62}$/.test(DB_SCHEMA)) throw new Error('Nome de schema inválido.');
```

Identificador não aceita parâmetro (`$1`), então o nome é interpolado em DDL: por isso a validação é obrigatória e o valor é constante de código, nunca entrada do usuário em runtime.

### 8.2 String de conexão: armazenamento

`electron/database-config.ts`, no padrão de token do Infinite.Tools:

- Arquivo `database.json` no `userData`: `{ schemaVersion, connectionStringEnc: string | null }`, gravação atômica.
- Cifra com `safeStorage.encryptString(cs).toString('base64')` (DPAPI no Windows, chave da conta logada); lê com `safeStorage.decryptString(Buffer.from(enc, 'base64'))`. Falha ao decifrar (arquivo copiado de outra máquina/usuário) = tratar como não configurado.
- Se `safeStorage.isEncryptionAvailable()` for `false`, recuse gravar e avise na tela. Não grave em texto puro.
- Fica fora do `settings.json` e fora de qualquer export/import.

Aceite os dois formatos de string:

```ts
import type { PoolConfig } from 'pg';

export function parseConnectionString(cs: string): PoolConfig {
  const value = cs.trim();
  if (/^postgres(ql)?:\/\//i.test(value)) return { connectionString: value };

  // Estilo Npgsql: Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require
  const parts = new Map<string, string>();
  for (const piece of value.split(';')) {
    const idx = piece.indexOf('=');
    if (idx > 0) parts.set(piece.slice(0, idx).trim().toLowerCase(), piece.slice(idx + 1).trim());
  }
  const host = parts.get('host') ?? parts.get('server');
  const database = parts.get('database') ?? parts.get('db');
  const user = parts.get('username') ?? parts.get('user id') ?? parts.get('user') ?? parts.get('uid');
  const password = parts.get('password') ?? parts.get('pwd');
  if (!host || !database || !user || !password) {
    throw new Error('Connection string inválida. Esperado: "Host=...;Port=5432;Database=...;Username=...;Password=..."');
  }
  const sslMode = (parts.get('ssl mode') ?? parts.get('sslmode') ?? '').toLowerCase().replace(/[-_ ]/g, '');
  return {
    host, database, user, password,
    port: Number(parts.get('port') ?? 5432),
    ssl: sslMode === 'require' ? { rejectUnauthorized: false }
       : sslMode === 'verifyca' || sslMode === 'verifyfull' ? true
       : undefined,
  };
}
```

### 8.3 Pool e migrations

`electron/db/postgres.ts`:

```ts
import { Pool, type PoolConfig } from 'pg';
import { DB_SCHEMA } from './schema';

const MIGRATIONS: Array<{ id: number; name: string; sql: string }> = [
  // Tabelas sem qualificar schema: o search_path do pool resolve. Nunca edite uma entrada publicada.
  { id: 1, name: 'inicial', sql: `CREATE TABLE exemplo (id bigserial PRIMARY KEY, nome text NOT NULL, criado_em timestamptz NOT NULL DEFAULT now());` },
];

let pool: Pool | null = null;

export async function connect(config: PoolConfig): Promise<void> {
  const next = new Pool({
    ...config, max: 5, connectionTimeoutMillis: 8000, idleTimeoutMillis: 30000,
    application_name: '<name>',
    options: `-c search_path=${DB_SCHEMA}`,
  });
  next.on('error', (err) => console.error('[pg] erro em conexão ociosa:', err.message));
  try {
    await migrate(next);
  } catch (err) {
    await next.end().catch(() => undefined);
    throw err;
  }
  const previous = pool;
  pool = next;
  await previous?.end().catch(() => undefined);
}

export function getPool(): Pool {
  if (!pool) throw new Error('Banco de dados não configurado. Informe a string de conexão em Configurações.');
  return pool;
}

export async function disconnect(): Promise<void> { await pool?.end().catch(() => undefined); pool = null; }

async function migrate(target: Pool): Promise<void> {
  const client = await target.connect();
  const lockKey = `<name>:migrations:${DB_SCHEMA}`;
  try {
    // Duas máquinas abrindo o app ao mesmo tempo não podem migrar em paralelo.
    await client.query('SELECT pg_advisory_lock(hashtext($1))', [lockKey]);
    // Checa antes de criar: CREATE SCHEMA IF NOT EXISTS exige privilégio CREATE mesmo se o schema já existir.
    const exists = await client.query('SELECT 1 FROM pg_namespace WHERE nspname = $1', [DB_SCHEMA]);
    if (exists.rowCount === 0) await client.query(`CREATE SCHEMA "${DB_SCHEMA}"`);
    await client.query(
      `CREATE TABLE IF NOT EXISTS "${DB_SCHEMA}".schema_migrations (id integer PRIMARY KEY, name text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now())`,
    );
    const applied = new Set((await client.query(`SELECT id FROM "${DB_SCHEMA}".schema_migrations`)).rows.map((r) => r.id as number));
    for (const m of MIGRATIONS) {
      if (applied.has(m.id)) continue;
      await client.query('BEGIN');
      try {
        await client.query(m.sql);
        await client.query(`INSERT INTO "${DB_SCHEMA}".schema_migrations (id, name) VALUES ($1, $2)`, [m.id, m.name]);
        await client.query('COMMIT');
      } catch (err) {
        await client.query('ROLLBACK');
        throw new Error(`Migration ${m.id} (${m.name}) falhou: ${(err as Error).message}`);
      }
    }
  } finally {
    await client.query('SELECT pg_advisory_unlock(hashtext($1))', [lockKey]).catch(() => undefined);
    client.release();
  }
}
```

No `whenReady`: se houver string salva, `await connect(parseConnectionString(cs))` dentro de `try/catch`; em falha, guarde a mensagem em `lastError` e **abra a janela mesmo assim** (banco fora do ar não pode impedir o usuário de chegar à tela onde corrige a conexão). `disconnect()` em `will-quit`.

Repositórios em `electron/repositories/<entidade>.ts` usando `getPool().query('... WHERE id = $1', [id])`. IPC por operação, como no SQLite. Operação com mais de um comando usa `client` do pool com `BEGIN/COMMIT/ROLLBACK` e `release()` no `finally`.

### 8.4 Seção "Banco de dados" em Configurações (obrigatória)

Canais IPC (e funções equivalentes em `api.database` no preload e no `api.d.ts`):

| Canal | Entrada | Saída |
|---|---|---|
| `database:getStatus` | — | `DatabaseStatus` |
| `database:test` | `connectionString` | `{ elapsedMs, serverVersion }` (abre conexão avulsa, `SELECT version()`, fecha) |
| `database:setConnectionString` | `connectionString` | `DatabaseStatus` (conecta + migra; **só grava se der certo**) |
| `database:clear` | — | `DatabaseStatus` (desconecta e apaga) |

```ts
export interface DatabaseStatus {
    configured: boolean;
    connected: boolean;
    schema: string;
    /** Só o que não é segredo, para o usuário reconhecer a conexão. */
    summary: { host: string; port: number; database: string; user: string } | null;
    encryptionAvailable: boolean;
    lastError: string | null;
}
```

A string **nunca** volta para o renderer, nem mascarada.

Na tela `features/settings/settings-page`, um `mat-card` "Banco de dados (PostgreSQL)" com:

- chip de status (Conectado / Não configurado / Erro, com `lastError`), resumo `user@host:port/database` e o schema em uso (somente leitura);
- campo `type="password"` para a string, com hint do formato aceito (`Host=...;Port=5432;Database=...;Username=...;Password=...` ou `postgres://...`); sempre abre vazio;
- botões **Testar conexão**, **Salvar e conectar**, **Remover**; loading e erro pelo `StatusService`, mensagem por `cleanIpcError`;
- aviso se `encryptionAvailable` for `false`.

Enquanto `configured` ou `connected` for `false`, o shell redireciona para Configurações e as telas que dependem do banco mostram estado vazio com link para lá, em vez de erro cru.

### 8.5 Pontos de atenção

- O usuário do banco precisa de `USAGE` + `CREATE` no schema (ou `CREATE` no database se o schema ainda não existir). Para gerar login e grants use a skill `postgres-permissoes-api`, se disponível.
- `options: -c search_path=...` não funciona atrás de PgBouncer em modo transaction. Nesse caso, qualifique as tabelas com o schema nas queries.
- Cada máquina guarda credencial de banco e fala direto com o Postgres. Aceitável para ferramenta interna em rede confiável; para app distribuído a terceiros o caminho correto é uma API no meio.
- A instância única deixa de ser requisito de integridade (o banco arbitra), mas continua valendo por causa do `settings.json`.

## 9. Adicionar uma tela/feature

1. `electron/<dominio>.ts`: lógica de Node/SO/banco, com tipos exportados.
2. `electron/main.ts`: `ipcMain.handle('<dominio>:<acao>', ...)` em `registerIpc()`.
3. `electron/preload.ts`: funções em `api.<dominio>`.
4. `src/types/api.d.ts`: tipos de dados + assinatura em `<PascalName>Api`; reexporte em `core/models.ts`.
5. `src/app/core/<dominio>.service.ts` se houver estado compartilhado; senão a tela chama `ElectronService` direto.
6. `src/app/features/<tela>/<tela>.ts` (+ `.html`/`.scss` quando o template passar de ~80 linhas).
7. Rota lazy em `app.routes.ts` e item em `NAV_ITEMS`.
8. Suba a versão no `package.json` (patch = correção, minor = tela/feature nova, major = quebra de formato persistido) e registre no README.

Lógica que não precisa de Node (parsers, diff, formatação, busca) fica como função pura em `src/app/core/`, não no processo principal.

## 10. Armadilhas (cada uma já custou um bug)

1. **Nunca carregar por `file://`.** Origem opaca: o CSP `'self'` não casa com nada e a fonte de ícones é bloqueada (ícone vira texto: "settings", "code"). Sempre `app://local/index.html` via `protocol.handle`.
2. **`inlineCritical: false`.** O build de produção adia o CSS principal com `<link media="print" onload=...>`; o `onload` inline é bloqueado pelo CSP e a folha principal nunca é aplicada.
3. **Fontes como `data:` URI** no `_fonts.scss`, com `font-src ... data:` no CSP. Requisição separada de fonte falhava silenciosamente.
4. **CSP no header da resposta `app://`**, não em `<meta>`. `connect-src 'self' app:` impede o renderer de fazer rede: toda chamada externa passa pelo processo principal.
5. **Asset ausente = 404.** Fallback cego para `index.html` entrega HTML no lugar de fonte/imagem e esconde o erro.
6. **Path traversal**: `path.normalize` + `startsWith(RENDERER_ROOT)` no handler.
7. **`withHashLocation()`**: recarregar a janela em rota interna continua funcionando.
8. **`h1`–`h6`, `p`, `label` não herdam o tema**: `mat.theme()` só estiliza componentes Material. Aplique os mixins de tipografia no `styles.scss`.
9. **`subscriptSizing: 'dynamic'`** global, senão cada form-field reserva ~22 px vazios.
10. **`normalize()` é whitelist**: campo novo esquecido ali some na leitura seguinte.
11. **`fs.watch` no diretório**, não no arquivo gravado por rename.
12. **Módulo nativo** precisa de `asarUnpack` e de rebuild para a ABI do Electron.
13. **Instalador NSIS para Windows se gera no Windows** (`npm run dist`).

## 11. Verificação antes de entregar

1. `npm install` (com SQLite, confira que o `postinstall` rodou sem erro).
2. `npm run build:all` sem erro nem estouro de budget.
3. `npm run electron`: a janela abre, os ícones aparecem como ícones, o console do DevTools não tem violação de CSP nem 404, alternar o tema persiste após reabrir.
4. Persistência: grave um registro, feche, reabra e confira. Postgres: teste string errada (erro legível, nada gravado), string certa (conecta, cria schema e `schema_migrations`), e abertura do app com o banco fora do ar (a janela abre em Configurações).
5. `npm run dist` gera `release/<ProductName>-Setup-<versão>.exe`; instale e repita o passo 3 no app instalado (é onde `asarUnpack` e caminhos errados aparecem).

Se não for possível executar algum passo no ambiente atual, diga qual ficou sem rodar em vez de declarar o projeto validado.