<script setup lang="ts">
import { ago, clean, isTestData, RUN_LABEL, runTone, STOP_STATE_LABEL, type StopState, type Trace } from '~/utils/lab'
import { agentName } from '~/utils/labD'

/** Chain of custody, top to bottom, with the records behind every station. */
const props = defineProps<{ trace: Trace }>()
const tone = (s: StopState) => (s === 'missing' || s === 'na' ? 'idle' : s)
const solid = (i: number) => {
  const a = props.trace.stops[i]!.state
  const b = props.trace.stops[i + 1]?.state
  return !!b && !['missing', 'na'].includes(a) && !['missing', 'na'].includes(b)
}
const said = (s: { state: StopState, note: string }) => { const l = STOP_STATE_LABEL[s.state]; return s.note.includes(l.slice(0, 2)) ? s.note : `${l}，${s.note}` }
const findings = (rv: { findings?: unknown }) => (Array.isArray(rv.findings) ? (rv.findings as { summary: string }[]) : [])
</script>

<template>
  <ol class="d-chain">
    <li v-for="(s, i) in trace.stops" :key="s.name" class="d-stop">
      <span class="d-stop-rail"><LabMark :tone="tone(s.state)" :dashed="s.state === 'missing'" :size="20" /><i v-if="i < trace.stops.length - 1" :class="{ 'is-solid': solid(i) }" /></span>
      <div class="d-stop-b">
        <p class="d-stop-t">{{ s.name }}<span>{{ said(s) }}</span></p>
        <ul v-if="s.name === '執行' && trace.runs.length" class="d-rec">
          <li v-for="r in trace.runs.slice(0, 4)" :key="r.id">
            <LabMark :tone="runTone(r.status)" :size="14" />
            <div>{{ agentName(r.agent) }}，第 {{ r.attempt }} 次，{{ RUN_LABEL[r.status] ?? r.status }}
              <span v-if="isTestData(trace.item.title)" class="d-tag is-test" style="margin-left: 6px">測試資料</span>
              <small>{{ ago(r.finishedAt ?? r.startedAt) }}，來源 {{ r.source }} 匯入<template v-if="r.model">，{{ r.model }}</template></small></div>
          </li>
        </ul>
        <ul v-if="s.name === '證據' && (trace.evidence.length || trace.reviews.length)" class="d-rec">
          <li v-for="e in trace.evidence" :key="e.id">
            <LabMark :tone="e.result === 'Pass' ? 'proven' : e.result === 'Fail' ? 'fault' : 'idle'" :size="14" />
            <div>{{ clean(e.title) }}<span v-if="isTestData(e.title)" class="d-tag is-test" style="margin-left: 6px">測試資料</span>
              <small>{{ e.result === 'Pass' ? '通過' : e.result === 'Fail' ? '失敗' : '尚未測試' }}，{{ e.kind }}，記錄於 {{ ago(e.recordedAt) }}</small></div>
          </li>
          <li v-for="rv in trace.reviews" :key="rv.id">
            <LabMark :tone="rv.verdict === 'Pass' ? 'proven' : 'fault'" :size="14" />
            <div>{{ agentName(rv.reviewer) }} 審查：{{ rv.verdict === 'Pass' ? '通過' : '要求修改' }}<small v-for="(f, k) in findings(rv)" :key="k">{{ f.summary }}</small></div>
          </li>
        </ul>
      </div>
    </li>
  </ol>
</template>
