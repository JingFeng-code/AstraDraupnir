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
import { severityPayload, severityRank, workTone, severityTone } from '~/utils/workItemForm'

const route = useRoute()
const api = useApi()
const { toast } = useWorkspace()
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

const filters = reactive({ status: '', type: '', severity: '', assignee: '', q: '' })
const order = ref('code')
const requirementFilter = computed(() => typeof route.query.requirement === 'string' ? route.query.requirement : '')
const assignees = computed(() => [...new Set(items.value.flatMap(item => item.assignee ? [item.assignee] : []))].sort())
function clearFilters() {
  Object.assign(filters, { status: '', type: '', severity: '', assignee: '', q: '' })
  if (requirementFilter.value) void navigateTo({ path: route.path, query: { ...route.query, requirement: undefined } })
}

const editing = ref<WorkItem | null>(null)
const removeOpen = ref(false)
const dependencyTarget = ref<{ item: WorkItem, id: string } | null>(null)
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
  blockedReason: '',
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
  if (filters.severity && item.severity !== filters.severity) return false
  if (filters.assignee === 'unassigned' ? !!item.assignee : filters.assignee && 'user:' + item.assignee !== filters.assignee) return false
  if (requirementFilter.value && item.requirementId !== requirementFilter.value) return false
  return `${item.code} ${item.title} ${item.description}`.toLowerCase().includes(filters.q.trim().toLowerCase())
}).sort((a, b) => (order.value === 'severity' ? severityRank(a.severity) - severityRank(b.severity) : 0)
  || a.code.localeCompare(b.code, 'zh-Hant', { numeric: true })))

function openForm(item?: WorkItem) {
  editing.value = item ?? null
  formError.value = null
  Object.assign(form, {
    code: item?.code ?? nextCode(), type: item?.type ?? 'Task', severity: item?.severity ?? '',
    title: item?.title ?? '', description: item?.description ?? '', status: item?.status ?? 'Ready',
    priority: item?.priority ?? 'P2', assigneeKind: item?.assigneeKind ?? 'None',
    assignee: item?.assignee ?? '', requirementId: item?.requirementId ?? requirementFilter.value,
    sprintId: item?.sprintId ?? '', milestoneId: item?.milestoneId ?? '',
    startDate: item?.startDate ?? '', dueDate: item?.dueDate ?? '', blockedReason: item?.blockedReason ?? '',
  })
  formOpen.value = true
}

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
    const deps = await api.get<WorkItemDependency[]>(`/api/work-items/${item.id}/dependencies`)
    return [item.id, deps.map(dep => dep.dependsOnId)] as const
  }))
  dependencies.value = { ...dependencies.value, ...Object.fromEntries(entries) }
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
    const all = [...page.items]
    for (let next = 2; next <= Math.ceil(page.total / page.pageSize); next++) {
      all.push(...(await api.get<Paged<WorkItem>>(`/api/projects/${id.value}/work-items`, { page: next, pageSize: 100 })).items)
    }
    items.value = all
    requirements.value = reqs
    sprints.value = sprintList
    milestones.value = milestoneList
    if (!form.code) form.code = nextCode()
    await loadDependencies(all)
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
    const url = `/api/projects/${id.value}/work-items`
    const payload = {
      ...editing.value,
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
      blockedReason: form.status === 'Blocked' ? optional(form.blockedReason.trim()) : null,
    }
    if (editing.value) await api.put<WorkItem>(url + '/' + editing.value.id, payload)
    else await api.post<WorkItem>(url, payload)
    toast.value = editing.value ? '已更新工作項目' : '已建立工作項目'
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
    toast.value = '已更新 ' + item.code
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
  const severity = severityPayload(item.type, select.value)
  select.value = item.severity ?? ''
  if (item.type !== 'Bug' || severity === item.severity) return
  busyId.value = item.id
  clearActionError()
  try {
    const updated = await api.put<WorkItem>(`/api/projects/${id.value}/work-items/${item.id}`, {
      ...item,
      severity,
    })
    const index = items.value.findIndex(candidate => candidate.id === item.id)
    if (index >= 0) items.value[index] = updated
    toast.value = '已更新 ' + item.code
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
    await loadDependencies([item])
    toast.value = '已更新相依項目'
  } catch (caught) {
    setActionError(caught)
  } finally {
    busyId.value = null
  }
}

