/**
 * Configuração da aplicação resolvida em tempo de build/dev pelo plugin Vite
 * `vite/app-config.ts` e exposta via módulo virtual `virtual:app-config`.
 *
 * É o MESMO contrato para a config local (desenvolvimento) e para a config de
 * runtime baixada do Consul na pipeline (`public/config/config.json`).
 *
 * Para adicionar um novo parâmetro tipado, inclua o campo aqui e preencha-o nos
 * arquivos `config/app-config.local.json` (dev) e na chave do Consul (build).
 * A index signature permite carregar parâmetros adicionais sem quebrar o build.
 */
export interface AppConfig {
  apiBaseUrl: string
  authApiBaseUrl: string

  // Novos parâmetros tipados entram acima desta linha.
  [key: string]: unknown
}
