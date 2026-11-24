<script setup lang="ts">
import type { ReportSummary } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDateTime } from '~/utils/format'

const route = useRoute()
const api = useApi()
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
  try {
    reports.value = await api.get<ReportSummary[]>(`/api/projects/${id.value}/reports`)
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

async function generate() {
  if (generating.value) return
  generateError.value = null
  generating.value = true
  try {
    const report = await api.post<ReportSummary>(`/api/projects/${id.value}/reports`, {
      title: title.value.trim() || null,
    })
    title.value = ''
    await load()
    await view(report)
  } catch (caught) {
    generateError.value = toApiError(caught)
  } finally {
    generating.value = false
  }
}

async function view(report: ReportSummary) {
  selected.value = report
  markdown.value = ''
  markdownError.value = null
  markdownLoading.value = true
  try {
    markdown.value = await api.get<string>(`/api/reports/${report.id}/markdown`)
  } catch (caught) {
    markdownError.value = toApiError(caught).detail
  } finally {
    markdownLoading.value = false
  }
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="site-container py-6">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold text-ink">交付報告</h2>
        <p class="text-sm text-ink-muted">產生 Markdown 報告與 Stylus DocumentSpec YAML；有安裝 Stylus 時另產 DOCX。</p>
      </div>
    </div>

    <form class="panel mt-4 flex flex-wrap items-end gap-3 p-4" @submit.prevent="generate">
      <div class="min-w-[16rem] flex-1">
        <label for="report-title" class="field-label">報告標題（選填）</label>
        <input id="report-title" v-model="title" class="field-input" placeholder="預設：專案名稱 delivery report" data-testid="report-title">
      </div>
      <button type="submit" class="btn btn-primary" :disabled="generating" data-testid="report-generate">
        {{ generating ? '產生中…' : '產生報告' }}
      </button>
    </form>
    <AppNotice v-if="generateError" class="mt-3" :message="generateError.detail" />

    <AppLoading v-if="loading" />
    <AppNotice v-else-if="error" class="mt-4" :message="error.detail" />
    <div v-else class="mt-5 grid gap-5 lg:grid-cols-[20rem_minmax(0,1fr)]">
      <section aria-labelledby="report-list-title">
        <h3 id="report-list-title" class="text-base font-bold text-ink">報告列表</h3>
        <AppEmpty v-if="!reports.length" class="mt-3" title="還沒有報告" description="按「產生報告」建立第一份交付文件。" />
        <ul v-else class="mt-3 space-y-2">
          <li v-for="report in reports" :key="report.id">
            <button
              type="button"
              class="w-full rounded-lg border border-line bg-surface p-3 text-left hover:border-navy-600"
              :class="{ 'border-navy-700 ring-1 ring-navy-700': selected?.id === report.id }"
              :data-testid="`report-item-${report.id}`"
              @click="view(report)"
            >
              <span class="block text-sm font-semibold text-ink">{{ report.title }}</span>
              <span class="mt-0.5 block text-xs text-ink-subtle">{{ formatDateTime(report.generatedAt) }}</span>
            </button>
          </li>
        </ul>
      </section>

      <section class="panel p-4" aria-labelledby="report-view-title">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h3 id="report-view-title" class="text-base font-bold text-ink">報告內容</h3>
          <div v-if="selected" class="flex flex-wrap gap-2">
            <a class="btn btn-secondary" :href="`/api/reports/${selected.id}/markdown`" :download="`${selected.id}.md`">下載 Markdown</a>
            <a class="btn btn-secondary" :href="`/api/reports/${selected.id}/stylus.yaml`" :download="`${selected.id}.yaml`">下載 Stylus YAML</a>
          </div>
        </div>

        <AppEmpty v-if="!selected" class="mt-3" title="尚未選擇報告" description="從左側選擇一份報告，或先產生一份。" />
        <AppLoading v-else-if="markdownLoading" />
        <AppNotice v-else-if="markdownError" class="mt-3" :message="markdownError" />
        <pre
          v-else
          class="mt-3 max-h-[36rem] overflow-auto whitespace-pre-wrap break-words rounded-lg bg-surface-muted p-4 font-mono text-xs leading-relaxed text-ink"
          data-testid="report-markdown"
        >{{ markdown }}</pre>
      </section>
    </div>
  </div>
</template>
