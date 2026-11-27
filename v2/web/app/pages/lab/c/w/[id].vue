<script setup lang="ts">
import { ago, clean, isTestData, RUN_LABEL, runTone, STATUS_LABEL, STATUS_TONE, STOP_STATE_LABEL, type StopState } from '~/utils/lab'

definePageMeta({ layout: 'lab-c' })
const lab = useLab()
const route = useRoute()
const item = computed(() => lab.byId.value.items.get(route.params.id as string))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const needs = computed(() => lab.dependenciesOf(item.value?.id ?? ''))
const waiting = computed(() => lab.dependentsOf(item.value?.id ?? ''))
const tone = (s: StopState) => (s === 'missing' || s === 'na' ? 'idle' : s)
const solid = (i: number) => {
  const a = trace.value!.stops[i]!.state
  const b = trace.value!.stops[i + 1]?.state
  return !!b && !['missing', 'na'].includes(a) && !['missing', 'na'].includes(b)
}
</script>

<template>
  <div v-if="item && trace" class="c-layout">
    <div>
      <p class="c-code"><NuxtLink :to="`/lab/c/p/${item.projectId}`" class="lab-link">回到 {{ lab.byId.value.projects.get(item.projectId)?.name }}</NuxtLink>，{{ item.code }}</p>
      <h1 class="c-h1" style="margin-top: 6px">{{ clean(item.title) }}<span v-if="isTestData(item.title)" class="c-test">測試資料</span></h1>
      <p class="c-sub" style="display: flex; gap: 8px; align-items: center"><LabMark :tone="STATUS_TONE[item.status]" />{{ STATUS_LABEL[item.status] }}<template v-if="item.assignee">，由 {{ item.assignee }} 負責</template></p>

      <ol class="c-big" aria-label="五站追溯">
        <li v-for="(s, i) in trace.stops" :key="s.name" class="c-node">
          <div class="c-node-rail"><LabMark :tone="tone(s.state)" :dashed="s.state === 'missing'" :size="20" /><i v-if="i < 4" :class="{ 'is-solid': solid(i) }" /></div>
          <h3>{{ s.name }}</h3>
          <p>{{ STOP_STATE_LABEL[s.state] }}，{{ s.note }}</p>
          <ul v-if="s.name === '執行' && trace.runs.length"><li v-for="r in trace.runs.slice(0, 3)" :key="r.id"><LabMark :tone="runTone(r.status)" :size="11" /> {{ r.agent }}，{{ RUN_LABEL[r.status] ?? r.status }}，{{ ago(r.finishedAt ?? r.startedAt) }}</li></ul>
          <ul v-if="s.name === '證據' && (trace.evidence.length || trace.reviews.length)">
            <li v-for="e in trace.evidence" :key="e.id">{{ clean(e.title) }}，{{ e.result === 'Pass' ? '通過' : e.result === 'Fail' ? '失敗' : '尚未測試' }}</li>
            <li v-for="rv in trace.reviews" :key="rv.id">{{ rv.reviewer }}：{{ rv.verdict === 'Pass' ? '通過' : '要求修改' }}</li>
          </ul>
        </li>
      </ol>

      <div class="c-around">
        <section aria-labelledby="cn-h"><h2 id="cn-h">先要完成</h2><p v-if="!needs.length" class="lab-muted">沒有先決條件</p><ul><li v-for="d in needs" :key="d.id"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/c/w/${d.id}`" class="lab-link">{{ clean(d.title) }}</NuxtLink></li></ul></section>
        <section aria-labelledby="cw-h"><h2 id="cw-h">正在等它</h2><p v-if="!waiting.length" class="lab-muted">沒有項目在等它</p><ul><li v-for="d in waiting" :key="d.id"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/c/w/${d.id}`" class="lab-link">{{ clean(d.title) }}</NuxtLink></li></ul></section>
      </div>
    </div>
    <LabCDrawer :item="item" />
  </div>
  <p v-else class="c-sub">{{ lab.data.value.ready ? '找不到這個工作項目。' : '讀取中…' }}</p>
</template>
