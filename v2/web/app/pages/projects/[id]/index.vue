<script setup lang="ts">
import type { Dashboard, ProjectMember, User } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { boardColumns, workStatusLabels } from '~/utils/labels'
import { formatDateTime, relativeTime } from '~/utils/format'
import { RUN_LABEL, runTone } from '~/lib/engineering'
import { workTone } from '~/utils/workItemForm'
import { canManageUsers } from '~/utils/permissions'

const route = useRoute()
const api = useApi()
const auth = useAuth()
const admin = computed(() => canManageUsers(auth.state.value.user))
const members = ref<ProjectMember[]>([])
const users = ref<User[]>([])
const { toast } = useWorkspace()
const memberTarget = ref<ProjectMember | null>(null)
const removeOpen = ref(false)
const memberBusy = ref(false)
const membersLoading = ref(true)
const memberToAdd = ref('')
const memberError = ref<string | null>(null)
const project = inject<Ref<import('~/types/api').Project | null> | null>('project', null)
const id = computed(() => String(route.params.id))

const dashboard = ref<Dashboard | null>(null)
const loading = ref(true)
const error = ref<ApiError | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    dashboard.value = await api.get<Dashboard>('/api/dashboard', { projectId: id.value })
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

async function loadMembers() {
  if (!admin.value) return
  membersLoading.value = true
  memberError.value = null
  try {
    [members.value, users.value] = await Promise.all([
      api.get<ProjectMember[]>(`/api/projects/${id.value}/members`),
      api.get<User[]>('/api/users'),
    ])
  } catch (caught) {
    memberError.value = toApiError(caught).detail
  } finally { membersLoading.value = false }
}

async function addMember() {
  if (!memberToAdd.value) return
  memberError.value = null
  try {
    await api.post(`/api/projects/${id.value}/members`, { userId: memberToAdd.value })
    memberToAdd.value = ''
    toast.value = '已加入專案成員'
    await loadMembers()
  } catch (caught) {
    memberError.value = toApiError(caught).detail
  }
}

async function removeMember() {
  if (!memberTarget.value || memberBusy.value) return
  memberBusy.value = true
  memberError.value = null
  try {
    await api.del(`/api/projects/${id.value}/members/${memberTarget.value.id}`)
    removeOpen.value = false
    toast.value = '已移除專案成員'
    await loadMembers()
  } catch (caught) {
    memberError.value = toApiError(caught).detail
    removeOpen.value = false
  } finally { memberBusy.value = false }
}

watch(id, load, { immediate: true })
watch([id, admin], loadMembers, { immediate: true })

const base = computed(() => `/projects/${id.value}`)
</script>

