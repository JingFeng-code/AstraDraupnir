// Nuxt server 代理：把 /api/** 轉給 NUXT_API_BASE（預設本機 API 5080），保留 Cookie 與 query。
import { proxyRequest } from 'h3'

const DEFAULT_API_BASE = 'http://127.0.0.1:5080'

export default defineEventHandler((event) => {
  const base = (process.env.NUXT_API_BASE || DEFAULT_API_BASE).replace(/\/+$/, '')
  return proxyRequest(event, `${base}${event.path}`)
})
