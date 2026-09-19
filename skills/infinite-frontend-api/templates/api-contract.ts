// Contrato de resposta das APIs Infinite (InfiniteApiController.Send).
// Independente de cliente HTTP: adapte a origem de `status`, `body` e `headers`
// ao axios, ofetch, fetch ou HttpClient do projeto.

export enum Effect {
  Success = 1,
  Created = 2,
  Error = 3,
  NotFound = 4,
  Invalid = 5,
  Forbidden = 6,
  Validation = 7,
}

/** Corpo de NotFound / Invalid / Forbidden / Error gerado pelo handler. */
export interface InfiniteErrorBody {
  title?: string | null
  message?: string | null
  effect: Effect
  errors?: { field: string, message: string }[]
  traceId?: string
}

/** 401/403 do pipeline de autenticação (application/problem+json). */
export interface AuthProblemBody {
  status: number
  title: string
  detail: string
}

/** 400 automático do ASP.NET: corpo fora do contrato (JSON inválido, campo desconhecido...). */
export interface ModelStateProblemBody {
  title: string
  status: number
  errors: Record<string, string[]>
  traceId?: string
}

export type ApiErrorKind =
  | 'regra'          // 400 Invalid: mostrar ao usuário
  | 'contrato'       // 400 do ASP.NET: bug de integração do front
  | 'nao-autenticado'
  | 'sem-permissao'
  | 'nao-encontrado'
  | 'validacao'
  | 'cancelado'
  | 'inesperado'

export interface ApiError {
  kind: ApiErrorKind
  status: number
  message: string
  /** Erros de campo do 422; chave = nome do campo no backend (PascalCase, com caminho). */
  fieldErrors: { field: string, message: string }[]
}

const MENSAGEM_GENERICA = 'Não foi possível concluir a operação. Tente novamente.'

export function lerErroApi(status: number, body: unknown): ApiError {
  const corpo = (body ?? {}) as Partial<InfiniteErrorBody & AuthProblemBody & ModelStateProblemBody>
  const mensagem = (corpo.message ?? corpo.detail ?? '') || MENSAGEM_GENERICA

  switch (status) {
    case 400:
      // O 400 do ASP.NET traz `errors` como objeto e não traz `effect`.
      if (corpo.effect === undefined)
        return { kind: 'contrato', status, message: MENSAGEM_GENERICA, fieldErrors: [] }
      return { kind: 'regra', status, message: mensagem, fieldErrors: [] }
    case 401:
      return { kind: 'nao-autenticado', status, message: mensagem, fieldErrors: [] }
    case 403:
      return { kind: 'sem-permissao', status, message: mensagem, fieldErrors: [] }
    case 404:
      return { kind: 'nao-encontrado', status, message: mensagem, fieldErrors: [] }
    case 422:
      return {
        kind: 'validacao',
        status,
        message: mensagem,
        fieldErrors: Array.isArray(corpo.errors) ? corpo.errors : [],
      }
    case 499:
      return { kind: 'cancelado', status, message: '', fieldErrors: [] }
    default:
      // O 500 do handler traz mensagem amigável (IHasFriendlyError); sem `effect`, use a genérica.
      return {
        kind: 'inesperado',
        status,
        message: corpo.effect !== undefined && corpo.message ? corpo.message : MENSAGEM_GENERICA,
        fieldErrors: [],
      }
  }
}

/** Erro 422 do campo do formulário, comparando sem diferenciar maiúsculas. */
export function erroDoCampo(erro: ApiError, campo: string): string | undefined {
  const alvo = campo.toLowerCase()
  return erro.fieldErrors.find(e => e.field.toLowerCase() === alvo)?.message
}

export interface Paginacao {
  total: number | null
  page: number
  pageSize: number
  totalPages: number | null
}

/**
 * Lê os headers de paginação. `total`/`totalPages` ficam null quando o navegador não os
 * expõe (chamada cross-origin sem Access-Control-Expose-Headers); não estime o total.
 */
export function lerPaginacao(
  headers: { get(nome: string): string | null },
  pedido: { page: number, pageSize: number },
): Paginacao {
  const numero = (nome: string) => {
    const valor = headers.get(nome)
    return valor === null || valor === '' ? null : Number(valor)
  }

  return {
    total: numero('X-Total-Count'),
    page: numero('X-Page') ?? pedido.page,
    pageSize: numero('X-Page-Size') ?? pedido.pageSize,
    totalPages: numero('X-Total-Pages'),
  }
}

export interface ListagemQuery {
  page?: number
  pageSize?: number
  search?: string
  sortField?: string
  sortOrder?: 'asc' | 'desc'
}

/** Remove filtros vazios antes de montar a query string. */
export function paramsDeListagem(query: ListagemQuery & Record<string, unknown>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(query)
      .filter(([, valor]) => valor !== undefined && valor !== null && valor !== '')
      .map(([chave, valor]) => [chave, String(valor)]),
  )
}
