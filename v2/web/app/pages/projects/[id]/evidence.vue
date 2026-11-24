<script setup lang="ts">
import type { Evidence, EvidenceKind, EvidenceResult, Review } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDateTime } from '~/utils/format'
import { evidenceKindLabels, verdictLabels } from '~/utils/labels'
import { parseSpoolEvidence } from '~/utils/spoolEvidence'
import { isTestData } from '~/lib/engineering'
import type { Tone } from '~/lib/engineering'

import { canImport } from '~/utils/permissions'

const route = useRoute()
const api = useApi()
const auth = useAuth()
const admin = computed(() => canImport(auth.state.value.user))
const ws = useWorkspace()
const id = computed(() => String(route.params.id))
const evidence = ref<Evidence[]>([])
const reviews = ref<Review[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const resultFilter = ref<'all' | EvidenceResult>('all')
const kindFilter = ref<'all' | EvidenceKind>('all')
const spoolPayload = ref('')
const spoolFile = ref('')
const spoolBusy = ref(false)
const spoolError = ref<string | null>(null)

const tone: Record<EvidenceResult, Tone> = { Pass: 'proven', Fail: 'fault', NotTested: 'idle', Blocked: 'needs' }
const resultText: Record<EvidenceResult, string> = { Pass: '通過', Fail: '失敗', NotTested: '未執行', Blocked: '受阻' }
const traceEvidence = computed(() => evidence.value
  .filter(item => item.kind === 'Trace')
  .map(item => ({ item, trace: parseSpoolEvidence(item.data) }))
  .filter(({ trace }) => trace !== null))
const filtered = computed(() => evidence.value.filter(item =>
  (resultFilter.value === 'all' || item.result === resultFilter.value) &&
  (kindFilter.value === 'all' || item.kind === kindFilter.value)))
const reviewText = (review: Review) => typeof review.findings === 'string' ? review.findings : JSON.stringify(review.findings ?? '—')
const itemOf = (workItemId: string | null) => workItemId ? ws.byId.value.items.get(workItemId) : undefined
const resultTone = (result: EvidenceResult) => tone[result]

async function load() {
  loading.value = true
  error.value = null
  try {
    [evidence.value, reviews.value] = await Promise.all([
      api.get<Evidence[]>('/api/evidence', { projectId: id.value }),
      api.get<Review[]>('/api/reviews', { projectId: id.value }),
    ])
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}
async function importSpool() {
  spoolError.value = null
  let parsed: unknown
  try { parsed = JSON.parse(spoolPayload.value) } catch {
    spoolError.value = '內容不是合法 JSON；請貼上 spool-result/v1。'
    return
  }
  spoolBusy.value = true
  try {
    const result = await api.post<{ imported: boolean }>(`/api/projects/${id.value}/spool-results`, parsed)
    ws.toast.value = result.imported ? '已記錄 Spool Trace 證據' : '這份 Spool 結果已存在，沒有重複寫入'
    spoolPayload.value = ''
    spoolFile.value = ''
    await load()
  } catch (caught) {
    spoolError.value = toApiError(caught).detail
  } finally {
    spoolBusy.value = false
  }
}
function onSpoolFile(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  spoolFile.value = file.name
  const reader = new FileReader()
  reader.onload = () => { spoolPayload.value = String(reader.result ?? '') }
  reader.readAsText(file)
}
watch(id, load, { immediate: true })
</script>

<template>
  <div>
    <h1 class="ui-h1">證據與 Review</h1>
    <p class="ui-lead">資料是 Supervisor／Gleipnir 匯入的快照，加上本機記錄的 Spool Trace。每一筆都保留來源與記錄時間。</p>

    <AppLoading v-if="loading" />
    <div v-else-if="error" class="ui-callout is-fault" style="margin-top: 20px"><p>{{ error.detail }}</p><button type="button" class="ui-btn is-quiet" @click="load">重新載入</button></div>
    <template v-else>
      <section v-if="admin" class="ui-panel" style="margin-top: 24px" aria-labelledby="spool-h">
        <div class="ui-panel-h"><div><h2 id="spool-h" class="ui-h2">記錄 Trace 證據</h2><p class="ui-meta">貼上或上傳 Spool <code>spool-result/v1</code> JSON；會存成 Trace 證據，重複內容不會重複寫入。</p></div></div>
        <div class="ui-panel-b ui-stack">
          <label class="ui-stack" style="gap: 6px"><span class="ui-meta">JSON 檔案</span><input type="file" accept="application/json,.json" class="ui-input" @change="onSpoolFile"></label>
          <label class="ui-stack" style="gap: 6px"><span class="ui-meta">或貼上內容</span><textarea v-model="spoolPayload" class="ui-field" rows="6" placeholder='{ "schema": "spool-result/v1", "command": "check", ... }' /></label>
          <p v-if="spoolFile" class="ui-meta">已載入：{{ spoolFile }}</p>
          <p v-if="spoolError" class="ui-callout is-fault">{{ spoolError }}</p>
          <div style="display: flex; justify-content: flex-end"><button type="button" class="ui-btn is-primary" :disabled="spoolBusy || !spoolPayload.trim()" @click="importSpool">{{ spoolBusy ? '記錄中…' : '記錄這份證據' }}</button></div>
        </div>
      </section>

      <section class="ui-panel" style="margin-top: 24px" aria-labelledby="trace-h">
        <div class="ui-panel-h"><h2 id="trace-h" class="ui-h2">Spool Trace</h2><span class="ui-source">本機匯入紀錄</span></div>
        <AppEmpty v-if="!traceEvidence.length" title="還沒有 Spool Trace" description="從上方的 Spool JSON 記錄第一筆 trace 證據。" />
        <div v-else class="ui-table" role="region" aria-label="Spool Trace 證據" tabindex="0" style="overflow-x: auto">
          <table class="ui-table">
            <thead><tr><th>Command</th><th>結果</th><th>Counts</th><th>受影響 Trace</th></tr></thead>
            <tbody><tr v-for="{ item, trace } in traceEvidence" :key="item.id" :data-testid="`trace-evidence-${item.id}`"><td class="ui-wide"><strong>{{ trace!.command }}</strong><span class="ui-meta" style="display: block">{{ formatDateTime(item.recordedAt) }}</span></td><td><UiPill :tone="resultTone(item.result)" :label="resultText[item.result]" /></td><td class="ui-meta">{{ JSON.stringify(trace!.counts) }}</td><td class="ui-meta"><span v-if="!trace!.affectedTraces.length">—</span><span v-for="affected in trace!.affectedTraces" :key="affected.featureId" style="display: block">{{ affected.featureName }}（{{ affected.featureId }}）</span></td></tr></tbody>
          </table>
        </div>
      </section>

      <section style="margin-top: 24px" aria-labelledby="evidence-h">
        <div class="ui-panel-h" style="padding: 0 0 12px"><h2 id="evidence-h" class="ui-h2">證據 {{ filtered.length }} 筆</h2><span class="ui-source">匯入快照，不是即時狀態</span></div>
        <div style="display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 14px">
          <label class="ui-meta">結果 <select v-model="resultFilter" class="ui-input" style="min-height: 36px"><option value="all">全部</option><option value="Pass">通過</option><option value="Fail">失敗</option><option value="NotTested">未執行</option></select></label>
          <label class="ui-meta">類型 <select v-model="kindFilter" class="ui-input" style="min-height: 36px"><option value="all">全部</option><option v-for="(label, key) in evidenceKindLabels" :key="key" :value="key">{{ label }}</option></select></label>
        </div>
        <AppEmpty v-if="!filtered.length" title="這個篩選沒有證據" description="調整篩選，或從上方記錄一筆 Spool Trace。" />
        <div v-else class="ui-panel"><div class="ui-table" role="region" aria-label="證據列表" tabindex="0" style="overflow-x: auto"><table class="ui-table"><thead><tr><th>類型</th><th>標題</th><th>結果</th><th>工作項目</th><th>位置</th><th>記錄時間</th></tr></thead><tbody><tr v-for="item in filtered" :key="item.id"><td>{{ evidenceKindLabels[item.kind] }}</td><td class="ui-wide"><strong>{{ item.title }}</strong> <span v-if="isTestData(item.title)" class="ui-tag is-test">測試資料</span><span v-if="itemOf(item.workItemId)" class="ui-meta" style="display: block"><NuxtLink :to="`/work/${item.workItemId}`">查看工作 {{ itemOf(item.workItemId)?.code }}</NuxtLink> · <NuxtLink :to="`/projects/${id}/agents`">查看執行紀錄</NuxtLink></span></td><td><UiPill :tone="resultTone(item.result)" :label="resultText[item.result]" /></td><td class="ui-meta">{{ itemOf(item.workItemId)?.code ?? '—' }}</td><td class="ui-meta" style="overflow-wrap: anywhere">{{ item.path ?? item.url ?? '—' }}</td><td class="ui-meta" style="white-space: nowrap">{{ formatDateTime(item.recordedAt) }}</td></tr></tbody></table></div></div>
      </section>

      <section style="margin-top: 24px" aria-labelledby="reviews-h">
        <div class="ui-panel-h" style="padding: 0 0 12px"><h2 id="reviews-h" class="ui-h2">Review {{ reviews.length }} 筆</h2><span class="ui-source">匯入快照</span></div>
        <AppEmpty v-if="!reviews.length" title="還沒有 Review" description="匯入含 review verdict 的 Supervisor 資料後會顯示在這裡。" />
        <div v-else class="ui-panel"><div class="ui-table" role="region" aria-label="Review 列表" tabindex="0" style="overflow-x: auto"><table class="ui-table"><thead><tr><th>審查者</th><th>結論</th><th>工作項目</th><th>Findings</th><th>時間</th></tr></thead><tbody><tr v-for="review in reviews" :key="review.id"><td>{{ review.reviewer }}</td><td><UiPill :tone="review.verdict === 'Pass' ? 'proven' : 'fault'" :label="verdictLabels[review.verdict].label" /></td><td class="ui-meta">{{ itemOf(review.workItemId)?.code ?? '—' }}</td><td class="ui-meta" style="max-width: 32rem">{{ reviewText(review) }}</td><td class="ui-meta" style="white-space: nowrap">{{ formatDateTime(review.createdAt) }}</td></tr></tbody></table></div></div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.ui-panel-b label, .ui-panel-b .ui-stack { min-width: 0; }
textarea.ui-field, input.ui-input, select.ui-input { min-width: 0; }
</style>
