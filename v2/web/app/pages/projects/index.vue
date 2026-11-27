<script setup lang="ts">
import type { Dashboard, Project, ProjectStatus } from '~/types/api'
import { projectStatusLabels } from '~/utils/labels'
import { toApiError, type ApiError } from '~/utils/errors'
import { canCreateProjects } from '~/utils/permissions'

const api = useApi()
const { state } = useAuth()
const { toast } = useWorkspace()
const canCreate = computed(() => canCreateProjects(state.value.user))
const projects = ref<Project[]>([])
const loading = ref(true)
const error = ref<ApiError>()
const query = ref('')
const status = ref<ProjectStatus | ''>('')
const order = ref<'recent' | 'name' | 'key'>('recent')
const page = ref(1)
const perPage = 20
const filtered = computed(() => {
  const needle = query.value.trim().toLowerCase()
  return projects.value.filter(p => (!status.value || p.status === status.value)
    && (!needle || [p.key, p.name, p.description].some(value => value.toLowerCase().includes(needle))))
    .sort((a, b) => order.value === 'recent' ? b.updatedAt.localeCompare(a.updatedAt) || a.key.localeCompare(b.key)
      : a[order.value].localeCompare(b[order.value], 'zh-Hant'))
})
const pages = computed(() => Math.max(1, Math.ceil(filtered.value.length / perPage)))
const visible = computed(() => filtered.value.slice((page.value - 1) * perPage, page.value * perPage))
watch([query, status, order], () => { page.value = 1 })
watch(pages, count => { page.value = Math.min(page.value, count) })

