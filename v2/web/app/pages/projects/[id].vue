<script setup lang="ts">
import type { Project, ProjectStatus } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { projectStatusLabels } from '~/utils/labels'
import { isAdmin } from '~/utils/permissions'

const route = useRoute()
const api = useApi()
const { state } = useAuth()
const canArchive = computed(() => isAdmin(state.value.user))
const { toast } = useWorkspace()
const id = computed(() => String(route.params.id))
const project = ref<Project | null>(null)
const loading = ref(true)
const error = ref<ApiError>()
const formOpen = ref(false)
const archiveOpen = ref(false)
const submitting = ref(false)
const formError = ref<ApiError>()
const form = reactive({ name: '', description: '', status: 'Active' as ProjectStatus })
const tones = { Active: 'moving', Paused: 'needs', Archived: 'idle' } as const

async function load() {
  loading.value = true
  error.value = undefined
  try { project.value = await api.get<Project>('/api/projects/' + id.value) }
  catch (caught) { error.value = toApiError(caught); project.value = null }
  finally { loading.value = false }
}

function edit() {
  if (!project.value) return
  Object.assign(form, { name: project.value.name, description: project.value.description, status: project.value.status })
  formError.value = undefined
  formOpen.value = true
}

function submit() {
  if (form.status === 'Archived' && project.value?.status !== 'Archived') {
    if (canArchive.value) archiveOpen.value = true
    return
  }
  void save()
}

async function save() {
  if (!project.value || submitting.value) return
  submitting.value = true
  formError.value = undefined
  try {
    project.value = await api.put<Project>('/api/projects/' + id.value, {
      ...project.value, name: form.name.trim(), description: form.description.trim(), status: form.status,
    })
    toast.value = '已更新專案'
    archiveOpen.value = false
    formOpen.value = false
  } catch (caught) { archiveOpen.value = false; formError.value = toApiError(caught) }
  finally { submitting.value = false }
}

watch(id, load, { immediate: true })
provide('project', project)
</script>

<template>
  <div class="min-w-0 space-y-6">
    <AppLoading v-if="loading" />
    <div v-else-if="error" class="space-y-3">
      <AppNotice :message="error.detail" />
      <div class="flex flex-wrap gap-2"><button class="btn btn-secondary" @click="load">重試</button><NuxtLink to="/projects" class="btn btn-ghost">返回專案列表</NuxtLink></div>
    </div>
    <template v-else-if="project">
      <header class="ui-panel p-5">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <span class="ui-meta font-mono">{{ project.key }}</span>
              <UiPill :tone="tones[project.status]" :label="projectStatusLabels[project.status].label" />
              <span v-if="project.name.includes('測試資料')" class="ui-tag is-test text-sm">測試資料</span>
            </div>
            <h1 class="ui-h1 mt-3 break-words">{{ project.name }}</h1>
            <p v-if="project.description" class="ui-lead break-words">{{ project.description }}</p>
          </div>
          <button class="btn btn-secondary" @click="edit">編輯專案</button>
        </div>
      </header>
      <ProjectTabs />
      <NuxtPage />
      <ProjectFormDialog v-model:open="formOpen" title="編輯專案" :busy="submitting">
        <form class="space-y-4" @submit.prevent="submit">
          <label class="block"><span class="field-label">名稱</span><input v-model="form.name" required class="field-input"></label>
          <label class="block"><span class="field-label">說明</span><textarea v-model="form.description" rows="3" class="field-input" /></label>
          <label class="block"><span class="field-label">狀態</span><select v-model="form.status" class="field-input"><option v-for="(label, value) in projectStatusLabels" :key="value" :value="value" :disabled="value === 'Archived' && !canArchive && project.status !== 'Archived'">{{ label.label }}</option></select></label>
          <AppNotice v-if="formError" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
          <div class="flex flex-wrap justify-end gap-2"><button type="button" class="btn btn-secondary" :disabled="submitting" @click="formOpen = false">取消</button><button class="btn btn-primary" :disabled="submitting">儲存專案</button></div>
        </form>
      </ProjectFormDialog>
      <ConfirmDialog v-model:open="archiveOpen" title="封存專案" description="專案狀態將改為已封存。" tone="danger" confirm-label="確認封存" :busy="submitting" @confirm="save" />
    </template>
    <AppEmpty v-else title="找不到專案"><NuxtLink to="/projects" class="btn btn-secondary">返回專案列表</NuxtLink></AppEmpty>
  </div>
</template>
