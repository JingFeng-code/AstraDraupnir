<script setup lang="ts">
import { clean, ago, RUN_LABEL, runTone, SEVERITY_LABEL, STATUS_LABEL, STATUS_ORDER, STATUS_TONE, TYPE_LABEL, isTestData } from '~/utils/lab'
import type { WorkStatus } from '~/types/api'

definePageMeta({ layout: 'lab-a' })
const lab = useLab()
const route = useRoute()
const item = computed(() => lab.byId.value.items.get(route.params.id as string))
const project = computed(() => (item.value ? lab.byId.value.projects.get(item.value.projectId) : null))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const moves = computed(() => (item.value ? lab.allowedMoves(item.value) : []))
const needs = computed(() => lab.dependenciesOf(item.value?.id ?? ''))
const waiting = computed(() => lab.dependentsOf(item.value?.id ?? ''))
const reason = ref('')
const asking = ref(false)
const undo = ref<{ status: WorkStatus } | null>(null)
const moveOf = (to: WorkStatus) => moves.value.find(m => m.to === to)
const pastIndex = computed(() => STATUS_ORDER.indexOf(item.value?.status as WorkStatus))

async function move(to: WorkStatus) {
  if (!item.value) return
  if (to === 'Blocked' && !asking.value) { asking.value = true; return }
  const from = item.value.status
  await lab.setStatus(item.value, to, to === 'Blocked' ? reason.value : undefined)
  if (item.value?.status === to) undo.value = { status: from }
  asking.value = false
  reason.value = ''
}
async function revert() {
  if (item.value && undo.value) { await lab.setStatus(item.value, undo.value.status); undo.value = null }
}
</script>

