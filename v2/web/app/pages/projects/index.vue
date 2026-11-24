<script setup lang="ts">
import type { Dashboard, Project } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { projectStatusLabels, workStatusLabels } from '~/utils/labels'

const api = useApi()

interface ProjectCardData {
  project: Project
  dashboard: Dashboard | null
}

const cards = ref<ProjectCardData[]>([])
const loading = ref(true)
const loadError = ref<ApiError | null>(null)

const formOpen = ref(false)
const submitting = ref(false)
const formError = ref<ApiError | null>(null)
const form = reactive({ key: '', name: '', description: '' })

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const projects = await api.get<Project[]>('/api/projects')
    cards.value = await Promise.all(projects.map(async (project) => {
      try {
        const dashboard = await api.get<Dashboard>('/api/dashboard', { projectId: project.id })
        return { project, dashboard }
      } catch {
        return { project, dashboard: null }
      }
    }))
  } catch (caught) {
    loadError.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

function activeCount(card: ProjectCardData): number {
  if (!card.dashboard) return 0
  return (card.dashboard.statusCounts.InProgress ?? 0) + (card.dashboard.statusCounts.InReview ?? 0)
}

function pendingGateCount(card: ProjectCardData): number {
  return card.dashboard?.pendingGates.length ?? 0
}

async function createProject() {
  if (submitting.value) return
  formError.value = null
  submitting.value = true
  try {
    const project = await api.post<Project>('/api/projects', {
      key: form.key.trim().toUpperCase(),
      name: form.name.trim(),
      description: form.description.trim(),
    })
    form.key = ''
    form.name = ''
    form.description = ''
    formOpen.value = false
    await navigateTo(`/projects/${project.id}`)
  } catch (caught) {
    formError.value = toApiError(caught)
  } finally {
    submitting.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="site-container py-8">
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <p class="font-mono text-xs uppercase tracking-wider text-accent">Projects</p>
        <h1 class="mt-1 text-3xl font-bold text-ink">專案</h1>
        <p class="mt-1 text-sm text-ink-muted">建立專案、追蹤需求與工作項目，並管理 Agent 交付。</p>
      </div>
      <button type="button" class="btn btn-primary" data-testid="new-project" @click="formOpen = !formOpen">
        {{ formOpen ? '收起表單' : '新增專案' }}
      </button>
    </div>

    <form v-if="formOpen" class="panel mt-6 space-y-4 p-6" data-testid="project-form" @submit.prevent="createProject">
      <h2 class="text-lg font-bold text-ink">新增專案</h2>
      <div class="grid gap-4 sm:grid-cols-3">
        <div>
          <label for="project-key" class="field-label">Key（2–10 個大寫字母）</label>
          <input id="project-key" v-model="form.key" required pattern="[A-Za-z]{2,10}" class="field-input font-mono" data-testid="project-key">
        </div>
        <div class="sm:col-span-2">
          <label for="project-name" class="field-label">名稱</label>
          <input id="project-name" v-model="form.name" required class="field-input" data-testid="project-name">
        </div>
      </div>
      <div>
        <label for="project-description" class="field-label">說明</label>
        <textarea id="project-description" v-model="form.description" rows="2" class="field-input" data-testid="project-description" />
      </div>
      <AppNotice v-if="formError" :message="formError.detail" :details="Object.values(formError.fields).flat()" />
      <div class="flex justify-end gap-2">
        <button type="button" class="btn btn-secondary" @click="formOpen = false">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="submitting" data-testid="project-submit">
          {{ submitting ? '建立中…' : '建立專案' }}
        </button>
      </div>
    </form>

    <AppLoading v-if="loading" />
    <AppNotice v-else-if="loadError" class="mt-6" :message="loadError.detail" />
    <AppEmpty
      v-else-if="cards.length === 0"
      class="mt-6"
      title="還沒有專案"
      description="建立第一個專案，開始追蹤需求、工作項目與 Human Gate。"
    />
    <ul v-else class="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      <li v-for="card in cards" :key="card.project.id">
        <NuxtLink
          :to="`/projects/${card.project.id}`"
          class="panel block h-full p-5 no-underline transition-colors hover:border-navy-600"
          :data-testid="`project-card-${card.project.key}`"
        >
          <div class="flex items-center justify-between gap-2">
            <span class="font-mono text-xs text-ink-subtle">{{ card.project.key }}</span>
            <StatusBadge :value="projectStatusLabels[card.project.status]" />
          </div>
          <h2 class="mt-2 text-lg font-bold text-ink">{{ card.project.name }}</h2>
          <p class="mt-1 line-clamp-2 text-sm text-ink-muted">{{ card.project.description || '沒有說明' }}</p>
          <dl class="mt-4 grid grid-cols-2 gap-2 border-t border-line pt-3 text-sm">
            <div>
              <dt class="text-xs text-ink-subtle">進行中工作</dt>
              <dd class="font-semibold text-ink">{{ activeCount(card) }}</dd>
            </div>
            <div>
              <dt class="text-xs text-ink-subtle">待批 Gate</dt>
              <dd class="font-semibold" :class="pendingGateCount(card) ? 'text-[#8a5300]' : 'text-ink'">{{ pendingGateCount(card) }}</dd>
            </div>
          </dl>
        </NuxtLink>
      </li>
    </ul>

    <p class="mt-8 text-xs text-ink-subtle">
      工作狀態：{{ Object.values(workStatusLabels).map(status => status.label).join('、') }}
    </p>
  </div>
</template>