<template>
  <div class="min-w-0">
    <AppLoading v-if="loading" />
    <div v-else-if="error" class="space-y-3"><AppNotice :message="error.detail" /><button class="btn btn-secondary" @click="load">重試</button></div>
    <div v-else-if="dashboard" class="space-y-6">
      <section aria-labelledby="dashboard-status">
        <h2 id="dashboard-status" class="ui-h2">工作狀態</h2>
        <ul class="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
          <li v-for="status in boardColumns" :key="status" class="ui-panel p-4">
            <UiPill :tone="workTone[status]" :label="workStatusLabels[status].label" />
            <p class="mt-1 text-2xl font-bold text-ink" :data-testid="`dashboard-count-${status}`">
              {{ dashboard.statusCounts[status] ?? 0 }}
            </p>
          </li>
        </ul>
      </section>

      <section class="ui-panel p-5" aria-labelledby="dashboard-sprint">
        <h2 id="dashboard-sprint" class="ui-h2">本 Sprint 完成率</h2>
        <div class="mt-3 flex flex-wrap items-center gap-4">
          <p class="text-3xl font-bold text-ink">{{ dashboard.activeSprintCompletionRate }}%</p>
          <p class="text-sm text-ink-muted">完成 {{ dashboard.activeSprintDone }} / {{ dashboard.activeSprintTotal }} 個工作項目</p>
        </div>
        <div class="mt-3 h-2 w-full overflow-hidden rounded-full bg-surface-muted" role="img" :aria-label="`Sprint 完成率 ${dashboard.activeSprintCompletionRate}%`">
          <div class="h-full rounded-full bg-navy-700" :style="{ width: `${Math.min(100, Math.max(0, dashboard.activeSprintCompletionRate))}%` }" />
        </div>
      </section>

      <div class="grid gap-6 lg:grid-cols-2">
        <section class="ui-panel p-5" aria-labelledby="dashboard-gates">
          <div class="flex items-center justify-between gap-2">
            <h2 id="dashboard-gates" class="ui-h2">待核准 Human Gate</h2>
            <NuxtLink to="/review" class="text-sm font-medium text-navy-700 hover:underline">前往審查與決策</NuxtLink>
          </div>
          <ul v-if="dashboard.pendingGates.length" class="mt-3 space-y-2">
            <li v-for="gate in dashboard.pendingGates" :key="gate.id" class="rounded-lg bg-surface-muted p-3">
              <p class="text-sm font-semibold text-ink">{{ gate.title }} <span v-if="gate.title.includes('測試資料')" class="ui-tag is-test text-sm">測試資料</span></p>
              <p class="text-sm text-ink-subtle">{{ gate.externalRef?.startsWith('supervisor:') ? '匯入自 Supervisor，未提供記錄時間' : gate.externalRef ?? '手動建立' }}</p>
            </li>
          </ul>
          <p v-else class="mt-3 text-sm text-ink-muted">目前沒有待批的 Gate。</p>
        </section>

        <section class="ui-panel p-5" aria-labelledby="dashboard-blocked">
          <div class="flex items-center justify-between gap-2">
            <h2 id="dashboard-blocked" class="ui-h2">阻塞項目</h2>
            <NuxtLink :to="`${base}/work-items`" class="text-sm font-medium text-navy-700 hover:underline">工作項目</NuxtLink>
          </div>
          <ul v-if="dashboard.blockedItems.length" class="mt-3 space-y-2">
            <li v-for="item in dashboard.blockedItems" :key="item.id" class="rounded-lg bg-surface-muted p-3">
              <p class="font-mono text-sm text-ink-subtle">{{ item.code }}</p>
              <p class="text-sm font-semibold text-ink"><NuxtLink :to="'/work/' + item.id" class="text-navy-700 hover:underline">{{ item.title }}</NuxtLink> <span v-if="item.title.includes('測試資料')" class="ui-tag is-test text-sm">測試資料</span></p>
              <p class="text-sm text-ink-muted">{{ item.blockedReason || '未填寫原因' }}</p>
            </li>
          </ul>
          <p v-else class="mt-3 text-sm text-ink-muted">沒有阻塞項目。</p>
        </section>
      </div>

      <section v-if="admin" class="ui-panel p-5" aria-labelledby="dashboard-members">
        <h2 id="dashboard-members" class="ui-h2">專案成員</h2>
        <p class="mt-1 text-sm text-ink-muted">只有 Admin 可以加入或移除 Member；Member 只看得到被加入的專案。</p>
        <AppLoading v-if="membersLoading" /><div v-else-if="memberError" class="mt-3 space-y-2"><AppNotice :message="memberError" /><button class="btn btn-secondary" @click="loadMembers">重試成員資料</button></div>
        <ul v-if="members.length" class="mt-3 space-y-2">
          <li v-for="member in members" :key="member.id" class="flex flex-wrap items-center justify-between gap-2 py-2" :data-testid="`member-row-${member.email}`">
            <span class="text-sm text-ink">{{ member.displayName }} <span class="font-mono text-sm text-ink-subtle">{{ member.email }}</span></span>
            <button type="button" class="btn btn-danger" @click="memberTarget = member; removeOpen = true">移除</button>
          </li>
        </ul>
        <p v-else-if="!membersLoading && !memberError" class="mt-3 text-sm text-ink-muted">還沒有 Member；Admin 仍可存取所有專案。</p>
        <form class="mt-3 flex flex-wrap items-end gap-2" @submit.prevent="addMember">
          <div class="min-w-0 max-w-full flex-1">
            <label for="member-user" class="field-label">加入 Member</label>
            <select id="member-user" v-model="memberToAdd" class="field-input" data-testid="member-user">
              <option value="">選擇使用者</option>
              <option v-for="user in users.filter(candidate => !members.some(member => member.id === candidate.id))" :key="user.id" :value="user.id">
                {{ user.displayName }}（{{ user.email }}）
              </option>
            </select>
          </div>
          <button type="submit" class="btn btn-secondary" data-testid="member-add">加入</button>
        </form>
      </section>

      <section class="ui-panel p-5" aria-labelledby="dashboard-runs">
        <div class="flex items-center justify-between gap-2">
          <h2 id="dashboard-runs" class="ui-h2">最近 Agent 執行</h2>
          <NuxtLink :to="`${base}/agents`" class="text-sm font-medium text-navy-700 hover:underline">全部執行</NuxtLink>
        </div>
        <div v-if="dashboard.recentAgentRuns.length" class="mt-3 min-w-0 overflow-x-auto" role="region" tabindex="0" aria-label="Agent 執行紀錄">
          <table class="ui-table run-table text-sm">
            <thead>
              <tr class="border-b border-line text-sm text-ink-subtle">
                <th class="py-2 pr-4 font-medium">任務</th>
                <th class="py-2 pr-4 font-medium">Agent</th>
                <th class="py-2 pr-4 font-medium">狀態</th>
                <th class="py-2 font-medium">時間</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="run in dashboard.recentAgentRuns" :key="run.id" class="border-b border-line/60">
                <td class="py-2 pr-4 font-mono text-sm">{{ run.externalTaskId }}</td>
                <td class="py-2 pr-4">{{ run.agent }}</td>
                <td class="py-2 pr-4"><UiPill :tone="runTone(run.status)" :label="RUN_LABEL[run.status] ?? run.status" /></td>
                <td class="ui-wide ui-meta" :title="formatDateTime(run.finishedAt ?? run.startedAt)">
                  {{ run.source === 'Supervisor' ? (run.finishedAt ?? run.startedAt ? '匯入自 Supervisor，記錄時間 ' + formatDateTime(run.finishedAt ?? run.startedAt) : '匯入自 Supervisor，未提供記錄時間') : relativeTime(run.finishedAt ?? run.startedAt) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="mt-3 text-sm text-ink-muted">還沒有 Agent 執行紀錄；可從「匯入 Supervisor」開始。</p>
      </section>

      <ConfirmDialog v-model:open="removeOpen" title="移除專案成員" :description="memberTarget?.displayName" tone="danger" confirm-label="移除成員" :busy="memberBusy" @confirm="removeMember" />
      <p class="ui-meta">專案：{{ project?.key }} · 更新時間 {{ formatDateTime(project?.updatedAt) }}</p>
    </div>
  </div>
</template>

<style scoped>
.run-table tbody tr { cursor: auto; }
</style>
