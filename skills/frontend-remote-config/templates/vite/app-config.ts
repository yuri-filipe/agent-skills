import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import type { Plugin } from 'vite'

/**
 * Plugin Vite que injeta a configuração da aplicação como uma constante síncrona
 * através do módulo virtual `virtual:app-config`.
 *
 * - build (pipeline): lê `public/config/config.json`, que a pipeline baixa do
 *   Consul (`frontends/<app>/<env>/config.json`) ANTES do `npm run build`.
 *   Se o arquivo não existir (build local fora da pipeline), faz fallback para os
 *   defaults locais emitindo um aviso.
 * - serve (desenvolvimento): lê `config/app-config.local.json` (defaults de localhost).
 *
 * A config é serializada e embutida no bundle — sem fetch assíncrono em runtime e
 * sem resolução a cada request.
 */

const VIRTUAL_ID = 'virtual:app-config'
const RESOLVED_ID = `\0${VIRTUAL_ID}`

const CONSUL_CONFIG_PATH = 'public/config/config.json'
const LOCAL_CONFIG_PATH = 'config/app-config.local.json'

export function appConfig(): Plugin {
  let isBuild = false
  let root = process.cwd()

  return {
    name: 'app-config',

    configResolved(config) {
      isBuild = config.command === 'build'
      root = config.root
    },

    resolveId(id) {
      if (id === VIRTUAL_ID)
        return RESOLVED_ID
    },

    load(id) {
      if (id !== RESOLVED_ID)
        return

      const consulFile = resolve(root, CONSUL_CONFIG_PATH)
      const localFile = resolve(root, LOCAL_CONFIG_PATH)

      let sourceFile: string

      if (isBuild) {
        if (existsSync(consulFile)) {
          sourceFile = consulFile
        }
        else {
          this.warn(
            `[app-config] "${CONSUL_CONFIG_PATH}" não encontrado no build; `
            + `usando defaults locais ("${LOCAL_CONFIG_PATH}"). `
            + 'Na pipeline esse arquivo é gerado a partir do Consul.',
          )
          sourceFile = localFile
        }
      }
      else {
        sourceFile = localFile
      }

      if (!existsSync(sourceFile))
        this.error(`[app-config] arquivo de configuração não encontrado: ${sourceFile}`)

      const raw = readFileSync(sourceFile, 'utf-8')

      try {
        // Valida que é um JSON válido (lança em caso de erro).
        const parsed = JSON.parse(raw)

        return `export default ${JSON.stringify(parsed)}`
      }
      catch (error) {
        this.error(`[app-config] JSON inválido em ${sourceFile}: ${(error as Error).message}`)
      }
    },
  }
}
