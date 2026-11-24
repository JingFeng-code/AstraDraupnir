<script setup lang="ts">
import type { ImportResult } from '~/types/api'
import { ApiError, toApiError } from '~/utils/errors'

const route = useRoute()
const api = useApi()
const id = computed(() => String(route.params.id))

const payload = ref('')
const fileName = ref('')
const submitting = ref(false)
const error = ref<ApiError | null>(null)
const result = ref<ImportResult | null>(null)

function onFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  fileName.value = file.name
  const reader = new FileReader()
  reader.onload = () => {
    payload.value = String(reader.result ?? '')
  }
  reader.onerror = () => {
    error.value = new ApiError(0, 'File read failed', '無法讀取檔案，請確認格式。')
  }
  reader.readAsText(file)
}

async function submit() {
  if (submitting.value) return
  error.value = null
  result.value = null
  let parsed: unknown
  try {
    parsed = JSON.parse(payload.value)
  } catch {
    error.value = new ApiError(400, 'Invalid JSON', '貼上的內容不是合法 JSON，請確認是 Supervisor queue.json 或 tasks.json。')
    return
  }

  submitting.value = true
  try {
    result.value = await api.post<ImportResult>('/api/import/supervisor', parsed, { projectId: id.value })
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    submitting.value = false
  }
}

async function loadFixture() {
  try {
    const response = await fetch('/fixtures/queue.json')
    payload.value = await response.text()
    fileName.value = 'queue.json（範例）'
  } catch {
    error.value = new ApiError(0, 'Fixture unavailable', '無法載入範例檔。')
  }
}
</script>

<template>
  <div class="site-container py-6">
    <h2 class="text-xl font-bold text-ink">匯入 Supervisor</h2>
    <p class="text-sm text-ink-muted">
      貼上 <code class="font-mono text-xs">queue.json</code> 或上傳 <code class="font-mono text-xs">state/tasks.json</code>。匯入是冪等的：重複匯入會更新同一筆 AgentRun，不會覆寫已有人工決定。
    </p>

    <div class="panel mt-4 space-y-4 p-5">
      <div>
        <label for="import-file" class="field-label">上傳 JSON</label>
        <input id="import-file" type="file" accept="application/json,.json" class="block w-full text-sm" data-testid="import-file" @change="onFile">
        <p v-if="fileName" class="mt-1 text-xs text-ink-subtle">已載入：{{ fileName }}</p>
      </div>
      <div>
        <label for="import-payload" class="field-label">或貼上內容</label>
        <textarea
          id="import-payload"
          v-model="payload"
          rows="12"
          class="field-input font-mono text-xs"
          placeholder="{ &quot;tasks&quot;: [ ... ] }"
          data-testid="import-payload"
        />
      </div>

      <AppNotice v-if="error" :message="error.detail" :details="Object.values(error.fields).flat()" data-testid="import-error" />
      <AppNotice
        v-if="result"
        tone="success"
        :message="`匯入完成：AgentRun ${result.agentRuns}、證據 ${result.evidence}、Review ${result.reviews}、Gate ${result.gates}`"
        data-testid="import-result"
      />

      <div class="flex flex-wrap justify-end gap-2">
        <button type="button" class="btn btn-secondary" @click="loadFixture">載入範例</button>
        <button type="button" class="btn btn-primary" :disabled="submitting || !payload.trim()" data-testid="import-submit" @click="submit">
          {{ submitting ? '匯入中…' : '匯入' }}
        </button>
      </div>
      <p v-if="result" class="text-right text-sm">
        <NuxtLink :to="`/projects/${id}/agents`" class="text-navy-700 underline">查看 Agent 執行</NuxtLink>
        <span aria-hidden="true"> · </span>
        <NuxtLink :to="`/projects/${id}/gates`" class="text-navy-700 underline">查看 Human Gate</NuxtLink>
      </p>
    </div>
  </div>
</template>
