import { assertSafeE2e, readHealth } from './guard'

const apiBase = process.env.NUXT_API_BASE?.trim() ?? ''

/** 先通過 fail-closed 寫入保護，再確認本機 API 可連線（未登入應回 401）。 */
export default async function globalSetup() {
  if (!apiBase) {
    throw new Error('E2E 寫入保護：請設定 NUXT_API_BASE（例如 http://127.0.0.1:5088）。')
  }
  let health
  try {
    health = await readHealth(apiBase)
  } catch (error) {
    throw new Error(`E2E 寫入保護無法讀取 /api/health（${apiBase}）：${String(error)}`)
  }
  assertSafeE2e(process.env, health)

  try {
    const response = await fetch(`${apiBase}/api/auth/me`)
    if (response.status !== 401) {
      throw new Error(`unexpected status ${response.status}`)
    }
  } catch (error) {
    throw new Error(
      `Draupnir API 無法連線（${apiBase}）。請先啟動 PostgreSQL 與 API，見 v2/README.md。原始錯誤：${String(error)}`,
    )
  }
}
