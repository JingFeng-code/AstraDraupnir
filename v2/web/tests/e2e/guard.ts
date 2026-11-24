import fs from 'node:fs'
import path from 'node:path'

export interface GuardHealth {
  status: string
  database: string
  disposable: boolean
}

const loopback = new Set(['127.0.0.1', 'localhost', '[::1]', '::1'])

/**
 * Fail-closed before the first mutating E2E request. The E2E suite creates projects,
 * users, gates and evidence; it must never run against the shared preview database.
 * The API's /api/health endpoint reports its database and whether it is disposable.
 */
export function assertSafeE2e(
  env: Record<string, string | undefined>,
  health: GuardHealth | undefined,
): void {
  if (env.E2E_ALLOW_WRITE !== '1') {
    throw new Error('E2E 寫入保護：請明確設定 E2E_ALLOW_WRITE=1；E2E 會建立專案、使用者與 Gate。')
  }
  const base = env.NUXT_API_BASE?.trim()
  if (!base) {
    throw new Error('E2E 寫入保護：請設定 NUXT_API_BASE 指向本機可拋棄的 API。')
  }
  const stateDir = env.E2E_STATE_DIR?.trim()
  if (!stateDir) {
    throw new Error('E2E 寫入保護：請設定 E2E_STATE_DIR 指向可寫入的暫存目錄。')
  }
  const resolvedState = path.resolve(stateDir)
  fs.mkdirSync(resolvedState, { recursive: true })
  fs.accessSync(resolvedState, fs.constants.W_OK)

  let url: URL
  try {
    url = new URL(base)
  } catch {
    throw new Error(`E2E 寫入保護：NUXT_API_BASE 不是有效 URL（${base}）。`)
  }
  if (!loopback.has(url.hostname)) {
    throw new Error(`E2E 寫入保護：只允許 loopback API，收到 ${url.hostname}。`)
  }
  if (!health) {
    throw new Error('E2E 寫入保護：API 沒有回報 /api/health，無法確認資料庫是否可拋棄；拒絕執行。')
  }
  if (health.status !== 'ok') {
    throw new Error(`E2E 寫入保護：API 狀態為 ${health.status}。`)
  }
  if (!health.disposable || ['draupnir', 'draupnir_v21', 'draupnir_design'].includes(health.database)) {
    throw new Error(`E2E 寫入保護：拒絕在非拋棄式資料庫 ${health.database} 上寫入。`)
  }
}

export async function readHealth(base: string): Promise<GuardHealth> {
  const response = await fetch(`${base.replace(/\/$/, '')}/api/health`)
  if (!response.ok) throw new Error(`/api/health 回應 ${response.status}`)
  return await response.json() as GuardHealth
}
