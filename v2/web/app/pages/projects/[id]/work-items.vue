<script setup lang="ts">
import type {
  AssigneeKind,
  Milestone,
  Paged,
  Priority,
  Requirement,
  Sprint,
  WorkItem,
  WorkItemDependency,
  WorkStatus,
  WorkType,
} from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { assigneeKindLabels, parseWorkStatus, priorityLabels, severityLabels, severityOptions, workStatusLabels, workTypeLabels } from '~/utils/labels'
import { todayIso } from '~/utils/format'
import { severityPayload } from '~/utils/workItemForm'

const route = useRoute()
const api = useApi()
const project = inject<Ref<import('~/types/api').Project | null> | null>('project', null)
const id = computed(() => String(route.params.id))
const projectKey = computed(() => project?.value?.key ?? '')

const items = ref<WorkItem[]>([])
const requirements = ref<Requirement[]>([])
const sprints = ref<Sprint[]>([])
const milestones = ref<Milestone[]>([])
const dependencies = ref<Record<string, string[]>>({})

const loading = ref(true)
const loadError = ref<ApiError | null>(null)
const actionError = ref<string | null>(null)
const actionDetails = ref<string[]>([])

const view = ref<'table' | 'board'>('table')
const busyId = ref<string | null>(null)

const filters = reactive({ status: '', type: '', q: '' })

const submitting = ref(false)
const formOpen = ref(false)
const formError = ref<ApiError | null>(null)
const form = reactive({
  code: '',
  type: 'Task' as WorkType,
  severity: '',
  title: '',
  description: '',
  status: 'Ready' as WorkStatus,
  priority: 'P2' as Priority,
  assigneeKind: 'None' as AssigneeKind,
  assignee: '',
  requirementId: '',
  sprintId: '',
  milestoneId: '',
  startDate: '',
  dueDate: '',
})

const blockedOpen = ref(false)
const blockedTarget = ref<WorkItem | null>(null)
const blockedReason = ref('')
const blockedError = ref<string | null>(null)

const statuses: WorkStatus[] = ['Backlog', 'Ready', 'InProgress', 'InReview', 'Blocked', 'Done']
const types: WorkType[] = ['Task', 'Bug', 'Feature']
const priorities: Priority[] = ['P0', 'P1', 'P2', 'P3']
const assigneeKinds: AssigneeKind[] = ['None', 'Human', 'Agent']

const filteredItems = computed(() => items.value.filter((item) => {
  if (filters.status && item.status !== filters.status) return false
  if (filters.type && item.type !== filters.type) return false
  if (filters.q && !`${item.code} ${item.title} ${item.description}`.toLowerCase().includes(filters.q.toLowerCase())) return false
  return true
}))

function nextCode(): string {
  const key = projectKey.value || 'ITEM'
  const numbers = items.value
    .map(item => Number(item.code.replace(`${key}-`, '')))
    .filter(number => Number.isFinite(number))
  return `${key}-${numbers.length ? Math.max(...numbers) + 1 : 1}`
}

function itemCode(depId: string): string {
  return items.value.find(item => item.id === depId)?.code ?? depId.slice(0, 8)
}

function setActionError(caught: unknown) {
  const error = toApiError(caught)
  actionError.value = error.detail
  actionDetails.value = Object.values(error.fields).flat()
}

function clearActionError() {
  actionError.value = null
  actionDetails.value = []
}

