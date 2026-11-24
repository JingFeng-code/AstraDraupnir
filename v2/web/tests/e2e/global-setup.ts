const apiBase = process.env.NUXT_API_BASE ?? 'http://127.0.0.1:5080'

/** 確認本機 API 可連線（未登入應回 401），避免 E2E 因後端未啟動而難以診斷。 */
export default async function globalSetup() {
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
