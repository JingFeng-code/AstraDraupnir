import type { User } from '~/types/api'

interface AuthState {
  user: User | null
  ready: boolean
}

/** 登入狀態（SPA；Cookie 由瀏覽器保存，請求走 /api 代理）。 */
export function useAuth() {
  const state = useState<AuthState>('draupnir-auth', () => ({ user: null, ready: false }))
  const loading = useState<boolean>('draupnir-auth-loading', () => false)

  async function fetchMe(force = false): Promise<User | null> {
    if (state.value.ready && !force) return state.value.user
    loading.value = true
    try {
      state.value.user = await $fetch<User>('/api/auth/me', { credentials: 'include' })
    } catch {
      state.value.user = null
    } finally {
      state.value.ready = true
      loading.value = false
    }
    return state.value.user
  }

  async function login(email: string, password: string): Promise<User> {
    loading.value = true
    try {
      const user = await $fetch<User>('/api/auth/login', {
        method: 'POST',
        body: { email, password },
        credentials: 'include',
      })
      state.value.user = user
      state.value.ready = true
      return user
    } finally {
      loading.value = false
    }
  }

  async function logout(): Promise<void> {
    try {
      await $fetch('/api/auth/logout', { method: 'POST', credentials: 'include' })
    } finally {
      state.value.user = null
      state.value.ready = true
    }
  }

  return { state, loading, fetchMe, login, logout }
}
