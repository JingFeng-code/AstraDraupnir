<script setup lang="ts">
const route = useRoute()
const auth = useAuth()
const router = useRouter()
const loggingOut = ref(false)

const isLogin = computed(() => route.path === '/login')

async function logout() {
  loggingOut.value = true
  try {
    await auth.logout()
    await router.push('/login')
  } finally {
    loggingOut.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <header class="sticky top-0 z-40 border-b border-line bg-paper/95 backdrop-blur print:hidden">
      <div class="site-container flex min-h-16 flex-wrap items-center justify-between gap-3 py-2">
        <NuxtLink to="/projects" class="flex items-baseline gap-2 text-ink no-underline">
          <span class="text-lg font-bold tracking-wide">Draupnir</span>
          <span class="font-mono text-xs text-ink-subtle">V2</span>
          <span class="sr-only">（回首頁）</span>
        </NuxtLink>

        <nav v-if="auth.state.value.user" class="flex flex-wrap items-center gap-3 text-sm" aria-label="帳號導覽">
          <NuxtLink to="/projects" class="font-medium text-navy-700 hover:underline">專案</NuxtLink>
          <NuxtLink v-if="auth.state.value.user?.role === 'Admin'" to="/users" class="font-medium text-navy-700 hover:underline">使用者</NuxtLink>
          <span class="hidden text-ink-subtle sm:inline">{{ auth.state.value.user.email }}</span>
          <button type="button" class="btn btn-secondary" :disabled="loggingOut" @click="logout">
            {{ loggingOut ? '登出中…' : '登出' }}
          </button>
        </nav>
      </div>
    </header>

    <main id="main-content" class="flex-1" tabindex="-1">
      <slot />
    </main>

    <footer class="border-t border-line bg-surface-muted py-6 print:hidden">
      <div class="site-container flex flex-wrap items-center justify-between gap-2 text-xs text-ink-subtle">
        <p>AstraDraupnir V2 · 本機開發環境</p>
        <p v-if="isLogin">請使用管理員帳號登入</p>
      </div>
    </footer>
  </div>
</template>
