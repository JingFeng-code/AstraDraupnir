<script setup lang="ts">
import type { Evidence, Review } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDateTime } from '~/utils/format'
import { evidenceKindLabels, evidenceResultLabels, verdictLabels } from '~/utils/labels'

const route = useRoute()
const api = useApi()
const id = computed(() => String(route.params.id))

const evidence = ref<Evidence[]>([])
const reviews = ref<Review[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)

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

function findingsText(review: Review): string {
  if (review.findings == null) return '—'
  return typeof review.findings === 'string' ? review.findings : JSON.stringify(review.findings)
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="site-container py-6">
    <h2 class="text-xl font-bold text-ink">證據與 Review</h2>
    <p class="text-sm text-ink-muted">來自 Supervisor／Gleipnir 匯入的測試、建置與審查結果。</p>

    <AppLoading v-if="loading" />
    <AppNotice v-else-if="error" class="mt-4" :message="error.detail" />
    <div v-else class="space-y-8">
      <section aria-labelledby="evidence-title">
        <h3 id="evidence-title" class="mt-6 text-lg font-bold text-ink">證據</h3>
        <AppEmpty v-if="!evidence.length" class="mt-3" title="還沒有證據" description="匯入包含 evidence 的 Supervisor 資料後會顯示在這裡。" />
        <div v-else class="table-wrap panel mt-3">
          <table class="w-full text-left text-sm">
            <thead class="bg-surface-muted text-xs text-ink-subtle">
              <tr>
                <th class="px-4 py-2 font-medium">類型</th>
                <th class="px-4 py-2 font-medium">標題</th>
                <th class="px-4 py-2 font-medium">結果</th>
                <th class="px-4 py-2 font-medium">位置</th>
                <th class="px-4 py-2 font-medium">時間</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in evidence" :key="item.id" class="border-t border-line">
                <td class="px-4 py-2 text-xs">{{ evidenceKindLabels[item.kind] }}</td>
                <td class="px-4 py-2">{{ item.title }}</td>
                <td class="px-4 py-2"><StatusBadge :value="evidenceResultLabels[item.result]" /></td>
                <td class="max-w-xs break-all px-4 py-2 font-mono text-xs text-ink-muted">{{ item.path ?? item.url ?? '—' }}</td>
                <td class="px-4 py-2 text-xs text-ink-muted">{{ formatDateTime(item.recordedAt) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section aria-labelledby="reviews-title">
        <h3 id="reviews-title" class="text-lg font-bold text-ink">Review</h3>
        <AppEmpty v-if="!reviews.length" class="mt-3" title="還沒有 Review" description="匯入包含 review verdict 的資料後會顯示在這裡。" />
        <div v-else class="table-wrap panel mt-3">
          <table class="w-full text-left text-sm">
            <thead class="bg-surface-muted text-xs text-ink-subtle">
              <tr>
                <th class="px-4 py-2 font-medium">審查者</th>
                <th class="px-4 py-2 font-medium">結論</th>
                <th class="px-4 py-2 font-medium">Findings</th>
                <th class="px-4 py-2 font-medium">時間</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="review in reviews" :key="review.id" class="border-t border-line">
                <td class="px-4 py-2">{{ review.reviewer }}</td>
                <td class="px-4 py-2"><StatusBadge :value="verdictLabels[review.verdict]" /></td>
                <td class="max-w-lg break-words px-4 py-2 text-xs text-ink-muted">{{ findingsText(review) }}</td>
                <td class="px-4 py-2 text-xs text-ink-muted">{{ formatDateTime(review.createdAt) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  </div>
</template>
