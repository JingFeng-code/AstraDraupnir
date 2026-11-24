<script setup lang="ts">
import type { Project } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { projectStatusLabels } from '~/utils/labels'

const route = useRoute()
const api = useApi()
const id = computed(() => String(route.params.id))

const project = ref<Project | null>(null)
const loading = ref(true)
const error = ref<ApiError | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    project.value = await api.get<Project>(`/api/projects/${id.value}`)
  } catch (caught) {
    error.value = toApiError(caught)
    project.value = null
  } finally {
    loading.value = false
  }
}

watch(id, load, { immediate: true })
provide('project', project)
</script>

<template>
  <div>
    <AppLoading v-if="loading && !project" class="site-container" />
    <div v-else-if="error" class="site-container py-8">
      <AppNotice :message="error.detail" />
      <NuxtLink to="/projects" class="btn btn-secondary mt-4">返回專案列表</NuxtLink>
    </div>
    <template v-else-if="project">
      <div class="border-b border-line bg-paper">
        <div class="site-container flex flex-wrap items-start justify-between gap-3 py-4">
          <div>
            <p class="font-mono text-xs text-ink-subtle">{{ project.key }}</p>
            <h1 class="mt-0.5 text-2xl font-bold text-ink">{{ project.name }}</h1>
            <p v-if="project.description" class="mt-1 max-w-2xl text-sm text-ink-muted">{{ project.description }}</p>
          </div>
          <StatusBadge :value="projectStatusLabels[project.status]" />
        </div>
      </div>
      <ProjectTabs />
      <NuxtPage />
    </template>
  </div>
</template>
