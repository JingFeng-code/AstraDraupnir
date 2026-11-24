import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  vite: {
    plugins: [tailwindcss()],
    // node_modules may be a junction to a sibling worktree (local multi-worktree setup); let the dev server serve its fonts
    server: { fs: { strict: false } },
  },
  compatibilityDate: '2026-11-01',
  devtools: { enabled: false },
  // SPA：登入狀態與 Cookie 由瀏覽器直接處理，Nuxt server 只負責 /api 代理
  ssr: false,
  css: ['~/assets/css/main.css'],
  typescript: {
    tsConfig: {
      include: ['../tests/**/*'],
    },
  },
  app: {
    head: {
      htmlAttrs: { lang: 'zh-Hant-TW' },
      charset: 'utf-8',
      viewport: 'width=device-width, initial-scale=1',
      title: 'Draupnir V2 · Astra 專案交付平台',
      meta: [
        { name: 'description', content: 'AstraDraupnir V2：專案、需求、工作項目、Agent 執行與 Human Gate 的交付平台。' },
      ],
    },
  },
})