async function loadDependencies(list: WorkItem[]) {
  const entries = await Promise.all(list.map(async (item) => {
    try {
      const deps = await api.get<WorkItemDependency[]>(`/api/work-items/${item.id}/dependencies`)
      return [item.id, deps.map(dep => dep.dependsOnId)] as const
    } catch {
      return [item.id, []] as const
    }
  }))
  dependencies.value = Object.fromEntries(entries)
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const [page, reqs, sprintList, milestoneList] = await Promise.all([
      api.get<Paged<WorkItem>>(`/api/projects/${id.value}/work-items`, { pageSize: 100 }),
      api.get<Requirement[]>(`/api/projects/${id.value}/requirements`),
      api.get<Sprint[]>(`/api/projects/${id.value}/sprints`),
      api.get<Milestone[]>(`/api/projects/${id.value}/milestones`),
    ])
    items.value = page.items
    requirements.value = reqs
    sprints.value = sprintList
    milestones.value = milestoneList
    if (!form.code) form.code = nextCode()
    await loadDependencies(page.items)
  } catch (caught) {
    loadError.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

function optional(value: string): string | null {
  return value.trim() ? value : null
}

async function createItem() {
  if (submitting.value) return
  formError.value = null
  submitting.value = true
  try {
    await api.post<WorkItem>(`/api/projects/${id.value}/work-items`, {
      code: form.code.trim(),
      type: form.type,
      severity: severityPayload(form.type, form.severity),
      title: form.title.trim(),
      description: form.description.trim(),
      status: form.status,
      priority: form.priority,
      assigneeKind: form.assigneeKind,
      assignee: optional(form.assignee),
      requirementId: optional(form.requirementId),
      sprintId: optional(form.sprintId),
      milestoneId: optional(form.milestoneId),
      startDate: optional(form.startDate),
      dueDate: optional(form.dueDate),
    })
    form.title = ''
    form.description = ''
    form.severity = ''
    form.assignee = ''
    form.startDate = ''
    form.dueDate = ''
    formOpen.value = false
    await load()
    form.code = nextCode()
  } catch (caught) {
    formError.value = toApiError(caught)
  } finally {
    submitting.value = false
  }
}

async function patchStatus(item: WorkItem, status: WorkStatus, reason?: string) {
  busyId.value = item.id
  clearActionError()
  try {
    const updated = await api.patch<WorkItem>(`/api/work-items/${item.id}/status`, {
      status,
      reason: reason ?? null,
    })
    const index = items.value.findIndex(candidate => candidate.id === item.id)
    if (index >= 0) items.value[index] = updated
  } catch (caught) {
    setActionError(caught)
  } finally {
    busyId.value = null
  }
}

function requestMove(item: WorkItem, status: WorkStatus) {
  clearActionError()
  if (status === 'Blocked') {
    blockedTarget.value = item
    blockedReason.value = item.blockedReason ?? ''
    blockedError.value = null
    blockedOpen.value = true
    return
  }
  void patchStatus(item, status)
}

async function confirmBlocked() {
  if (!blockedTarget.value) return
  if (!blockedReason.value.trim()) {
    blockedError.value = '設為「阻塞」時必須填寫原因。'
    return
  }
  const target = blockedTarget.value
  blockedOpen.value = false
  await patchStatus(target, 'Blocked', blockedReason.value.trim())
}

async function updateSeverity(item: WorkItem, event: Event) {
  const select = event.target as HTMLSelectElement
  const severity = select.value || null
  if (item.type !== 'Bug') return
  busyId.value = item.id
  clearActionError()
  try {
    const updated = await api.put<WorkItem>(`/api/projects/${id.value}/work-items/${item.id}`, {
      ...item,
      severity,
    })
    const index = items.value.findIndex(candidate => candidate.id === item.id)
    if (index >= 0) items.value[index] = updated
  } catch (caught) {
    setActionError(caught)
  } finally {
    busyId.value = null
  }
}

function onTableStatusChange(event: Event, item: WorkItem) {
  const select = event.target as HTMLSelectElement
  const status = parseWorkStatus(select.value)
  select.value = item.status
  if (status && status !== item.status) requestMove(item, status)
}

async function addDependency(item: WorkItem, event: Event) {
  const select = event.target as HTMLSelectElement
  const dependsOnId = select.value
  if (!dependsOnId) return
  clearActionError()
  busyId.value = item.id
  try {
    await api.post(`/api/work-items/${item.id}/dependencies`, { dependsOnId })
    select.value = ''
    await loadDependencies(items.value)
  } catch (caught) {
    setActionError(caught)
  } finally {
    busyId.value = null
  }
}

async function removeDependency(item: WorkItem, depId: string) {
  clearActionError()
  busyId.value = item.id
  try {
    await api.del(`/api/work-items/${item.id}/dependencies/${depId}`)
    await loadDependencies(items.value)
  } catch (caught) {
    setActionError(caught)
  } finally {
    busyId.value = null
  }
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="site-container py-6">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold text-ink">工作項目</h2>
        <p class="text-sm text-ink-muted">表格或看板操作；Done 需要所有相依項目先完成，Blocked 需要原因。</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <div class="inline-flex rounded-lg border border-line bg-surface p-0.5" role="group" aria-label="檢視模式">
          <button
            type="button"
            class="rounded-md px-3 py-1.5 text-sm font-medium"
            :class="view === 'table' ? 'bg-navy-800 text-white' : 'text-ink-muted'"
            :aria-pressed="view === 'table'"
            data-testid="view-table"
            @click="view = 'table'"
          >
            表格
          </button>
          <button
            type="button"
            class="rounded-md px-3 py-1.5 text-sm font-medium"
            :class="view === 'board' ? 'bg-navy-800 text-white' : 'text-ink-muted'"
            :aria-pressed="view === 'board'"
            data-testid="view-board"
            @click="view = 'board'"
          >
            看板
          </button>
        </div>
        <button type="button" class="btn btn-primary" data-testid="new-work-item" @click="formOpen = !formOpen">
          {{ formOpen ? '收起表單' : '新增工作項目' }}
        </button>
      </div>
    </div>

    <form v-if="formOpen" class="panel mt-4 space-y-4 p-5" data-testid="work-item-form" @submit.prevent="createItem">
      <div class="grid gap-4 sm:grid-cols-4">
        <div>
          <label for="wi-code" class="field-label">代碼</label>
          <input id="wi-code" v-model="form.code" required class="field-input font-mono" data-testid="work-item-code">
        </div>
        <div>
          <label for="wi-type" class="field-label">類型</label>
          <select id="wi-type" v-model="form.type" class="field-input" data-testid="work-item-type">
            <option v-for="type in types" :key="type" :value="type">{{ workTypeLabels[type] }}</option>
          </select>
        </div>
        <div v-if="form.type === 'Bug'">
          <label for="wi-severity" class="field-label">Bug 嚴重度</label>
          <select id="wi-severity" v-model="form.severity" class="field-input" data-testid="work-item-severity">
            <option value="">未指定</option>
            <option v-for="severity in severityOptions" :key="severity" :value="severity">{{ severityLabels[severity].label }}</option>
          </select>
        </div>
        <div class="sm:col-span-2">
          <label for="wi-title" class="field-label">標題</label>
          <input id="wi-title" v-model="form.title" required class="field-input" data-testid="work-item-title">
        </div>
      </div>
      <div>
        <label for="wi-description" class="field-label">說明</label>
        <textarea id="wi-description" v-model="form.description" rows="2" class="field-input" data-testid="work-item-description" />
      </div>
      <div class="grid gap-4 sm:grid-cols-3">
        <div>
          <label for="wi-status" class="field-label">狀態</label>
          <select id="wi-status" v-model="form.status" class="field-input">
            <option v-for="status in statuses" :key="status" :value="status">{{ workStatusLabels[status].label }}</option>
          </select>
        </div>
        <div>
          <label for="wi-priority" class="field-label">優先級</label>
          <select id="wi-priority" v-model="form.priority" class="field-input">
            <option v-for="priority in priorities" :key="priority" :value="priority">{{ priorityLabels[priority] }}</option>
          </select>
        </div>
        <div>
          <label for="wi-assignee-kind" class="field-label">指派類型</label>
          <select id="wi-assignee-kind" v-model="form.assigneeKind" class="field-input">
            <option v-for="kind in assigneeKinds" :key="kind" :value="kind">{{ assigneeKindLabels[kind] }}</option>
          </select>
        </div>
      </div>
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <label for="wi-assignee" class="field-label">指派對象</label>
          <input id="wi-assignee" v-model="form.assignee" class="field-input" data-testid="work-item-assignee">
        </div>
        <div>
          <label for="wi-requirement" class="field-label">需求</label>
          <select id="wi-requirement" v-model="form.requirementId" class="field-input">
            <option value="">（無）</option>
            <option v-for="requirement in requirements" :key="requirement.id" :value="requirement.id">{{ requirement.code }} {{ requirement.title }}</option>
          </select>
        </div>
        <div>
          <label for="wi-sprint" class="field-label">Sprint</label>
          <select id="wi-sprint" v-model="form.sprintId" class="field-input">
            <option value="">（無）</option>
            <option v-for="sprint in sprints" :key="sprint.id" :value="sprint.id">{{ sprint.name }}</option>
          </select>
        </div>
        <div>
          <label for="wi-milestone" class="field-label">Milestone</label>
          <select id="wi-milestone" v-model="form.milestoneId" class="field-input">
            <option value="">（無）</option>
            <option v-for="milestone in milestones" :key="milestone.id" :value="milestone.id">{{ milestone.name }}</option>
          </select>
        </div>
        <div>
          <label for="wi-start" class="field-label">開始日期</label>
          <input id="wi-start" v-model="form.startDate" type="date" :max="todayIso()" class="field-input">
        </div>
        <div>
          <label for="wi-due" class="field-label">截止日期</label>
          <input id="wi-due" v-model="form.dueDate" type="date" class="field-input">
        </div>
      </div>
      <AppNotice v-if="formError" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
      <div class="flex justify-end gap-2">
        <button type="button" class="btn btn-secondary" @click="formOpen = false">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="submitting" data-testid="work-item-submit">
          {{ submitting ? '建立中…' : '建立工作項目' }}
        </button>
      </div>
    </form>

    <div class="mt-4 flex flex-wrap items-end gap-3">
      <div>
        <label for="filter-status" class="field-label">狀態篩選</label>
        <select id="filter-status" v-model="filters.status" class="field-input w-40">
          <option value="">全部</option>
          <option v-for="status in statuses" :key="status" :value="status">{{ workStatusLabels[status].label }}</option>
        </select>
      </div>
      <div>
        <label for="filter-type" class="field-label">類型篩選</label>
        <select id="filter-type" v-model="filters.type" class="field-input w-36">
          <option value="">全部</option>
          <option v-for="type in types" :key="type" :value="type">{{ workTypeLabels[type] }}</option>
        </select>
      </div>
      <div class="min-w-[12rem] flex-1">
        <label for="filter-q" class="field-label">搜尋</label>
        <input id="filter-q" v-model="filters.q" type="search" class="field-input" placeholder="代碼、標題或說明">
      </div>
    </div>

    <AppNotice
      v-if="actionError"
      class="mt-4"
      :message="actionError"
      :details="actionDetails"
      data-testid="action-error"
    />

    <AppLoading v-if="loading" />
    <AppNotice v-else-if="loadError" class="mt-4" :message="loadError.detail" />
    <AppEmpty v-else-if="!items.length" class="mt-4" title="還沒有工作項目" description="新增第一個工作項目，或從 Supervisor 匯入。" />

    <template v-else>
      <div v-if="view === 'board'" class="mt-5">
        <WorkItemBoard :items="filteredItems" :busy-id="busyId" @move="requestMove" />
      </div>

      <div v-else class="table-wrap panel mt-5">
        <table class="w-full text-left text-sm">
          <thead class="bg-surface-muted text-xs text-ink-subtle">
            <tr>
              <th class="px-3 py-2 font-medium">代碼</th>
              <th class="px-3 py-2 font-medium">標題</th>
              <th class="px-3 py-2 font-medium">狀態</th>
              <th class="px-3 py-2 font-medium">相依</th>
              <th class="px-3 py-2 font-medium">類型／優先級</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in filteredItems"
              :key="item.id"
              class="border-t border-line align-top"
              :data-testid="`work-item-row-${item.code}`"
            >
              <td class="px-3 py-3 font-mono text-xs">{{ item.code }}</td>
              <td class="px-3 py-3">
                <p class="font-medium text-ink">{{ item.title }}</p>
                <p v-if="item.description" class="mt-0.5 line-clamp-2 text-xs text-ink-muted">{{ item.description }}</p>
                <p v-if="item.blockedReason" class="mt-1 text-xs text-[#8a1c1c]">阻塞原因：{{ item.blockedReason }}</p>
              </td>
              <td class="px-3 py-3">
                <label class="sr-only" :for="`status-${item.code}`">變更 {{ item.code }} 狀態</label>
                <select
                  :id="`status-${item.code}`"
                  class="field-input w-32 py-1 text-xs"
                  :value="item.status"
                  :disabled="busyId === item.id"
                  :data-testid="`status-select-${item.code}`"
                  @change="onTableStatusChange($event, item)"
                >
                  <option v-for="status in statuses" :key="status" :value="status">{{ workStatusLabels[status].label }}</option>
                </select>
              </td>
              <td class="px-3 py-3 text-xs">
                <ul v-if="(dependencies[item.id] ?? []).length" class="space-y-1">
                  <li v-for="depId in dependencies[item.id]" :key="depId" class="flex flex-wrap items-center gap-1">
                    <span class="font-mono">{{ itemCode(depId) }}</span>
                    <button type="button" class="text-[#8a1c1c] underline" :aria-label="`移除 ${item.code} 對 ${itemCode(depId)} 的相依`" @click="removeDependency(item, depId)">
                      移除
                    </button>
                  </li>
                </ul>
                <p v-else class="text-ink-subtle">無</p>
                <div class="mt-2 flex items-center gap-1">
                  <label class="sr-only" :for="`dep-${item.code}`">選擇 {{ item.code }} 的相依項目</label>
                  <select
                    :id="`dep-${item.code}`"
                    class="field-input w-28 py-1 text-xs"
                    :disabled="busyId === item.id"
                    :data-testid="`dependency-select-${item.code}`"
                    @change="addDependency(item, $event)"
                  >
                    <option value="">加入相依…</option>
                    <option
                      v-for="candidate in items.filter(other => other.id !== item.id && !(dependencies[item.id] ?? []).includes(other.id))"
                      :key="candidate.id"
                      :value="candidate.id"
                    >
                      {{ candidate.code }}
                    </option>
                  </select>
                </div>
              </td>
              <td class="px-3 py-3 text-xs text-ink-muted">
                <p>{{ workTypeLabels[item.type] }} · {{ priorityLabels[item.priority] }}</p>
                <label v-if="item.type === 'Bug'" class="mt-1 block">
                  <span class="text-[11px] text-ink-subtle">Bug 嚴重度</span>
                  <select
                    class="field-input mt-0.5 py-1 text-xs"
                    :value="item.severity ?? ''"
                    :disabled="busyId === item.id"
                    :data-testid="`severity-select-${item.code}`"
                    @change="updateSeverity(item, $event)"
                  >
                    <option value="">未指定</option>
                    <option v-for="severity in severityOptions" :key="severity" :value="severity">{{ severityLabels[severity].label }}</option>
                  </select>
                </label>
                <p v-if="item.assignee" class="mt-0.5">{{ assigneeKindLabels[item.assigneeKind] }}：{{ item.assignee }}</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <ConfirmDialog
      v-model:open="blockedOpen"
      title="設定為阻塞"
      description="請填寫阻塞原因；原因會顯示在工作項目與 Dashboard。"
      confirm-label="設為阻塞"
      :busy="busyId === blockedTarget?.id"
      @confirm="confirmBlocked"
    >
      <label for="blocked-reason" class="field-label mt-4">原因</label>
      <textarea id="blocked-reason" v-model="blockedReason" rows="3" class="field-input" data-testid="blocked-reason" />
      <p v-if="blockedError" class="mt-2 text-sm text-[#8a1c1c]" role="alert">{{ blockedError }}</p>
    </ConfirmDialog>
  </div>
</template>
