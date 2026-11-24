<script setup lang="ts">
import type { User } from '~/types/api'

const auth = useAuth()
const ws = useWorkspace()
const themeApi = useTheme()
const router = useRouter()

// Local, package.json-independent labels. These describe the running mode, not a release artifact.
const APP_LABEL = 'Draupnir V2.1'
const BUILD_LABEL = '本機開發版（Direction D）'

const apiState = ref<'loading' | 'ok' | 'error'>('loading')
const apiDetail = ref('')
const loggingOut = ref(false)
const user = computed<User | null>(() => auth.state.value.user)

async function probeApi() {
  apiState.value = 'loading'
  apiDetail.value = ''
  try {
    await $fetch('/api/auth/me', { credentials: 'include' })
    apiState.value = 'ok'
  } catch (caught) {
    apiState.value = 'error'
    apiDetail.value = caught instanceof Error ? caught.message : '無法連線'
  }
}

async function logout() {
  loggingOut.value = true
  try {
    await auth.logout()
    ws.toast.value = '已登出'
    await router.push('/login')
  } finally {
    loggingOut.value = false
  }
}

onMounted(probeApi)
</script>

<template>
  <div>
    <h1 class="ui-h1">系統設定</h1>
    <p class="ui-lead">只調整這台瀏覽器的偏好；設定不會寫入後端或 Supervisor。</p>

    <section class="ui-panel settings-panel">
      <div class="ui-panel-h"><h2 class="ui-h2">外觀</h2><span class="ui-meta">儲存在本機瀏覽器</span></div>
      <div class="ui-panel-b settings-group">
        <p id="theme-label" class="ui-h3">主題</p>
        <div class="settings-choices" role="group" aria-labelledby="theme-label">
          <button type="button" class="btn" :class="themeApi.theme.value === 'dark' ? 'btn-primary' : 'btn-secondary'" :aria-pressed="themeApi.theme.value === 'dark'" @click="themeApi.setTheme('dark')">深色</button>
          <button type="button" class="btn" :class="themeApi.theme.value === 'light' ? 'btn-primary' : 'btn-secondary'" :aria-pressed="themeApi.theme.value === 'light'" @click="themeApi.setTheme('light')">淺色</button>
        </div>
        <p id="sidebar-label" class="ui-h3">側邊欄預設</p>
        <div class="settings-choices" role="group" aria-labelledby="sidebar-label">
          <button type="button" class="btn" :class="!themeApi.collapsed.value ? 'btn-primary' : 'btn-secondary'" :aria-pressed="!themeApi.collapsed.value" @click="themeApi.setCollapsed(false)">展開</button>
          <button type="button" class="btn" :class="themeApi.collapsed.value ? 'btn-primary' : 'btn-secondary'" :aria-pressed="themeApi.collapsed.value" @click="themeApi.setCollapsed(true)">收合</button>
        </div>
      </div>
    </section>

    <section class="ui-panel settings-panel">
      <div class="ui-panel-h"><h2 class="ui-h2">目前帳號</h2></div>
      <div class="ui-panel-b settings-account">
        <p v-if="!user" class="ui-meta">尚未登入。</p>
        <template v-else>
          <p class="settings-value">{{ user.email }}</p>
          <p class="ui-meta">角色：<span class="ui-tag">{{ user.role }}</span></p>
        </template>
        <button type="button" class="btn btn-secondary" :disabled="loggingOut" @click="logout">{{ loggingOut ? '登出中…' : '登出' }}</button>
      </div>
    </section>

    <section class="ui-panel settings-panel">
      <div class="ui-panel-h"><h2 class="ui-h2">環境資訊</h2></div>
      <div class="ui-panel-b settings-env">
        <p class="ui-meta">{{ APP_LABEL }} · {{ BUILD_LABEL }}。目前為本機環境，沒有部署到正式服務。</p>
        <AppLoading v-if="apiState === 'loading'" label="正在檢查 API…" />
        <AppNotice v-else-if="apiState === 'error'" :message="`API 無法連線：${apiDetail}`">
          <button type="button" class="btn btn-secondary" @click="probeApi">重試</button>
        </AppNotice>
        <p v-else class="ui-meta">API 可達（透過本機 <code>/api</code> 代理）。</p>
      </div>
    </section>

    <section class="ui-panel settings-panel">
      <div class="ui-panel-h"><h2 class="ui-h2">資料來源與誠實說明</h2></div>
      <div class="ui-panel-b settings-notes">
        <p class="ui-meta">從 Supervisor 匯入的 Agent 執行紀錄是「快照」，不是目前正在進行的狀態；畫面會標示來源與記錄時間。</p>
        <p class="ui-meta">在 Draupnir 做出的 Human Gate 決定只記錄在本機資料庫，<strong>不會同步回 Supervisor</strong>。</p>
        <p class="ui-meta">標示「測試資料」的紀錄是模擬資料；稽核紀錄是 API 的真實操作紀錄（最多最近 200 筆）。</p>
      </div>
    </section>

    <section class="ui-panel settings-panel">
      <div class="ui-panel-h"><h2 class="ui-h2">鍵盤快速鍵</h2></div>
      <div class="ui-panel-b settings-shortcuts">
        <p><kbd class="ui-kbd">Ctrl</kbd> + <kbd class="ui-kbd">K</kbd><span class="ui-meta">開啟全站搜尋</span></p>
        <p><kbd class="ui-kbd">j</kbd> / <kbd class="ui-kbd">k</kbd><span class="ui-meta">在「審查與決策」往下／往上移動</span></p>
        <p><kbd class="ui-kbd">Esc</kbd><span class="ui-meta">關閉對話框</span></p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.settings-panel { margin-top: 24px; }
.settings-group { display: grid; gap: 10px; }
.settings-choices { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 10px; }
.settings-account { display: grid; gap: 8px; justify-items: start; }
.settings-value { font-size: 16px; font-weight: 600; }
.settings-env, .settings-notes, .settings-shortcuts { display: grid; gap: 10px; }
.settings-shortcuts p { display: flex; align-items: center; gap: 10px; }
.ui-kbd { font-size: 12px; padding: 2px 7px; border-radius: 6px; background: var(--c-sunken); font-family: inherit; }
@media (max-width: 720px) { .settings-panel { margin-top: 18px; } }
</style>
