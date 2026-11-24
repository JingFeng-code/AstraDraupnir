<script setup lang="ts">
import type { Paged, Priority, Requirement, RequirementSource, RequirementStatus, WorkItem } from '~/types/api'
import { requirementProgress } from '~/utils/workItemForm'
import { toApiError, type ApiError } from '~/utils/errors'
import { priorityLabels, requirementSourceLabels, requirementStatusLabels } from '~/utils/labels'

const route = useRoute()
const api = useApi()
const id = computed(() => String(route.params.id))

const { toast } = useWorkspace()
const editing = ref<string | null>(null)
const filter = ref<RequirementStatus | 'Partial' | 'Blocked' | ''>('')
const workItems = ref<WorkItem[]>([])
const items = ref<Requirement[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const submitting = ref(false)
const formError = ref<ApiError | null>(null)
const formOpen = ref(false)

const priorities: Priority[] = ['P0', 'P1', 'P2', 'P3']
const statuses: RequirementStatus[] = ['Draft', 'Approved', 'Implemented', 'Verified']
const sources: RequirementSource[] = ['UserAuthorized', 'Repository', 'ClaudeDecision', 'External']

const form = reactive({
  code: '',
  title: '',
  body: '',
  priority: 'P2' as Priority,
  status: 'Draft' as RequirementStatus,
  source: 'UserAuthorized' as RequirementSource,
})

const tones = { Draft: 'idle', Approved: 'needs', Implemented: 'moving', Verified: 'proven' } as const
const progress = computed(() => requirementProgress(workItems.value))
const filtered = computed(() => items.value.filter(item => !filter.value
  || item.status === filter.value || progress.value.get(item.id)?.status === filter.value))

function openForm(item?: Requirement) {
  editing.value = item?.id ?? null
  formError.value = null
  Object.assign(form, item
    ? { code: item.code, title: item.title, body: item.body, priority: item.priority, status: item.status, source: item.source }
    : { code: nextCode(), title: '', body: '', priority: 'P2', status: 'Draft', source: 'UserAuthorized' })
  formOpen.value = true
}

function nextCode(): string {
  const numbers = items.value
    .map(item => Number(item.code.replace(/^REQ-/, '')))
    .filter(number => Number.isFinite(number))
  return `REQ-${numbers.length ? Math.max(...numbers) + 1 : 1}`
}

async function load() {
  loading.value = true
  error.value = null
  try {
    const projectPath = `/api/projects/${id.value}`
    const [requirements, first] = await Promise.all([
      api.get<Requirement[]>(projectPath + '/requirements'),
      api.get<Paged<WorkItem>>(projectPath + '/work-items', { page: 1, pageSize: 100 }),
    ])
    const all = [...first.items]
    for (let page = 2; page <= Math.ceil(first.total / first.pageSize); page++) {
      all.push(...(await api.get<Paged<WorkItem>>(projectPath + '/work-items', { page, pageSize: 100 })).items)
    }
    items.value = requirements
    workItems.value = all
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

async function create() {
  if (submitting.value) return
  formError.value = null
  submitting.value = true
  try {
    const url = `/api/projects/${id.value}/requirements`
    const payload = {
      code: form.code.trim(),
      title: form.title.trim(),
      body: form.body.trim(),
      priority: form.priority,
      status: form.status,
      source: form.source,
    }
    if (editing.value) await api.put<Requirement>(url + '/' + editing.value, payload)
    else await api.post<Requirement>(url, payload)
    toast.value = editing.value ? '已更新需求' : '已建立需求'
    formOpen.value = false
    await load()
  } catch (caught) {
    formError.value = toApiError(caught)
  } finally {
    submitting.value = false
  }
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="space-y-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="ui-h2">需求</h2>
        <p class="text-sm text-ink-muted">專案內唯一的 REQ 編號；工作項目可以連到需求。</p>
      </div>
      <button type="button" class="btn btn-primary" data-testid="new-requirement" @click="openForm()">
        新增需求
      </button>
    </div>

    <ProjectFormDialog v-model:open="formOpen" :title="editing ? '編輯需求' : '新增需求'" :busy="submitting">
    <form class="space-y-4" data-testid="requirement-form" @submit.prevent="create">
      <div class="grid gap-4 sm:grid-cols-3">
        <div>
          <label for="req-code" class="field-label">編號</label>
          <input id="req-code" v-model="form.code" required pattern="REQ-[1-9][0-9]*" class="field-input font-mono" data-testid="requirement-code">
        </div>
        <div class="sm:col-span-2">
          <label for="req-title" class="field-label">標題</label>
          <input id="req-title" v-model="form.title" required class="field-input" data-testid="requirement-title">
        </div>
      </div>
      <div>
        <label for="req-body" class="field-label">內容</label>
        <textarea id="req-body" v-model="form.body" rows="2" class="field-input" data-testid="requirement-body" />
      </div>
      <div class="grid gap-4 sm:grid-cols-3">
        <div>
          <label for="req-priority" class="field-label">優先級</label>
          <select id="req-priority" v-model="form.priority" class="field-input">
            <option v-for="priority in priorities" :key="priority" :value="priority">{{ priorityLabels[priority] }}</option>
          </select>
        </div>
        <div>
          <label for="req-status" class="field-label">狀態</label>
          <select id="req-status" v-model="form.status" class="field-input">
            <option v-for="status in statuses" :key="status" :value="status">{{ requirementStatusLabels[status].label }}</option>
          </select>
        </div>
        <div>
          <label for="req-source" class="field-label">來源</label>
          <select id="req-source" v-model="form.source" class="field-input">
            <option v-for="source in sources" :key="source" :value="source">{{ requirementSourceLabels[source] }}</option>
          </select>
        </div>
      </div>
      <AppNotice v-if="formError" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
      <div class="flex justify-end gap-2">
        <button type="button" class="btn btn-secondary" @click="formOpen = false">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="submitting" data-testid="requirement-submit">
          {{ submitting ? '儲存中…' : editing ? '儲存需求' : '建立需求' }}
        </button>
      </div>
    </form>
    </ProjectFormDialog>

    <AppLoading v-if="loading" />
    <div v-else-if="error" class="space-y-3"><AppNotice :message="error.detail" /><button class="btn btn-secondary" @click="load">重試</button></div>
    <AppEmpty v-else-if="!items.length" title="還沒有需求" description="建立需求後，就能將工作項目連到需求。">
      <button class="btn btn-secondary" @click="openForm()">新增需求</button>
    </AppEmpty>
    <template v-else>
      <label class="block max-w-xs"><span class="field-label">需求狀態篩選</span>
        <select v-model="filter" class="field-input">
          <option value="">全部狀態</option>
          <option v-for="status in statuses" :key="status" :value="status">{{ requirementStatusLabels[status].label }}</option>
          <option value="Partial">部分完成（依工作項目）</option><option value="Blocked">阻塞（依工作項目）</option>
        </select>
      </label>
      <AppEmpty v-if="!filtered.length" title="找不到符合條件的需求"><button class="btn btn-secondary" @click="filter = ''">清除篩選</button></AppEmpty>
      <div v-else class="ui-panel">
        <table class="ui-table" aria-label="需求">
          <thead><tr><th scope="col">編號與需求</th><th scope="col">狀態</th><th scope="col">優先級與來源</th><th scope="col">工作項目</th><th scope="col">操作</th></tr></thead>
          <tbody>
            <tr v-for="item in filtered" :key="item.id">
              <td>
                <p class="ui-meta font-mono">{{ item.code }}</p>
                <p class="font-semibold break-words">{{ item.title }}</p>
                <span v-if="item.title.includes('測試資料')" class="ui-tag is-test text-sm">測試資料</span>
                <p v-if="item.body" class="ui-meta mt-1 break-words">{{ item.body }}</p>
              </td>
              <td class="ui-wide">
                <div class="flex flex-wrap gap-2">
                  <UiPill :tone="tones[item.status]" :label="requirementStatusLabels[item.status].label" />
                  <UiPill v-if="progress.get(item.id)?.status === 'Partial'" tone="moving" label="工作部分完成" />
                  <UiPill v-if="progress.get(item.id)?.status === 'Blocked'" tone="fault" label="工作阻塞" />
                </div>
              </td>
              <td class="ui-meta">{{ priorityLabels[item.priority] }}<br>{{ requirementSourceLabels[item.source] }}</td>
              <td><NuxtLink :to="{ path: '/projects/' + id + '/work-items', query: { requirement: item.id } }" class="text-navy-700 underline whitespace-nowrap" :aria-label="(progress.get(item.id)?.total ?? 0) + ' 個工作項目：' + item.code">{{ progress.get(item.id)?.total ?? 0 }} 個工作項目</NuxtLink></td>
              <td><button class="btn btn-secondary whitespace-nowrap" :aria-label="'編輯 ' + item.code" @click="openForm(item)">編輯</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>

<style scoped>
.ui-table tbody tr { cursor: auto; }
</style>
