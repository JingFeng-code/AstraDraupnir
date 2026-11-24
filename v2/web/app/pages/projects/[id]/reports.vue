<script setup lang="ts">
import type { ReportSummary } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDateTime } from '~/utils/format'

const route = useRoute()
const api = useApi()
const ws = useWorkspace()
const id = computed(() => String(route.params.id))
const reports = ref<ReportSummary[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const title = ref('')
const generating = ref(false)
const generateError = ref<ApiError | null>(null)
const selected = ref<ReportSummary | null>(null)
const markdown = ref('')
const markdownLoading = ref(false)
const markdownError = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try { reports.value = await api.get<ReportSummary[]>(`/api/projects/${id.value}/reports`) }
  catch (caught) { error.value = toApiError(caught) }
  finally { loading.value = false }
}
async function generate() {
  if (generating.value) return
  generateError.value = null
  generating.value = true
  try {
    const report = await api.post<ReportSummary>(`/api/projects/${id.value}/reports`, { title: title.value.trim() || null })
    title.value = ''
    ws.toast.value = '交付報告已產生'
    await load()
    await view(report)
  } catch (caught) { generateError.value = toApiError(caught) }
  finally { generating.value = false }
}
async function view(report: ReportSummary) {
  selected.value = report
  markdown.value = ''
  markdownError.value = null
  markdownLoading.value = true
  try { markdown.value = await api.get<string>(`/api/reports/${report.id}/markdown`) }
  catch (caught) { markdownError.value = toApiError(caught).detail }
  finally { markdownLoading.value = false }
}
watch(id, load, { immediate: true })
</script>

<template>
  <div>
    <h1 class="ui-h1">交付報告</h1>
    <p class="ui-lead">從專案目前的工作、Gate、證據與執行紀錄產生 Markdown 與 Stylus DocumentSpec YAML；本機可下載，不會自動發佈或同步。</p>

    <section class="ui-panel" style="margin-top: 24px" aria-labelledby="new-report-h">
      <div class="ui-panel-h"><div><h2 id="new-report-h" class="ui-h2">產生新報告</h2><p class="ui-meta">報告是當下資料的快照；產生後不會隨資料變動自動更新。</p></div></div>
      <form class="ui-panel-b" style="display: flex; flex-wrap: wrap; gap: 12px; align-items: end" @submit.prevent="generate">
        <label class="ui-stack" style="gap: 6px; flex: 1; min-width: 240px"><span class="ui-meta">報告標題（選填）</span><input id="report-title" v-model="title" class="ui-input" placeholder="預設：專案名稱 delivery report" data-testid="report-title"></label>
        <button type="submit" class="ui-btn is-primary" :disabled="generating" data-testid="report-generate">{{ generating ? '產生中…' : '產生報告' }}</button>
      </form>
      <p v-if="generateError" class="ui-callout is-fault" style="margin: 0 22px 18px">{{ generateError.detail }}</p>
    </section>

    <AppLoading v-if="loading" />
    <div v-else-if="error" class="ui-callout is-fault" style="margin-top: 20px"><p>{{ error.detail }}</p><button type="button" class="ui-btn is-quiet" @click="load">重新載入</button></div>
    <AppEmpty v-else-if="!reports.length" class="ui-panel" title="還沒有交付報告" description="按「產生報告」建立第一份快照。" style="margin-top: 24px" />
    <div v-else class="ui-split is-2r" style="margin-top: 24px">
      <section class="ui-panel" aria-labelledby="report-list-h">
        <div class="ui-panel-h"><h2 id="report-list-h" class="ui-h2">報告列表 {{ reports.length }}</h2><span class="ui-source">Draupnir 本機產生</span></div>
        <ul>
          <li v-for="report in reports" :key="report.id"><button type="button" class="ui-row" :class="{ 'is-on': selected?.id === report.id }" :data-testid="`report-item-${report.id}`" @click="view(report)"><UiMark :tone="'proven'" :size="18" /><div style="min-width: 0; flex: 1"><p class="ui-row-t">{{ report.title }}</p><p class="ui-row-m">產生時間 {{ formatDateTime(report.generatedAt) }} · 來源 Draupnir API（本機）</p><p v-if="report.docxPath" class="ui-meta">DOCX 已產生</p></div></button></li>
        </ul>
      </section>
      <section class="ui-panel ui-sticky" aria-labelledby="report-view-h">
        <div class="ui-panel-h"><h2 id="report-view-h" class="ui-h2">報告內容</h2><div v-if="selected" style="display: flex; gap: 8px; flex-wrap: wrap"><a class="ui-btn" :href="`/api/reports/${selected.id}/markdown`" :download="`${selected.id}.md`">下載 Markdown</a><a class="ui-btn" :href="`/api/reports/${selected.id}/stylus.yaml`" :download="`${selected.id}.yaml`">下載 Stylus YAML</a></div></div>
        <div class="ui-panel-b"><AppEmpty v-if="!selected" title="尚未選擇報告" description="從左側選擇一份，或先產生一份。" /><AppLoading v-else-if="markdownLoading" /><p v-else-if="markdownError" class="ui-callout is-fault">{{ markdownError }}</p><pre v-else class="ui-field" style="white-space: pre-wrap; overflow-wrap: anywhere; max-height: 40rem; overflow: auto" data-testid="report-markdown">{{ markdown }}</pre></div>
      </section>
    </div>
  </div>
</template>
