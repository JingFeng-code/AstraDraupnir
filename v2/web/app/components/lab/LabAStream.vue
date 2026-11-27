<script setup lang="ts">
import { clean, dayLabel, isTestData, projectOf, type Tone } from '~/utils/lab'

/** The ledger: what happened, grouped by day. Each entry carries the thread of the work it belongs to. */
const props = defineProps<{ projectId?: string, limit?: number }>()
const lab = useLab()
const filter = ref<'all' | 'moving' | 'proven' | 'problem'>('all')
const FILTERS = [['all', '全部'], ['moving', '進行中'], ['proven', '已完成'], ['problem', '有問題或在等你']] as const

const shown = computed(() => {
  const keep = (t: Tone) => filter.value === 'all' || (filter.value === 'problem' ? t === 'fault' || t === 'needs' : t === filter.value)
  return lab.events.value.filter(e => (!props.projectId || e.projectId === props.projectId) && keep(e.tone)).slice(0, props.limit ?? 60)
})
const groups = computed(() => {
  const out: { day: string, rows: typeof shown.value }[] = []
  for (const e of shown.value) {
    const day = dayLabel(e.at)
    const last = out[out.length - 1]
    if (last && last.day === day) last.rows.push(e)
    else out.push({ day, rows: [e] })
  }
  return out
})
const hm = (iso: string) => new Date(iso).toLocaleTimeString('zh-TW', { hour: '2-digit', minute: '2-digit', hour12: false })
const traceFor = (itemId: string | null) => {
  const item = itemId ? lab.byId.value.items.get(itemId) : null
  return item ? lab.traceOf(item) : null
}
</script>

<template>
  <section aria-label="事件日誌">
    <div class="a-filter" role="group" aria-label="篩選事件">
      <button v-for="[key, text] in FILTERS" :key="key" type="button" :aria-pressed="filter === key" @click="filter = key">{{ text }}</button>
    </div>
    <p v-if="!groups.length" class="a-empty">這個篩選條件下沒有事件。</p>
    <template v-for="g in groups" :key="g.day">
      <h3 class="a-day">{{ g.day }}</h3>
      <ol>
        <li v-for="e in g.rows" :key="e.id" class="a-ev">
          <time class="a-ev-time" :datetime="e.at">{{ hm(e.at) }}</time>
          <span class="a-ev-mark"><LabMark :tone="e.tone" :label="e.verb" /></span>
          <div>
            <p class="a-ev-t">
              {{ e.agent }} {{ e.verb }}
              <NuxtLink v-if="e.itemId" :to="`/lab/a/w/${e.itemId}`">{{ clean(e.title) }}</NuxtLink>
              <template v-else>{{ clean(e.title) }}</template>
              <span v-if="isTestData(e.title)" class="a-test">測試資料</span>
            </p>
            <p class="a-ev-m">{{ projectOf(lab.data.value.projects, e.projectId)?.name ?? '未歸屬專案' }}，任務 {{ e.taskId }}</p>
          </div>
          <span v-if="traceFor(e.itemId)" class="a-ev-trace"><LabTrace :trace="traceFor(e.itemId)!" compact /></span>
        </li>
      </ol>
    </template>
  </section>
</template>
