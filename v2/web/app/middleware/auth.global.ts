// 全域登入檢查（SPA）。未登入導向 /login，登入後不留在登入頁。
export default defineNuxtRouteMiddleware(async (to) => {
  if (import.meta.server) return
  const auth = useAuth()
  if (!auth.state.value.ready) await auth.fetchMe()

  if (!auth.state.value.user && to.path !== '/login') {
    return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }
  if (auth.state.value.user && to.path === '/login') {
    return navigateTo('/')
  }
})