const progress = reactive<Record<string, { dashboard?: Dashboard, error?: string }>>({})
const tones = { Active: 'moving', Paused: 'needs', Archived: 'idle' } as const
async function loadProgress(id: string) {
  progress[id] = {}
  try {
    progress[id] = { dashboard: await api.get<Dashboard>('/api/dashboard', { projectId: id }) }
  } catch (caught) {
    progress[id] = { error: toApiError(caught).detail }
  }
}
watch(visible, list => {
  for (const project of list) if (!progress[project.id]) void loadProgress(project.id)
})
function total(dashboard: Dashboard) {
  return Object.values(dashboard.statusCounts).reduce((sum, amount) => sum + amount, 0)
}
async function load() {
  loading.value = true
  error.value = undefined
  try { projects.value = await api.get<Project[]>('/api/projects') }
  catch (caught) { error.value = toApiError(caught) }
  finally { loading.value = false }
}
const formOpen = ref(false)
const submitting = ref(false)
const formError = ref<ApiError>()
const form = reactive({ key: '', name: '', description: '' })
async function createProject() {
  if (submitting.value || !canCreate.value) return
  submitting.value = true
  formError.value = undefined
  try {
    const project = await api.post<Project>('/api/projects', {
      key: form.key.trim().toUpperCase(), name: form.name.trim(), description: form.description.trim(),
    })
    formOpen.value = false
    toast.value = '已建立專案'
    await navigateTo('/projects/' + project.id)
  } catch (caught) {
    formError.value = toApiError(caught)
  } finally { submitting.value = false }
}
onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <header class="flex flex-wrap items-start justify-between gap-4">
      <div><h1 class="ui-h1">專案</h1><p class="ui-lead">查看各專案的工作進度與待核准事項。</p></div>
      <button v-if="canCreate" class="btn btn-primary" data-testid="new-project" @click="formOpen = true">新增專案</button>
    </header>
    <ProjectFormDialog v-model:open="formOpen" title="新增專案" :busy="submitting">
      <form class="space-y-4" data-testid="project-form" @submit.prevent="createProject">
        <label class="block"><span class="field-label">Key（2–10 個大寫字母）</span><input v-model="form.key" required pattern="[A-Za-z]{2,10}" class="field-input" data-testid="project-key"></label>
        <label class="block"><span class="field-label">名稱</span><input v-model="form.name" required class="field-input" data-testid="project-name"></label>
        <label class="block"><span class="field-label">說明</span><textarea v-model="form.description" rows="3" class="field-input" data-testid="project-description" /></label>
        <AppNotice v-if="formError" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
        <div class="flex flex-wrap justify-end gap-2">
          <button type="button" class="btn btn-secondary" :disabled="submitting" @click="formOpen = false">取消</button>
          <button class="btn btn-primary" :disabled="submitting" data-testid="project-submit">{{ submitting ? '建立中…' : '建立專案' }}</button>
        </div>
      </form>
    </ProjectFormDialog>
    <AppLoading v-if="loading" />
    <div v-else-if="error" class="space-y-3"><AppNotice :message="error.detail" /><button class="btn btn-secondary" @click="load">重試</button></div>
    <AppEmpty v-else-if="!projects.length" title="還沒有專案" description="建立第一個專案以追蹤交付。">
      <button v-if="canCreate" class="btn btn-secondary" @click="formOpen = true">新增專案</button>
      <NuxtLink v-else to="/" class="btn btn-secondary">返回工作區</NuxtLink>
    </AppEmpty>
    <template v-else>
      <div class="ui-panel grid gap-4 p-4 sm:grid-cols-3">
        <label><span class="field-label">搜尋專案</span><input v-model="query" type="search" class="field-input" placeholder="Key、名稱或說明"></label>
        <label><span class="field-label">狀態篩選</span><select v-model="status" class="field-input"><option value="">全部狀態</option><option v-for="(label, value) in projectStatusLabels" :key="value" :value="value">{{ label.label }}</option></select></label>
        <label><span class="field-label">排序</span><select v-model="order" class="field-input"><option value="recent">最近更新</option><option value="name">專案名稱</option><option value="key">專案 Key</option></select></label>
      </div>
      <AppEmpty v-if="!visible.length" title="找不到符合條件的專案"><button class="btn btn-secondary" @click="query = ''; status = ''">清除篩選</button></AppEmpty>
      <ul v-else class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <li v-for="project in visible" :key="project.id" class="ui-panel project-card min-w-0 p-5">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <span class="ui-meta font-mono">{{ project.key }}</span><UiPill :tone="tones[project.status]" :label="projectStatusLabels[project.status].label" />
          </div>
          <h2 class="ui-h2 mt-3 break-words"><NuxtLink :to="'/projects/' + project.id" class="project-link text-navy-700 hover:underline" :data-testid="'project-card-' + project.key">{{ project.name }}</NuxtLink></h2>
          <span v-if="project.name.includes('測試資料')" class="ui-tag is-test text-sm">測試資料</span>
          <p v-if="project.description" class="ui-meta mt-2 break-words">{{ project.description }}</p>
          <template v-if="progress[project.id]?.dashboard">
            <div class="ui-meta mt-4 flex flex-wrap justify-between gap-2">
              <span>完成 {{ progress[project.id]!.dashboard!.statusCounts.Done }} / {{ total(progress[project.id]!.dashboard!) }} 個工作項目</span>
              <span>待核准 Human Gate {{ progress[project.id]!.dashboard!.pendingGates.length }}</span>
            </div>
            <progress class="project-progress mt-2 w-full" :value="progress[project.id]!.dashboard!.statusCounts.Done" :max="total(progress[project.id]!.dashboard!) || 1" :aria-label="project.name + ' 的工作項目進度'" />
          </template>
          <div v-else-if="progress[project.id]?.error" class="mt-4 space-y-2"><AppNotice :message="progress[project.id]!.error!" /><button class="btn btn-secondary relative z-10" @click="loadProgress(project.id)">重試進度</button></div>
          <AppLoading v-else class="mt-3" />
        </li>
      </ul>
      <nav v-if="pages > 1" class="flex flex-wrap items-center justify-between gap-3" aria-label="專案分頁">
        <p class="ui-meta" aria-live="polite">第 {{ page }} / {{ pages }} 頁，共 {{ filtered.length }} 個專案</p>
        <div class="flex gap-2"><button class="btn btn-secondary" :disabled="page === 1" @click="page--">上一頁</button><button class="btn btn-secondary" :disabled="page === pages" @click="page++">下一頁</button></div>
      </nav>
    </template>
  </div>
</template>

<style scoped>
.project-card { position: relative; }
.project-link::after { position: absolute; inset: 0; content: ''; border-radius: var(--radius-2); }
.project-progress { appearance: none; display: block; height: 8px; overflow: hidden; border: 0; border-radius: 6px; background: var(--c-sunken); }
.project-progress::-webkit-progress-bar { background: var(--c-sunken); }
.project-progress::-webkit-progress-value { background: var(--c-brand); }
.project-progress::-moz-progress-bar { background: var(--c-brand); }
</style>
