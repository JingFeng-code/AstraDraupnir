import { toApiError } from '~/utils/errors'

type Query = Record<string, string | number | boolean | null | undefined>

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  query?: Query
}

/**
 * 呼叫 /api（Nuxt server 代理）。錯誤一律轉成 ApiError，訊息已翻成中文。
 */
export function useApi() {
  async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    try {
      return await $fetch<T>(path, {
        method: options.method ?? 'GET',
        body: options.body as Record<string, unknown> | undefined,
        query: options.query,
        credentials: 'include',
      })
    } catch (error) {
      throw toApiError(error)
    }
  }

  return {
    request,
    get: <T>(path: string, query?: Query) => request<T>(path, { query }),
    post: <T>(path: string, body?: unknown, query?: Query) => request<T>(path, { method: 'POST', body, query }),
    put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
    patch: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PATCH', body }),
    del: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  }
}
