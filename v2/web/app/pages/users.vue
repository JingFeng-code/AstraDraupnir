<script setup lang="ts">
import type { User, UserRole } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { canManageUsers } from '~/utils/permissions'

const auth = useAuth()
const api = useApi()
const users = ref<User[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const form = reactive({ email: '', displayName: '', password: '', role: 'Member' as UserRole })
const submitting = ref(false)
const formError = ref<ApiError | null>(null)
const actionError = ref<string | null>(null)

const admin = computed(() => canManageUsers(auth.state.value.user))

async function load() {
  if (!admin.value) {
    loading.value = false
    return
  }
  loading.value = true
  error.value = null
  try {
    users.value = await api.get<User[]>('/api/users')
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

async function createUser() {
  if (submitting.value) return
  formError.value = null
  submitting.value = true
  try {
    await api.post<User>('/api/users', {
      email: form.email.trim(),
      displayName: form.displayName.trim(),
      password: form.password,
      role: form.role,
    })
    form.email = ''
    form.displayName = ''
    form.password = ''
    form.role = 'Member'
    await load()
  } catch (caught) {
    formError.value = toApiError(caught)
  } finally {
    submitting.value = false
  }
}

async function resetPassword(user: User) {
  const password = window.prompt(`為 ${user.email} 設定新密碼（至少 8 字元）`)
  if (!password) return
  actionError.value = null
  try {
    await api.patch<User>(`/api/users/${user.id}`, { password })
  } catch (caught) {
    actionError.value = toApiError(caught).detail
  }
}

async function toggleActive(user: User) {
  actionError.value = null
  try {
    await api.patch<User>(`/api/users/${user.id}`, { isActive: !user.isActive })
    await load()
  } catch (caught) {
    actionError.value = toApiError(caught).detail
  }
}

onMounted(async () => {
  if (!auth.state.value.ready) await auth.fetchMe()
  await load()
})
</script>

<template>
  <div class="site-container py-8">
    <div>
      <p class="font-mono text-xs uppercase tracking-wider text-accent">Administration</p>
      <h1 class="mt-1 text-3xl font-bold text-ink">使用者</h1>
      <p class="mt-1 text-sm text-ink-muted">Admin 可建立、停用使用者與重設密碼；Member 只能存取被加入的專案。</p>
    </div>

    <AppNotice v-if="!admin" class="mt-6" message="只有 Admin 可以管理使用者。" />
    <template v-else>
      <form class="panel mt-6 space-y-4 p-6" data-testid="user-form" @submit.prevent="createUser">
        <h2 class="text-lg font-bold text-ink">新增使用者</h2>
        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label for="user-email" class="field-label">Email</label>
            <input id="user-email" v-model="form.email" type="email" required class="field-input" data-testid="user-email">
          </div>
          <div>
            <label for="user-display-name" class="field-label">顯示名稱</label>
            <input id="user-display-name" v-model="form.displayName" required class="field-input" data-testid="user-display-name">
          </div>
          <div>
            <label for="user-password" class="field-label">初始密碼（至少 8 字元）</label>
            <input id="user-password" v-model="form.password" type="password" required minlength="8" class="field-input" data-testid="user-password">
          </div>
          <div>
            <label for="user-role" class="field-label">角色</label>
            <select id="user-role" v-model="form.role" class="field-input" data-testid="user-role">
              <option value="Member">Member</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
        </div>
        <AppNotice v-if="formError" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
        <div class="flex justify-end">
          <button type="submit" class="btn btn-primary" :disabled="submitting" data-testid="user-submit">
            {{ submitting ? '建立中…' : '建立使用者' }}
          </button>
        </div>
      </form>

      <AppNotice v-if="actionError" class="mt-4" :message="actionError" />
      <AppLoading v-if="loading" />
      <AppNotice v-else-if="error" class="mt-4" :message="error.detail" />
      <div v-else class="table-wrap panel mt-6">
        <table class="w-full text-left text-sm">
          <thead class="bg-surface-muted text-xs text-ink-subtle">
            <tr>
              <th class="px-4 py-2 font-medium">Email</th>
              <th class="px-4 py-2 font-medium">名稱</th>
              <th class="px-4 py-2 font-medium">角色</th>
              <th class="px-4 py-2 font-medium">狀態</th>
              <th class="px-4 py-2 font-medium">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in users" :key="user.id" class="border-t border-line" :data-testid="`user-row-${user.email}`">
              <td class="px-4 py-2 font-mono text-xs">{{ user.email }}</td>
              <td class="px-4 py-2">{{ user.displayName }}</td>
              <td class="px-4 py-2">{{ user.role }}</td>
              <td class="px-4 py-2">{{ user.isActive ? '啟用' : '停用' }}</td>
              <td class="px-4 py-2">
                <div class="flex flex-wrap gap-2">
                  <button type="button" class="btn btn-secondary" @click="resetPassword(user)">重設密碼</button>
                  <button type="button" class="btn btn-secondary" @click="toggleActive(user)">
                    {{ user.isActive ? '停用' : '啟用' }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>
