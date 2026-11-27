<script setup lang="ts">
const route = useRoute()
const auth = useAuth()
const router = useRouter()
const ws = useWorkspace()
const themeApi = useTheme()
const loggingOut = ref(false)
const searchOpen = ref(false)
const menuOpen = ref(false)
const projectFilter = ref('')

const isLogin = computed(() => route.path === '/login')
const user = computed(() => auth.state.value.user)
const isAdmin = computed(() => user.value?.role === 'Admin')
const gates = computed(() => ws.pendingGates.value.length)

onMounted(() => {
  themeApi.init()
  window.addEventListener('keydown', onKey)
})
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
watch(() => user.value?.id, (id) => { if (id) ws.load() }, { immediate: true })
watch(() => route.fullPath, () => { menuOpen.value = false })

function onKey(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); if (user.value) searchOpen.value = true }
}
async function logout() {
  loggingOut.value = true
  try { await auth.logout(); await router.push('/login') } finally { loggingOut.value = false }
}

const nav = computed(() => [
  { to: '/', label: '總覽', icon: 'M3 10.5 12 3l9 7.5V21h-6v-6H9v6H3z', exact: true },
  { to: '/review', label: '審查與決策', icon: 'M4 5h16v11H8l-4 4zM8 9h8M8 12h5', count: gates.value },
  { to: '/trace', label: '追溯與品質', icon: 'M5 6a2 2 0 1 0 0 .01M12 12a2 2 0 1 0 0 .01M19 18a2 2 0 1 0 0 .01M7 7l3.5 3.5M14 13.5 17 16.5' },
  { to: '/agents', label: 'Agent', icon: 'M9 3h6v4H9zM5 9h14v10H5zM9 14h.01M15 14h.01' },
  { to: '/projects', label: '專案', icon: 'M3 6h7l2 2h9v11H3z' },
  ...(isAdmin.value ? [{ to: '/users', label: '使用者與權限', icon: 'M9 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM3 20c0-3.5 2.7-6 6-6s6 2.5 6 6M17 8h4M19 6v4' }, { to: '/audit', label: '稽核紀錄', icon: 'M6 3h9l4 4v14H6zM9 12h7M9 16h7M9 8h3' }] : []),
  { to: '/settings', label: '系統設定', icon: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19 12l2-1-1-3-2 .5-1.5-1.5L17 5l-3-1-1 2h-2L10 4 7 5l.5 2.5L6 9l-2-.5-1 3 2 1v2l-2 1 1 3 2-.5L7.5 20 7 22l3 1 1-2h2l1 2 3-1-.5-2.5 1.5-1.5 2 .5 1-3-2-1z' },
])
const active = (n: { to: string, exact?: boolean }) => (n.exact ? route.path === n.to : route.path === n.to || route.path.startsWith(`${n.to}/`))
const projectList = computed(() => {
  const q = projectFilter.value.trim().toLowerCase()
  return ws.data.value.projects.filter(p => !q || p.name.toLowerCase().includes(q) || p.key.toLowerCase().includes(q))
})
const projectId = computed(() => (route.path.startsWith('/projects/') ? (route.params.id as string) : ''))
const crumbProject = computed(() => ws.byId.value.projects.get(projectId.value))
</script>

<template>
  <div v-if="isLogin" class="ui-login-wrap"><slot /></div>
  <div v-else class="ui-app" :class="{ 'is-collapsed': themeApi.collapsed.value, 'is-menu': menuOpen }">
    <aside class="ui-side" aria-label="工作空間">
      <div class="ui-side-top">
        <NuxtLink to="/" class="ui-brand" aria-label="Draupnir 總覽">
          <svg class="ui-brand-mark" width="30" height="30" viewBox="0 0 30 30" aria-hidden="true">
            <rect x="1" y="1" width="28" height="28" rx="9" fill="currentColor" />
            <path d="M6 18h5" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-dasharray="1 4" />
            <path d="M13 18h5" stroke="#fff" stroke-width="2.4" stroke-linecap="round" />
            <path d="m23 9.5 1.3 2.7 3 .4-2.2 2.1.5 3-2.6-1.4-2.6 1.4.5-3-2.2-2.1 3-.4z" fill="#fff" />
          </svg>
          <span class="ui-label">Draupnir</span>
        </NuxtLink>
        <button type="button" class="ui-menu-btn ui-btn is-quiet" :aria-expanded="menuOpen" aria-controls="ui-nav-area" @click="menuOpen = !menuOpen">選單</button>
      </div>
      <div id="ui-nav-area" class="ui-nav-area">
        <button type="button" class="ui-search-btn" title="搜尋（Ctrl+K）" @click="searchOpen = true">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></svg>
          <span class="ui-label">搜尋…</span><kbd class="ui-kbd ui-label">Ctrl K</kbd>
        </button>
        <nav class="ui-nav" aria-label="主要">
          <NuxtLink v-for="n in nav" :key="n.to" :to="n.to" :class="{ 'is-on': active(n) }" :aria-current="active(n) ? 'page' : undefined" :title="n.label">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path :d="n.icon" /></svg>
            <span class="ui-label">{{ n.label }}</span>
            <span v-if="n.count" class="ui-count ui-label" :aria-label="`${n.count} 個 Human Gate 待決定`">{{ n.count }}</span>
          </NuxtLink>
        </nav>
        <div class="ui-projects ui-label">
          <p class="ui-nav-h">專案</p>
          <label class="sr-only" for="proj-filter">篩選專案</label>
          <input id="proj-filter" v-model="projectFilter" class="ui-input ui-filter" placeholder="篩選專案" autocomplete="off">
          <nav class="ui-nav ui-proj-list" aria-label="專案清單">
            <NuxtLink v-for="p in projectList" :key="p.id" :to="`/projects/${p.id}`" :class="{ 'is-on': projectId === p.id }" :aria-current="projectId === p.id ? 'page' : undefined">{{ p.name }}</NuxtLink>
            <p v-if="!projectList.length" class="ui-meta" style="padding: 6px 12px">沒有符合的專案</p>
          </nav>
        </div>
        <div class="ui-side-foot">
          <button type="button" class="ui-btn is-quiet" :aria-pressed="themeApi.theme.value === 'dark'" @click="themeApi.toggle()">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z" /></svg>
            <span class="ui-label">{{ themeApi.theme.value === 'dark' ? '深色主題' : '淺色主題' }}</span>
          </button>
          <button type="button" class="ui-btn is-quiet ui-collapse" :aria-label="themeApi.collapsed.value ? '展開側邊欄' : '收合側邊欄'" @click="themeApi.setCollapsed(!themeApi.collapsed.value)">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path :d="themeApi.collapsed.value ? 'm9 6 6 6-6 6' : 'm15 6-6 6 6 6'" /></svg>
            <span class="ui-label">收合</span>
          </button>
        </div>
      </div>
    </aside>

    <div class="ui-main">
      <header class="ui-top">
        <nav class="ui-crumb" aria-label="位置">
          <NuxtLink to="/">工作空間</NuxtLink>
          <template v-if="crumbProject"><span aria-hidden="true">/</span><NuxtLink :to="`/projects/${crumbProject.id}`" style="color: var(--c-ink); font-weight: 600">{{ crumbProject.name }}</NuxtLink></template>
        </nav>
        <div class="ui-top-r">
          <span v-if="user" class="ui-source ui-hide-sm">{{ user.email }}</span>
          <button v-if="user" type="button" class="ui-btn is-quiet" :disabled="loggingOut" @click="logout">{{ loggingOut ? '登出中…' : '登出' }}</button>
        </div>
      </header>
      <main id="main-content" class="ui-content" tabindex="-1">
        <slot />
      </main>
    </div>
    <p v-if="ws.toast.value" class="ui-toast" role="status" @click="ws.toast.value = null">{{ ws.toast.value }}</p>
    <AppSearch :open="searchOpen" @close="searchOpen = false" />
  </div>
</template>

<style>
.ui-login-wrap { min-height: 100vh; }
.ui-app.is-collapsed { grid-template-columns: 80px minmax(0, 1fr); }
.ui-app.is-collapsed .ui-label, .ui-app.is-collapsed .ui-projects { display: none; }
.ui-app.is-collapsed .ui-nav a, .ui-app.is-collapsed .ui-search-btn, .ui-app.is-collapsed .ui-side-foot .ui-btn { justify-content: center; padding: 0; }
.ui-app.is-collapsed .ui-nav a.is-on::before { left: -14px; }
.ui-side-top { display: flex; align-items: center; justify-content: space-between; }
.ui-menu-btn { display: none; }
.ui-side { overflow: hidden; }
.ui-nav-area { display: flex; flex-direction: column; gap: 4px; flex: 1; min-height: 0; }
.ui-search-btn { display: flex; align-items: center; gap: 10px; min-height: 42px; padding: 0 12px; border-radius: var(--radius-1); background: var(--c-surface); box-shadow: 0 0 0 1px var(--c-line); color: var(--c-ink-2); margin-bottom: 10px; }
.ui-search-btn:hover { box-shadow: 0 0 0 1px var(--c-line-strong); color: var(--c-ink); }
.ui-kbd { margin-left: auto; font-size: 12px; padding: 1px 6px; border-radius: 6px; background: var(--c-sunken); font-family: inherit; }
.ui-filter { width: 100%; margin: 0 0 6px; min-height: 38px; }
.ui-projects { display: flex; flex-direction: column; flex: 1; min-height: 0; }
.ui-proj-list { flex: 1; min-height: 0; overflow-y: auto; }
.ui-side-foot { flex: none; }
.ui-side-foot { margin-top: auto; padding: 12px 0 0; display: grid; gap: 4px; font-size: 15px; }
.ui-side-foot .ui-btn { justify-content: flex-start; }
.ui-hide-sm { }
@media (max-width: 960px) {
  .ui-app, .ui-app.is-collapsed { grid-template-columns: 1fr; }
  .ui-side { height: auto; position: static; padding: 10px 14px; overflow: visible; }
  .ui-proj-list { max-height: 30vh; flex: none; }
  .ui-menu-btn { display: inline-flex; }
  .ui-nav-area { display: none; }
  .ui-app.is-menu .ui-nav-area { display: flex; padding: 8px 0 12px; }
  .ui-app.is-collapsed .ui-label, .ui-app.is-collapsed .ui-projects { display: initial; }
  .ui-collapse { display: none !important; }
  .ui-hide-sm { display: none; }
}
</style>
