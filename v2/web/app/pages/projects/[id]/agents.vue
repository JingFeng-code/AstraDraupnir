<script setup lang="ts">
import type { AgentRun } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDateTime } from '~/utils/format'
import { runSourceLabels } from '~/utils/labels'
import { isTestData, RUN_LABEL, runTone } from '~/lib/engineering'
import { agentName, SNAPSHOT_NOTE } from '~/lib/evidenceRules'

const route = useRoute()
const api = useApi()
const ws = useWorkspace()
const id = computed(() => String(route.params.id))
const runs = ref<AgentRun[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const source = ref('all')
const agent = computed(() => [...new Set(runs.value.map(r => r.agent))])
const filtered = computed(() => runs.value.filter(r => source.value === 'all' || r.source === source.value))
const workItem = (run: AgentRun) => run.workItemId ? ws.byId.value.items.get(run.workItemId) : undefined
const resultText = (run: AgentRun) => run.status === 'DONE' ? '已完成' : `紀錄停在「${RUN_LABEL[run.status] ?? run.status}」`
const sourceText = (run: AgentRun) => run.source === 'Manual' ? '手動' : `${runSourceLabels[run.source]} 匯入`

async function load() {
  loading.value = true
  error.value = null
  try { runs.value = await api.get<AgentRun[]>('/api/agent-runs', { projectId: id.value }) }
  catch (caught) { error.value = toApiError(caught) }
  finally { loading.value = false }
}
watch(id, load, { immediate: true })
</script>

<template>
  <div>
    <h1 class="ui-h1">專案 Agent 執行</h1>
    <p class="ui-lead">這個專案的 Agent 執行歷史。名稱「Unknown」代表原始紀錄沒有寫明執行者，介面顯示為「未確認」，不猜測。</p>
    <div class="ui-callout" style="margin-top: 20px; max-width: 900px">
      <p class="ui-h3">這是匯入快照，不是即時狀態</p>
      <p class="ui-meta">紀錄來自 Supervisor／Gleipnir／手動匯入；最後看到的狀態可能是「實作中」或「審查中」，但 Draupnir 沒有連到 Agent，現在不一定還在執行。</p>
      <p class="ui-meta">{{ SNAPSHOT_NOTE }}。時間為紀錄時間，不是最後活動時間。</p>
    </div>

    <AppLoading v-if="loading" />
    <div v-else-if="error" class="ui-callout is-fault" style="margin-top: 20px"><p>{{ error.detail }}</p><button type="button" class="ui-btn is-quiet" @click="load">重新載入</button></div>
    <template v-else>
      <div style="display: flex; flex-wrap: wrap; gap: 12px; align-items: end; margin-top: 20px">
        <label class="ui-stack" style="gap: 6px"><span class="ui-meta">來源</span><select v-model="source" class="ui-input"><option value="all">全部</option><option v-for="s in ['Supervisor','Gleipnir','Manual'] as const" :key="s" :value="s">{{ s === 'Supervisor' ? 'Supervisor 匯入' : s === 'Gleipnir' ? 'Gleipnir 匯入' : '手動' }}</option></select></label>
        <span class="ui-meta" style="margin-left: auto">{{ filtered.length }} 筆執行紀錄 · {{ agent.length }} 個 Agent</span>
      </div>
      <AppEmpty v-if="!filtered.length" class="ui-panel" style="margin-top: 18px" title="這個專案還沒有 Agent 執行" description="從「匯入 Supervisor」貼上快照後，紀錄會出現在這裡。"><NuxtLink :to="`/projects/${id}/import`" class="ui-btn is-primary">前往匯入</NuxtLink></AppEmpty>
      <div v-else class="ui-panel" style="margin-top: 18px">
        <div style="overflow-x: auto" role="region" aria-label="Agent 執行紀錄" tabindex="0">
          <table class="ui-table">
            <thead><tr><th>Agent</th><th>工作項目／外部任務</th><th>結果</th><th>Model</th><th>嘗試</th><th>紀錄時間</th><th>來源</th></tr></thead>
            <tbody>
              <tr v-for="run in filtered" :key="run.id" @click="run.workItemId && navigateTo(`/work/${run.workItemId}`)">
                <td style="white-space: nowrap; font-weight: 600">{{ agentName(run.agent) }}</td>
                <td class="ui-wide"><span v-if="workItem(run)"><NuxtLink :to="`/work/${run.workItemId}`" style="text-decoration: underline">{{ workItem(run)?.code }} {{ workItem(run)?.title }}</NuxtLink></span><span v-else class="font-mono">{{ run.externalTaskId }}</span><span v-if="isTestData(run.externalTaskId) || isTestData(workItem(run)?.title)" class="ui-tag is-test" style="margin-left: 8px">測試資料</span></td>
                <td><UiPill :tone="runTone(run.status)" :label="resultText(run)" /></td>
                <td class="ui-meta">{{ run.model || '未記錄' }}</td>
                <td>{{ run.attempt }}</td>
                <td class="ui-meta" style="white-space: nowrap">{{ formatDateTime(run.finishedAt ?? run.startedAt) }}</td>
                <td><span class="ui-tag">{{ sourceText(run) }}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </div>
</template>
