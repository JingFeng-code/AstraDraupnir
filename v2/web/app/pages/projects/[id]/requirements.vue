<script setup lang="ts">
import type { Priority, Requirement, RequirementSource, RequirementStatus } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { priorityLabels, requirementSourceLabels, requirementStatusLabels } from '~/utils/labels'

const route = useRoute()
const api = useApi()
const id = computed(() => String(route.params.id))

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
    items.value = await api.get<Requirement[]>(`/api/projects/${id.value}/requirements`)
    if (!form.code) form.code = nextCode()
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
    await api.post<Requirement>(`/api/projects/${id.value}/requirements`, {
      code: form.code.trim(),
      title: form.title.trim(),
      body: form.body.trim(),
      priority: form.priority,
      status: form.status,
      source: form.source,
    })
    form.title = ''
    form.body = ''
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
  <div class="site-container py-6">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold text-ink">需求</h2>
        <p class="text-sm text-ink-muted">專案內唯一的 REQ 編號；工作項目可以連到需求。</p>
      </div>
      <button type="button" class="btn btn-primary" data-testid="new-requirement" @click="formOpen = !formOpen">
        {{ formOpen ? '收起表單' : '新增需求' }}
      </button>
    </div>

    <form v-if="formOpen" class="panel mt-4 space-y-4 p-5" data-testid="requirement-form" @submit.prevent="create">
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
          {{ submitting ? '建立中…' : '建立需求' }}
        </button>
      </div>
    </form>

    <AppLoading v-if="loading" />
    <AppNotice v-else-if="error" class="mt-4" :message="error.detail" />
    <AppEmpty v-else-if="!items.length" class="mt-4" title="還沒有需求" description="新增第一個需求，讓工作項目有依據。" />
    <div v-else class="table-wrap panel mt-4">
      <table class="w-full text-left text-sm">
        <thead class="bg-surface-muted text-xs text-ink-subtle">
          <tr>
            <th class="px-4 py-2 font-medium">編號</th>
            <th class="px-4 py-2 font-medium">標題</th>
            <th class="px-4 py-2 font-medium">優先級</th>
            <th class="px-4 py-2 font-medium">狀態</th>
            <th class="px-4 py-2 font-medium">來源</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in items" :key="item.id" class="border-t border-line">
            <td class="px-4 py-2 font-mono text-xs">{{ item.code }}</td>
            <td class="px-4 py-2">
              <p class="font-medium text-ink">{{ item.title }}</p>
              <p v-if="item.body" class="mt-0.5 line-clamp-2 text-xs text-ink-muted">{{ item.body }}</p>
            </td>
            <td class="px-4 py-2 text-xs">{{ priorityLabels[item.priority] }}</td>
            <td class="px-4 py-2"><StatusBadge :value="requirementStatusLabels[item.status]" /></td>
            <td class="px-4 py-2 text-xs text-ink-muted">{{ requirementSourceLabels[item.source] }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