async function removeDependency() {
  if (!dependencyTarget.value) return
  const { item, id: depId } = dependencyTarget.value
  clearActionError()
  busyId.value = item.id
  try {
    await api.del(`/api/work-items/${item.id}/dependencies/${depId}`)
    removeOpen.value = false
    await loadDependencies([item])
    toast.value = '已更新相依項目'
  } catch (caught) {
    removeOpen.value = false
    setActionError(caught)
  } finally {
    busyId.value = null
  }
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="min-w-0 space-y-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="ui-h2">工作項目</h2>
        <p class="text-sm text-ink-muted">完成前須先完成所有相依項目。設為阻塞時須填寫原因。</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <div class="flex flex-wrap gap-2" role="group" aria-label="檢視模式">
          <button
            type="button"
            class="btn"
            :class="view === 'table' ? 'btn-secondary ring-2 ring-navy-700' : 'btn-secondary'"
            :aria-pressed="view === 'table'"
            data-testid="view-table"
            @click="view = 'table'"
          >
            表格
          </button>
          <button
            type="button"
            class="btn"
            :class="view === 'board' ? 'btn-secondary ring-2 ring-navy-700' : 'btn-secondary'"
            :aria-pressed="view === 'board'"
            data-testid="view-board"
            @click="view = 'board'"
          >
            看板
          </button>
        </div>
        <button type="button" class="btn btn-primary" data-testid="new-work-item" @click="openForm()">
          新增工作項目
        </button>
      </div>
    </div>

    <ProjectFormDialog v-model:open="formOpen" :title="editing ? '編輯工作項目' : '新增工作項目'" :busy="submitting">
    <form class="space-y-4" data-testid="work-item-form" @submit.prevent="createItem">
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
          <label for="wi-severity" class="field-label">缺陷嚴重度</label>
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
      <label v-if="form.status === 'Blocked'" class="block"><span class="field-label">阻塞原因</span><textarea v-model="form.blockedReason" required class="field-input" /></label>
      <AppNotice v-if="formError" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
      <div class="flex justify-end gap-2">
        <button type="button" class="btn btn-secondary" @click="formOpen = false">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="submitting" data-testid="work-item-submit">
          {{ submitting ? '儲存中…' : editing ? '儲存工作項目' : '建立工作項目' }}
        </button>
      </div>
    </form>
    </ProjectFormDialog>

    <div class="ui-panel grid gap-4 p-4 sm:grid-cols-2 xl:grid-cols-3">
      <div>
        <label for="filter-status" class="field-label">狀態篩選</label>
        <select id="filter-status" v-model="filters.status" class="field-input">
          <option value="">全部</option>
          <option v-for="status in statuses" :key="status" :value="status">{{ workStatusLabels[status].label }}</option>
        </select>
      </div>
      <div>
        <label for="filter-type" class="field-label">類型篩選</label>
        <select id="filter-type" v-model="filters.type" class="field-input">
          <option value="">全部</option>
          <option v-for="type in types" :key="type" :value="type">{{ workTypeLabels[type] }}</option>
        </select>
      </div>
      <div class="min-w-0">
        <label for="filter-q" class="field-label">搜尋</label>
        <input id="filter-q" v-model="filters.q" type="search" class="field-input" placeholder="代碼、標題或說明">
      </div>
      <label><span class="field-label">嚴重度篩選</span><select v-model="filters.severity" class="field-input"><option value="">全部嚴重度</option><option v-for="severity in severityOptions" :key="severity" :value="severity">{{ severityLabels[severity].label }}</option></select></label>
      <label><span class="field-label">指派對象篩選</span><select v-model="filters.assignee" class="field-input"><option value="">所有對象</option><option value="unassigned">未指派</option><option v-for="assignee in assignees" :key="assignee" :value="'user:' + assignee">{{ assignee }}</option></select></label>
      <label><span class="field-label">排序</span><select v-model="order" class="field-input"><option value="code">工作項目代碼</option><option value="severity">嚴重度（最高優先）</option></select></label>
      <div class="flex flex-wrap items-center gap-3 sm:col-span-2 xl:col-span-3">
        <button type="button" class="btn btn-secondary" :aria-pressed="filters.type === 'Bug'" :class="{ 'ring-2 ring-navy-700': filters.type === 'Bug' }" @click="filters.type = filters.type === 'Bug' ? '' : 'Bug'">缺陷</button>
        <span v-if="requirementFilter" class="ui-meta">需求：{{ requirements.find(req => req.id === requirementFilter)?.code ?? '指定需求' }}</span>
        <button type="button" class="btn btn-ghost" @click="clearFilters">清除篩選</button>
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
    <div v-else-if="loadError" class="space-y-3"><AppNotice :message="loadError.detail" /><button class="btn btn-secondary" @click="load">重試</button></div>
    <AppEmpty v-else-if="!items.length" class="mt-4" title="還沒有工作項目" description="新增第一個工作項目，或從 Supervisor 匯入。"><button class="btn btn-secondary" @click="openForm()">新增工作項目</button></AppEmpty>
    <AppEmpty v-else-if="!filteredItems.length" title="找不到符合條件的工作項目"><button class="btn btn-secondary" @click="clearFilters">清除篩選</button></AppEmpty>

    <template v-else>
      <div v-if="view === 'board'" class="mt-5">
        <WorkItemBoard :items="filteredItems" :busy-id="busyId" @move="requestMove" />
      </div>

      <div v-else class="ui-panel mt-5 min-w-0">
        <table class="ui-table work-table" aria-label="工作項目">
          <thead>
            <tr>
              <th scope="col">代碼</th>
              <th scope="col">標題</th>
              <th scope="col">狀態</th>
              <th scope="col">相依</th>
              <th scope="col">類型／優先級</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in filteredItems"
              :key="item.id"
              class="align-top"
              :data-testid="`work-item-row-${item.code}`"
            >
              <td class="px-3 py-3 font-mono text-sm">{{ item.code }}</td>
              <td class="ui-wide">
                <NuxtLink :to="'/work/' + item.id" class="font-semibold text-navy-700 hover:underline">{{ item.title }}</NuxtLink> <span v-if="item.title.includes('測試資料')" class="ui-tag is-test text-sm">測試資料</span>
                <button class="btn btn-ghost" :aria-label="'編輯 ' + item.code" @click="openForm(item)">編輯</button>
                <p v-if="item.description" class="mt-0.5 line-clamp-2 text-sm text-ink-muted">{{ item.description }}</p>
                <p v-if="item.blockedReason" class="ui-callout is-fault mt-1 text-sm">阻塞原因：{{ item.blockedReason }}</p>
              </td>
              <td class="ui-wide">
                <UiPill :tone="workTone[item.status]" :label="workStatusLabels[item.status].label" />
                <label class="sr-only" :for="`status-${item.code}`">變更 {{ item.code }} 狀態</label>
                <select
                  :id="`status-${item.code}`"
                  class="field-input mt-2"
                  :value="item.status"
                  :disabled="busyId === item.id"
                  :data-testid="`status-select-${item.code}`"
                  @change="onTableStatusChange($event, item)"
                >
                  <option v-for="status in statuses" :key="status" :value="status">{{ workStatusLabels[status].label }}</option>
                </select>
              </td>
              <td class="ui-wide text-sm">
                <ul v-if="(dependencies[item.id] ?? []).length" class="space-y-1">
                  <li v-for="depId in dependencies[item.id]" :key="depId" class="flex flex-wrap items-center gap-1">
                    <span class="font-mono">{{ itemCode(depId) }}</span>
                    <button type="button" class="btn btn-danger" :aria-label="`移除 ${item.code} 對 ${itemCode(depId)} 的相依`" @click="dependencyTarget = { item, id: depId }; removeOpen = true">
                      移除
                    </button>
                  </li>
                </ul>
                <p v-else class="ui-meta">無相依項目</p>
                <div class="mt-2 flex items-center gap-1">
                  <label class="sr-only" :for="`dep-${item.code}`">選擇 {{ item.code }} 的相依項目</label>
                  <select
                    :id="`dep-${item.code}`"
                    class="field-input"
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
              <td class="ui-wide text-sm text-ink-muted">
                <p>{{ workTypeLabels[item.type] }} · {{ priorityLabels[item.priority] }}</p>
                <label v-if="item.type === 'Bug'" class="mt-1 block">
                  <span class="field-label">缺陷嚴重度</span>
                  <UiPill v-if="item.severity" :tone="severityTone[item.severity]" :label="severityLabels[item.severity].label" />
                  <select
                    class="field-input mt-2"
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

    <ConfirmDialog v-model:open="removeOpen" title="移除相依項目" tone="danger" confirm-label="確認移除" :busy="!!busyId" @confirm="removeDependency" />
    <ConfirmDialog
      v-model:open="blockedOpen"
      title="設定為阻塞"
      description="請填寫阻塞原因；原因會顯示在工作項目與專案總覽。"
      confirm-label="設為阻塞"
      :busy="busyId === blockedTarget?.id"
      @confirm="confirmBlocked"
    >
      <label for="blocked-reason" class="field-label mt-4">原因</label>
      <textarea id="blocked-reason" v-model="blockedReason" rows="3" class="field-input" data-testid="blocked-reason" />
      <p v-if="blockedError" class="ui-callout is-fault mt-2 text-sm" role="alert">{{ blockedError }}</p>
    </ConfirmDialog>
  </div>
</template>

<style scoped>
.work-table tbody tr { cursor: auto; }
@media (max-width: 760px) {
  .work-table td { min-width: 0; padding: 2px 0; }
  .work-table td:nth-child(4) { grid-column: 1 / -1; }
}
</style>
