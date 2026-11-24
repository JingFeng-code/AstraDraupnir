<script setup lang="ts">
import type { ImportResult } from '~/types/api'
import { ApiError, toApiError } from '~/utils/errors'

const route = useRoute()
const api = useApi()
const ws = useWorkspace()
const id = computed(() => String(route.params.id))
const payload = ref('')
const fileName = ref('')
const submitting = ref(false)
const error = ref<ApiError | null>(null)
const result = ref<ImportResult | null>(null)
const preview = ref<{ count: number, ids: string[], requiresHuman: number } | null>(null)
const previewError = ref<string | null>(null)
const lastImport = ref<string | null>(null)

function parsePreview(text: string) {
  preview.value = null
  previewError.value = null
  if (!text.trim()) return
  try {
    const raw = JSON.parse(text) as { tasks?: unknown }
    const tasks = Array.isArray(raw.tasks) ? raw.tasks : null
    if (!tasks) { previewError.value = '找不到 tasks 陣列；預期 Supervisor tasks.json 或 queue.json。'; return }
    const ids = tasks.map(t => typeof t === 'object' && t && 'id' in t ? String((t as { id: unknown }).id) : '').filter(Boolean)
    const requiresHuman = tasks.filter(t => typeof t === 'object' && t && ((t as { requiresHuman?: unknown }).requiresHuman === true || (t as { status?: unknown }).status === 'WAITING_HUMAN')).length
    preview.value = { count: tasks.length, ids: ids.slice(0, 8), requiresHuman }
  } catch {
    previewError.value = '內容不是合法 JSON，請確認是 Supervisor tasks.json 或 queue.json。'
  }
}
function onFile(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  fileName.value = file.name
  const reader = new FileReader()
  reader.onload = () => { payload.value = String(reader.result ?? ''); parsePreview(payload.value) }
  reader.onerror = () => { error.value = new ApiError(0, 'File read failed', '無法讀取檔案，請確認格式。') }
  reader.readAsText(file)
}
async function submit() {
  if (submitting.value) return
  error.value = null
  result.value = null
  let parsed: unknown
  try { parsed = JSON.parse(payload.value) } catch {
    error.value = new ApiError(400, 'Invalid JSON', '貼上的內容不是合法 JSON，請確認是 Supervisor queue.json 或 tasks.json。')
    return
  }
  submitting.value = true
  try {
    result.value = await api.post<ImportResult>('/api/import/supervisor', parsed, { projectId: id.value })
    lastImport.value = new Date().toLocaleString('zh-TW', { hour12: false })
    ws.toast.value = `Supervisor 快照已匯入：AgentRun ${result.value.agentRuns}、證據 ${result.value.evidence}、Review ${result.value.reviews}、Gate ${result.value.gates}`
    await ws.load(true)
  } catch (caught) { error.value = toApiError(caught) } finally { submitting.value = false }
}
async function loadFixture() {
  try {
    const response = await fetch('/fixtures/queue.json')
    payload.value = await response.text()
    fileName.value = 'queue.json（範例）'
    parsePreview(payload.value)
  } catch {
    error.value = new ApiError(0, 'Fixture unavailable', '無法載入範例檔。')
  }
}
</script>

<template>
  <div>
    <h1 class="ui-h1">匯入 Supervisor</h1>
    <p class="ui-lead">把 Supervisor 的 <code>tasks.json</code> 或 <code>queue.json</code> 匯入成 Draupnir 的歷史紀錄；這是單次快照，不會建立即時連線。</p>
    <div class="ui-callout is-need" style="margin-top: 20px; max-width: 900px">
      <p class="ui-h3">這是匯入快照，尚未對真實 Supervisor 即時同步驗證</p>
      <p class="ui-meta">匯入後看到的是當時的 AgentRun／證據／Review／Gate 歷史，不是「目前正在執行」。重複匯入是冪等的，會更新同一筆紀錄，不會覆寫已有人工決定。Draupnir 不會把 Gate 決定同步回 Supervisor。</p>
    </div>

    <div class="ui-panel" style="margin-top: 20px">
      <div class="ui-panel-b ui-stack">
        <label class="ui-stack" style="gap: 6px"><span class="ui-meta">上傳 JSON</span><input id="import-file" type="file" accept="application/json,.json" class="ui-input" data-testid="import-file" @change="onFile"><span v-if="fileName" class="ui-meta">已載入：{{ fileName }}</span></label>
        <label class="ui-stack" style="gap: 6px"><span class="ui-meta">或貼上內容</span><textarea id="import-payload" v-model="payload" rows="12" class="ui-field" style="font-family: var(--font-mono); font-size: 14px" placeholder='{ "tasks": [ ... ] }' data-testid="import-payload" @input="parsePreview(payload)" /></label>
        <p v-if="previewError" class="ui-callout is-fault">{{ previewError }}</p>
        <section v-else-if="preview" class="ui-panel" style="box-shadow: none; background: var(--c-sunken)" data-testid="import-preview" aria-label="匯入預覽">
          <div class="ui-panel-h"><h2 class="ui-h2">匯入預覽</h2><span class="ui-source">尚未寫入</span></div>
          <div class="ui-panel-b"><p><strong>{{ preview.count }}</strong> 個任務<template v-if="preview.requiresHuman">，其中 {{ preview.requiresHuman }} 個需要 Human Gate</template>。</p><p class="ui-meta" style="margin-top: 6px">任務：{{ preview.ids.join('、') }}<template v-if="preview.count > preview.ids.length">…</template></p></div>
        </section>
        <p v-if="error" class="ui-callout is-fault" data-testid="import-error">{{ error.detail }}<span v-if="Object.keys(error.fields).length" class="ui-meta" style="display: block">{{ Object.values(error.fields).flat().join('；') }}</span></p>
        <p v-if="result" class="ui-callout is-ok" data-testid="import-result">匯入完成：AgentRun {{ result.agentRuns }}、證據 {{ result.evidence }}、Review {{ result.reviews }}、Gate {{ result.gates }}<span v-if="lastImport" class="ui-meta" style="display: block">本次匯入時間（瀏覽器）：{{ lastImport }}</span></p>
        <div style="display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 8px"><button type="button" class="ui-btn is-quiet" @click="loadFixture">載入範例</button><button type="button" class="ui-btn is-primary" :disabled="submitting || !payload.trim()" data-testid="import-submit" @click="submit">{{ submitting ? '匯入中…' : '匯入這份快照' }}</button></div>
        <p v-if="result" class="ui-meta" style="text-align: right"><NuxtLink :to="`/projects/${id}/agents`">查看 Agent 執行</NuxtLink> · <NuxtLink :to="`/projects/${id}/gates`">查看 Human Gate</NuxtLink></p>
      </div>
    </div>
  </div>
</template>
