<script setup lang="ts">
import type { Project, ProjectMember, User, UserRole } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { canManageUsers } from '~/utils/permissions'

const auth = useAuth()
const api = useApi()
const ws = useWorkspace()
const users = ref<User[]>([])
const projects = ref<Project[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const form = reactive({ email: '', displayName: '', password: '', role: 'Member' as UserRole })
const submitting = ref(false)
const formError = ref<ApiError | null>(null)
const actionError = ref<string | null>(null)

const admin = computed(() => canManageUsers(auth.state.value.user))
const selectedProject = ref<string>('')
const members = ref<ProjectMember[]>([])
const membersLoading = ref(false)
const membersError = ref<string | null>(null)
const newMemberId = ref('')
const memberBusy = ref(false)

type Pending =
  | { kind: 'deactivate', user: User, title: string, description: string }
  | { kind: 'role', user: User, role: UserRole, title: string, description: string }
  | { kind: 'remove', user: ProjectMember, title: string, description: string }
const pending = ref<Pending | null>(null)
const acting = ref(false)

const availableMembers = computed(() => users.value.filter(u =>
  u.isActive && !members.value.some(m => m.id === u.id)))
const projectName = computed(() => projects.value.find(p => p.id === selectedProject.value)?.name ?? '')

async function load() {
  if (!admin.value) {
    loading.value = false
    return
  }
  loading.value = true
  error.value = null
  try {
    const [list, projectList] = await Promise.all([
      api.get<User[]>('/api/users'),
      api.get<Project[] | { items: Project[] }>('/api/projects'),
    ])
    users.value = list
    projects.value = Array.isArray(projectList) ? projectList : projectList.items
    if (!selectedProject.value && projects.value.length) selectedProject.value = projects.value[0]!.id
    if (selectedProject.value) await loadMembers()
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

async function loadMembers() {
  if (!selectedProject.value) { members.value = []; return }
  membersLoading.value = true
  membersError.value = null
  try {
    members.value = await api.get<ProjectMember[]>(`/api/projects/${selectedProject.value}/members`)
  } catch (caught) {
    membersError.value = toApiError(caught).detail
  } finally {
    membersLoading.value = false
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
    ws.toast.value = '已建立使用者'
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
    ws.toast.value = `${user.email} 的密碼已重設`
  } catch (caught) {
    actionError.value = toApiError(caught).detail
  }
}

function askToggleActive(user: User) {
  actionError.value = null
  if (user.isActive) {
    pending.value = {
      kind: 'deactivate', user,
      title: `停用 ${user.email}？`,
      description: '停用後該使用者無法登入；這是降低存取權限的動作，需要確認。',
    }
    return
  }
  void setActive(user, true)
}

async function setActive(user: User, isActive: boolean) {
  try {
    await api.patch<User>(`/api/users/${user.id}`, { isActive })
    ws.toast.value = isActive ? `${user.email} 已啟用` : `${user.email} 已停用`
    await load()
  } catch (caught) {
    actionError.value = toApiError(caught).detail
  }
}

function askRole(user: User, event: Event) {
  const role = (event.target as HTMLSelectElement).value as UserRole
  if (role === user.role) return
  actionError.value = null
  if (user.role === 'Admin' && role === 'Member') {
    pending.value = {
      kind: 'role', user, role,
      title: `將 ${user.email} 降為 Member？`,
      description: '這是降低存取權限的動作；該使用者會失去管理者功能。',
    }
    return
  }
  void setRole(user, role)
}

async function setRole(user: User, role: UserRole) {
  try {
    await api.patch<User>(`/api/users/${user.id}`, { role })
    ws.toast.value = `${user.email} 的角色已改為 ${role}`
    await load()
  } catch (caught) {
    actionError.value = toApiError(caught).detail
  }
}

function askRemoveMember(member: ProjectMember) {
  membersError.value = null
  pending.value = {
    kind: 'remove', user: member,
    title: `移除 ${member.email} 的專案成員資格？`,
    description: `該使用者將失去 ${projectName.value} 的存取權限。`,
  }
}

async function addMember() {
  if (!newMemberId.value || !selectedProject.value || memberBusy.value) return
  memberBusy.value = true
  membersError.value = null
  try {
    await api.post(`/api/projects/${selectedProject.value}/members`, { userId: newMemberId.value })
    newMemberId.value = ''
    ws.toast.value = '已加入專案成員'
    await loadMembers()
  } catch (caught) {
    membersError.value = toApiError(caught).detail
  } finally {
    memberBusy.value = false
  }
}

async function runPending() {
  const item = pending.value
  if (!item || acting.value) return
  acting.value = true
  try {
    if (item.kind === 'deactivate') await setActive(item.user, false)
    if (item.kind === 'role') await setRole(item.user, item.role)
    if (item.kind === 'remove') {
      await api.del(`/api/projects/${selectedProject.value}/members/${item.user.id}`)
      ws.toast.value = `已移除 ${item.user.email} 的成員資格`
      await loadMembers()
    }
    pending.value = null
  } catch (caught) {
    actionError.value = toApiError(caught).detail
  } finally {
    acting.value = false
  }
}

onMounted(async () => {
  if (!auth.state.value.ready) await auth.fetchMe()
  await load()
})
</script>

<template>
  <div>
    <h1 class="ui-h1">使用者</h1>
    <p class="ui-lead">Admin 可建立、停用使用者與重設密碼；Member 只能存取被加入的專案。</p>

    <AppNotice v-if="!admin" class="users-notice" tone="info" message="沒有權限：只有 Admin 可以管理使用者。" />

    <template v-else>
      <AppLoading v-if="loading" label="正在讀取使用者與專案…" />
      <AppNotice v-else-if="error" :message="error.detail" :details="Object.values(error.fields).flat()">
        <button type="button" class="btn btn-secondary" @click="load">重試</button>
      </AppNotice>

      <template v-else>
        <section class="ui-panel users-panel">
          <div class="ui-panel-h">
            <h2 class="ui-h2">新增使用者</h2>
            <span class="ui-meta">角色與狀態由 Admin 管理</span>
          </div>
          <div class="ui-panel-b">
            <form data-testid="user-form" class="users-form" @submit.prevent="createUser">
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
              <div class="users-form-actions">
                <button type="submit" class="btn btn-primary" :disabled="submitting" data-testid="user-submit">
                  {{ submitting ? '建立中…' : '建立使用者' }}
                </button>
              </div>
            </form>
            <AppNotice v-if="formError" class="users-inline-notice" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
          </div>
        </section>

        <AppNotice v-if="actionError" class="users-notice" :message="actionError" />

        <section class="ui-panel users-panel">
          <div class="ui-panel-h"><h2 class="ui-h2">使用者清單 · {{ users.length }}</h2></div>
          <AppEmpty v-if="!users.length" title="還沒有使用者" description="用上方表單建立第一位使用者。" />
          <table v-else class="ui-table">
            <thead>
              <tr>
                <th scope="col">Email</th>
                <th scope="col">名稱</th>
                <th scope="col">角色</th>
                <th scope="col">狀態</th>
                <th scope="col">操作</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="user in users" :key="user.id" :data-testid="`user-row-${user.email}`">
                <td class="ui-wide">{{ user.email }}</td>
                <td>{{ user.displayName }}</td>
                <td>
                  <label class="sr-only" :for="`role-${user.id}`">{{ user.email }} 的角色</label>
                  <select :id="`role-${user.id}`" class="ui-input" :value="user.role" @change="askRole(user, $event)">
                    <option value="Member">Member</option>
                    <option value="Admin">Admin</option>
                  </select>
                </td>
                <td><UiPill :tone="user.isActive ? 'proven' : 'idle'" :label="user.isActive ? '啟用' : '停用'" /></td>
                <td>
                  <div class="users-row-actions">
                    <button type="button" class="btn btn-secondary" @click="resetPassword(user)">重設密碼</button>
                    <button type="button" class="btn" :class="user.isActive ? 'btn-danger' : 'btn-secondary'" @click="askToggleActive(user)">
                      {{ user.isActive ? '停用' : '啟用' }}
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="ui-panel users-panel">
          <div class="ui-panel-h">
            <h2 class="ui-h2">專案成員與權限</h2>
            <label class="users-project-pick">
              <span class="ui-meta">專案</span>
              <select v-model="selectedProject" class="ui-input" @change="loadMembers">
                <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.name }}</option>
              </select>
            </label>
          </div>
          <div class="ui-panel-b">
            <p v-if="!projects.length" class="ui-meta">尚無專案可管理成員。</p>
            <template v-else>
              <div class="users-add-member">
                <label class="sr-only" for="new-member">選擇使用者</label>
                <select id="new-member" v-model="newMemberId" class="ui-input">
                  <option value="">選擇要加入的使用者</option>
                  <option v-for="u in availableMembers" :key="u.id" :value="u.id">{{ u.email }}（{{ u.role }}）</option>
                </select>
                <button type="button" class="btn btn-primary" :disabled="!newMemberId || memberBusy" @click="addMember">加入成員</button>
              </div>
              <AppLoading v-if="membersLoading" label="正在讀取成員…" />
              <AppNotice v-else-if="membersError" :message="membersError">
                <button type="button" class="btn btn-secondary" @click="loadMembers">重試</button>
              </AppNotice>
              <AppEmpty v-else-if="!members.length" title="這個專案還沒有成員" description="Member 必須被加入專案才能看到它。" />
              <table v-else class="ui-table">
                <thead><tr><th scope="col">Email</th><th scope="col">角色</th><th scope="col">狀態</th><th scope="col">操作</th></tr></thead>
                <tbody>
                  <tr v-for="member in members" :key="member.id">
                    <td class="ui-wide">{{ member.email }}</td>
                    <td>{{ member.role }}</td>
                    <td><UiPill :tone="member.isActive ? 'proven' : 'idle'" :label="member.isActive ? '啟用' : '停用'" /></td>
                    <td><button type="button" class="btn btn-danger" @click="askRemoveMember(member)">移除成員</button></td>
                  </tr>
                </tbody>
              </table>
            </template>
          </div>
        </section>

        <section class="ui-panel users-panel">
          <div class="ui-panel-h"><h2 class="ui-h2">角色可以做什麼</h2></div>
          <div class="ui-panel-b">
            <table class="ui-table users-matrix">
              <thead><tr><th scope="col">能力</th><th scope="col">Admin</th><th scope="col">Member</th></tr></thead>
              <tbody>
                <tr><td class="ui-wide">建立／停用使用者、指派角色</td><td>可以</td><td>不可以</td></tr>
                <tr><td class="ui-wide">建立專案</td><td>可以</td><td>不可以</td></tr>
                <tr><td class="ui-wide">核准／駁回 Human Gate</td><td>可以</td><td>不可以</td></tr>
                <tr><td class="ui-wide">檢視稽核紀錄</td><td>可以</td><td>不可以</td></tr>
                <tr><td class="ui-wide">看見並處理專案</td><td>所有專案</td><td>僅被加入的專案</td></tr>
                <tr><td class="ui-wide">新增／修改工作項目與證據</td><td>可以</td><td>可以（限所屬專案）</td></tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
    </template>

    <ConfirmDialog
      :open="!!pending"
      :title="pending?.title ?? ''"
      :description="pending?.description"
      :busy="acting"
      tone="danger"
      confirm-label="確認執行"
      @update:open="pending = null"
      @confirm="runPending"
    />
  </div>
</template>

<style scoped>
.users-panel { margin-top: 24px; }
.users-notice { margin-top: 20px; }
.users-inline-notice { margin-top: 16px; }
.users-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
.users-form-actions { grid-column: 1 / -1; display: flex; justify-content: flex-end; }
.users-row-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.users-project-pick { display: flex; align-items: center; gap: 10px; }
.users-add-member { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 16px; }
.users-add-member select { flex: 1; min-width: 220px; }
.users-matrix td:not(.ui-wide) { white-space: nowrap; }
@media (max-width: 720px) {
  .users-form { grid-template-columns: 1fr; }
  .users-panel { margin-top: 18px; }
}
</style>
