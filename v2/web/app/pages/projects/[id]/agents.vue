<script setup lang="ts">
import type { AgentRun } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDateTime, relativeTime } from '~/utils/format'
import { runSourceLabels } from '~/utils/labels'

const route = useRoute()
const api = useApi()
const id = computed(() => String(route.params.id))

const runs = ref<AgentRun[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    runs.value = await api.get<AgentRun[]>('/api/agent-runs', { projectId: id.value })
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="site-container py-6">
    <h2 class="text-xl font-bold text-ink">Agent 執行</h2>
    <p class="text-sm text-ink-muted">來源：Supervisor／Gleipnir／手動匯入。匯入為冪等，重複匯入會更新同一筆嘗試。</p>

    <AppLoading v-if="loading" />
    <AppNotice v-else-if="error" class="mt-4" :message="error.detail" />
    <AppEmpty
      v-else-if="!runs.length"
      class="mt-4"
      title="還沒有 Agent 執行"
      description="從「匯入 Supervisor」貼上 queue.json 或上傳檔案，就會出現在這裡。"
    >
      <NuxtLink :to="`/projects/${id}/import`" class="btn btn-primary">前往匯入</NuxtLink>
    </AppEmpty>
    <div v-else class="table-wrap panel mt-4">
      <table class="w-full text-left text-sm">
        <thead class="bg-surface-muted text-xs text-ink-subtle">
          <tr>
            <th class="px-4 py-2 font-medium">外部任務</th>
            <th class="px-4 py-2 font-medium">Agent</th>
            <th class="px-4 py-2 font-medium">狀態</th>
            <th class="px-4 py-2 font-medium">來源</th>
            <th class="px-4 py-2 font-medium">嘗試</th>
            <th class="px-4 py-2 font-medium">時間</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="run in runs" :key="run.id" class="border-t border-line">
            <td class="px-4 py-2 font-mono text-xs">{{ run.externalTaskId }}</td>
            <td class="px-4 py-2">{{ run.agent }}<span v-if="run.model" class="text-xs text-ink-subtle"> · {{ run.model }}</span></td>
            <td class="px-4 py-2"><span class="font-mono text-xs font-semibold">{{ run.status }}</span></td>
            <td class="px-4 py-2 text-xs">{{ runSourceLabels[run.source] }}</td>
            <td class="px-4 py-2 text-xs">{{ run.attempt }}</td>
            <td class="px-4 py-2 text-xs text-ink-muted" :title="formatDateTime(run.finishedAt ?? run.startedAt)">
              {{ relativeTime(run.finishedAt ?? run.startedAt) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
