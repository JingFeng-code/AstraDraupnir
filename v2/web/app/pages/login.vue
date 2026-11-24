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
  <div class="mx-auto w-full max-w-md px-5 py-16">
    <h1 class="text-3xl font-bold text-ink">登入 Draupnir</h1>
    <p class="mt-2 text-sm text-ink-muted">使用本機設定的管理員帳號。Human Gate 的批准與駁回需要這個帳號。</p>

    <form class="panel mt-6 space-y-4 p-6" @submit.prevent="submit">
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
        >
      </div>

      <AppNotice v-if="error" :message="error.detail" :details="Object.values(error.fields).flat()" />

      <button type="submit" class="btn btn-primary w-full" :disabled="submitting" data-testid="login-submit">
        {{ submitting ? '登入中…' : '登入' }}
      </button>
    </form>
  </div>
</template>
