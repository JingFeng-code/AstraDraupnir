<script setup lang="ts">
import { toApiError, type ApiError } from '~/utils/errors'

const route = useRoute()
const auth = useAuth()

const email = ref('')
const password = ref('')
const submitting = ref(false)
const error = ref<ApiError | null>(null)

const redirect = computed(() => {
  const target = route.query.redirect
  return typeof target === 'string' && target.startsWith('/') ? target : '/'
})

async function submit() {
  if (submitting.value) return
  error.value = null
  submitting.value = true
  try {
    await auth.login(email.value.trim(), password.value)
    await navigateTo(redirect.value, { replace: true })
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <div class="ui-panel login-panel">
      <div class="login-brand">
        <svg class="ui-brand-mark" width="34" height="34" viewBox="0 0 30 30" aria-hidden="true">
          <rect x="1" y="1" width="28" height="28" rx="9" fill="currentColor" />
          <path d="M6 18h5" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-dasharray="1 4" />
          <path d="M13 18h5" stroke="#fff" stroke-width="2.4" stroke-linecap="round" />
          <path d="m23 9.5 1.3 2.7 3 .4-2.2 2.1.5 3-2.6-1.4-2.6 1.4.5-3-2.2-2.1 3-.4z" fill="#fff" />
        </svg>
        <span>Draupnir</span>
      </div>

      <h1 class="ui-h1">登入 Draupnir</h1>
      <p class="ui-lead">使用本機設定的管理員帳號。Human Gate 的批准與駁回需要這個帳號。</p>

      <form class="login-form" @submit.prevent="submit">
        <div>
          <label for="email" class="field-label">Email</label>
          <input
            id="email"
            v-model="email"
            type="email"
            autocomplete="username"
            required
            class="field-input"
            data-testid="login-email"
            :disabled="submitting"
          >
        </div>
        <div>
          <label for="password" class="field-label">密碼</label>
          <input
            id="password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            required
            class="field-input"
            data-testid="login-password"
            :disabled="submitting"
          >
        </div>

        <AppNotice v-if="error" :message="error.detail" :details="Object.values(error.fields).flat()" />

        <button type="submit" class="btn btn-primary login-submit" :disabled="submitting" data-testid="login-submit">
          {{ submitting ? '登入中…' : '登入' }}
        </button>
        <p v-if="submitting" class="ui-meta login-status" role="status">正在驗證帳號…</p>
      </form>
    </div>
  </main>
</template>

<style scoped>
.login-page { min-height: 100vh; display: grid; place-items: center; padding: 32px 16px; }
.login-panel { width: min(30rem, 100%); padding: 34px 32px 30px; }
.login-brand { display: flex; align-items: center; gap: 10px; margin-bottom: 22px; font-size: 18px; font-weight: 700; color: var(--c-ink); }
.login-form { margin-top: 26px; display: grid; gap: 16px; }
.login-submit { width: 100%; min-height: 46px; }
.login-status { text-align: center; }
@media (max-width: 390px) {
  .login-panel { padding: 26px 20px; }
}
</style>