<template>
  <article v-if="item && trace" class="a-item">
    <NuxtLink :to="`/lab/a/p/${item.projectId}`" class="a-back">回到 {{ project?.name }}</NuxtLink>
    <header class="a-head" style="margin-top: 14px">
      <p class="a-sub" style="margin: 0 0 4px">{{ item.code }}，{{ TYPE_LABEL[item.type] }}<template v-if="item.severity">，{{ SEVERITY_LABEL[item.severity] }}</template><span v-if="isTestData(item.title)" class="a-test">測試資料</span></p>
      <h1 class="a-h1">{{ clean(item.title) }}</h1>
      <p class="a-sub" style="display: flex; gap: 8px; align-items: center"><LabMark :tone="STATUS_TONE[item.status]" />{{ STATUS_LABEL[item.status] }}<template v-if="item.assignee">，由 {{ item.assignee }} 負責</template></p>
      <p v-if="item.blockedReason" class="a-sub" style="color: var(--fault)">被擋住的原因：{{ item.blockedReason }}</p>
    </header>

    <section aria-labelledby="path-h">
      <h2 id="path-h" class="a-h2">狀態</h2>
      <ol class="a-path" aria-label="狀態路徑">
        <li v-for="(st, i) in STATUS_ORDER" :key="st">
          <button type="button" class="a-step" :class="{ 'is-on': item.status === st, 'is-past': pastIndex > i }" :aria-current="item.status === st ? 'step' : undefined"
            :disabled="item.status === st || lab.busy.value || !moveOf(st)?.ok" :aria-describedby="moveOf(st)?.why ? `why-${st}` : undefined" @click="move(st)">
            <LabMark v-if="pastIndex > i" tone="proven" :size="11" /><LabMark v-else-if="item.status === st" :tone="STATUS_TONE[st]" :size="11" />{{ STATUS_LABEL[st] }}
          </button>
        </li>
        <li style="margin-left: 8px"><button type="button" class="lab-btn is-danger" :disabled="item.status === 'Blocked' || lab.busy.value" @click="move('Blocked')">標記為被擋住</button></li>
      </ol>
      <ul class="a-why-list">
        <li v-for="m in moves.filter(x => !x.ok)" :key="m.to" :id="`why-${m.to}`" class="a-why">不能移到「{{ STATUS_LABEL[m.to] }}」：{{ m.why }}</li>
      </ul>
      <div v-if="asking" class="a-reason">
        <label class="lab-sr" for="why-input">被擋住的原因</label>
        <input id="why-input" v-model="reason" class="lab-field" style="min-width: 280px" placeholder="例如：等你選定設計方向" @keydown.enter="reason.trim() && move('Blocked')">
        <button type="button" class="lab-btn is-danger" :disabled="!reason.trim()" @click="move('Blocked')">確認標記為被擋住</button>
        <button type="button" class="lab-btn" @click="asking = false">取消</button>
      </div>
      <p v-if="undo" class="a-why" style="margin-top: 10px">已更新。<button type="button" class="a-link" @click="revert">復原成「{{ STATUS_LABEL[undo.status] }}」</button></p>
    </section>

    <section class="a-block" aria-labelledby="trace-h">
      <h2 id="trace-h" class="a-h2">追溯線：這項工作能信到哪裡</h2>
      <LabTrace :trace="trace" />
    </section>

    <section class="a-block" aria-labelledby="dep-h">
      <h2 id="dep-h" class="a-h2">相依</h2>
      <dl class="a-kv">
        <dt>要先完成</dt>
        <dd>
          <span v-if="!needs.length" class="lab-muted">沒有先決條件</span>
          <ul><li v-for="d in needs" :key="d.id" style="display: flex; gap: 8px; align-items: center"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/a/w/${d.id}`" class="a-link" style="font-weight: 500">{{ d.title }}</NuxtLink></li></ul>
        </dd>
        <dt>正在等它</dt>
        <dd>
          <span v-if="!waiting.length" class="lab-muted">沒有項目在等它</span>
          <ul><li v-for="d in waiting" :key="d.id" style="display: flex; gap: 8px; align-items: center"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/a/w/${d.id}`" class="a-link" style="font-weight: 500">{{ d.title }}</NuxtLink></li></ul>
        </dd>
      </dl>
    </section>

    <section class="a-block" aria-labelledby="log-h">
      <h2 id="log-h" class="a-h2">執行、審查與證據</h2>
      <p v-if="!trace.runs.length && !trace.evidence.length && !trace.reviews.length" class="a-empty">還沒有任何 Agent 執行或證據。派工後，紀錄會出現在這裡。</p>
      <ul class="a-log">
        <li v-for="r in trace.runs" :key="r.id"><LabMark :tone="runTone(r.status)" /><div><p class="a-ev-t">{{ r.agent }}，第 {{ r.attempt }} 次，{{ RUN_LABEL[r.status] ?? r.status }}</p><p class="a-ev-m">{{ ago(r.finishedAt ?? r.startedAt) }}<template v-if="r.model">，模型 {{ r.model }}</template></p></div></li>
        <li v-for="e in trace.evidence" :key="e.id"><LabMark :tone="e.result === 'Pass' ? 'proven' : e.result === 'Fail' ? 'fault' : 'idle'" /><div><p class="a-ev-t">{{ e.title }}</p><p class="a-ev-m">{{ e.result === 'Pass' ? '通過' : e.result === 'Fail' ? '失敗' : '尚未測試' }}，{{ e.kind }}</p></div></li>
        <li v-for="rv in trace.reviews" :key="rv.id"><LabMark :tone="rv.verdict === 'Pass' ? 'proven' : 'fault'" /><div><p class="a-ev-t">{{ rv.reviewer }} 的審查：{{ rv.verdict === 'Pass' ? '通過' : '要求修改' }}</p>
          <ul v-if="Array.isArray(rv.findings) && rv.findings.length" class="a-ev-m"><li v-for="(f, i) in (rv.findings as {summary: string}[])" :key="i">{{ f.summary }}</li></ul></div></li>
      </ul>
    </section>
  </article>
  <p v-else class="a-empty">{{ lab.data.value.ready ? '找不到這個工作項目。' : '讀取中…' }}</p>
</template>
