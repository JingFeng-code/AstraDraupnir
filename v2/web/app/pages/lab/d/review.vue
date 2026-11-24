<script setup lang="ts">
import { clean, projectOf, refLabel, STATUS_LABEL, STATUS_TONE } from '~/utils/lab'
import { gateEvidence } from '~/utils/labD'

/** Human Gate review: queue, evidence, decision. Approval is allowed only when required evidence stands; otherwise it is a recorded manual exception. */
definePageMeta({ layout: 'lab-d' })
const lab = useLab()
const route = useRoute()
const tab = ref<'gates' | 'issues'>(route.query.tab === 'issues' ? 'issues' : 'gates')
const gates = computed(() => lab.pendingGates.value)
const issues = computed(() => lab.attention.value.filter(a => a.kind !== 'gate'))
const rows = computed(() => (tab.value === 'gates' ? gates.value.map(g => ({ id: g.id, title: g.title, projectId: g.projectId, itemId: g.workItemId ?? undefined, why: '' })) : issues.value))
const selId = ref('')
watchEffect(() => { if (!rows.value.some(r => r.id === selId.value)) selId.value = rows.value[0]?.id ?? '' })
const sel = computed(() => rows.value.find(r => r.id === selId.value))
const gate = computed(() => (tab.value === 'gates' ? gates.value.find(g => g.id === selId.value) : undefined))
const item = computed(() => (sel.value?.itemId ? lab.byId.value.items.get(sel.value.itemId) : undefined))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const ev = computed(() => (gate.value ? gateEvidence(gate.value, trace.value) : null))
const unblocks = computed(() => (item.value ? [item.value, ...lab.dependentsOf(item.value.id)].filter(x => x.status !== 'Done') : []))

const mode = ref<'idle' | 'approve' | 'reject' | 'exception'>('idle')
const note = ref('')
const ack = ref(false)
const typed = ref('')
watch(selId, () => { mode.value = 'idle'; note.value = ''; ack.value = false; typed.value = '' })
const exceptionReady = computed(() => note.value.trim().length >= 20 && ack.value && typed.value === '例外')
async function submit() {
  if (!gate.value) return
  const m = mode.value
  const prefix = m === 'exception' ? '【人工例外決定：缺少或未通過必要證據】' : ''
  const at = rows.value.findIndex(r => r.id === selId.value)
  await lab.decide(gate.value, m !== 'reject', `${prefix}${note.value}`.trim())
  mode.value = 'idle'; note.value = ''; ack.value = false; typed.value = ''
  selId.value = rows.value[Math.min(at, rows.value.length - 1)]?.id ?? ''
}
function onKey(e: KeyboardEvent) {
  const t = e.target as HTMLElement
  if (['TEXTAREA', 'INPUT', 'SELECT'].includes(t.tagName) || e.ctrlKey || e.metaKey) return
  const i = rows.value.findIndex(r => r.id === selId.value)
  if (e.key === 'j') selId.value = rows.value[Math.min(i + 1, rows.value.length - 1)]?.id ?? selId.value
  else if (e.key === 'k') selId.value = rows.value[Math.max(i - 1, 0)]?.id ?? selId.value
  else if (e.key === 'Escape') mode.value = 'idle'
}
onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <div>
    <h1 class="d-h1">審查與決策</h1>
    <p class="d-lead">看證據，看後果，再決定。必要證據不齊全時，不能直接核准。</p>
    <div class="d-tabs" role="tablist" aria-label="清單">
      <button type="button" role="tab" :aria-selected="tab === 'gates'" @click="tab = 'gates'">待決定的 Human Gate {{ gates.length }}</button>
      <button type="button" role="tab" :aria-selected="tab === 'issues'" @click="tab = 'issues'">需要處理 {{ issues.length }}</button>
    </div>

    <div class="d-split is-3">
      <section class="d-panel" aria-label="清單">
        <p class="d-meta" style="padding: 14px 22px 4px">按 <span class="d-kbd">j</span> <span class="d-kbd">k</span> 切換</p>
        <p v-if="!rows.length" class="d-empty">{{ tab === 'gates' ? '目前沒有待決定的 Human Gate。' : '目前沒有需要處理的事。' }}</p>
        <ul>
          <li v-for="r in rows" :key="r.id">
            <button type="button" class="d-row" :class="{ 'is-on': r.id === selId }" :aria-current="r.id === selId ? 'true' : undefined" @click="selId = r.id">
              <LabMark :tone="tab === 'gates' ? 'needs' : 'fault'" :size="18" />
              <div><p class="d-row-t">{{ clean(r.title) }}</p><p class="d-row-m">{{ projectOf(lab.data.value.projects, r.projectId)?.name }}<template v-if="r.why">，{{ r.why }}</template></p></div>
            </button>
          </li>
        </ul>
      </section>

      <section class="d-panel" aria-labelledby="ev-h">
        <div v-if="sel" class="d-panel-b" style="padding-top: 22px">
          <p class="d-meta">{{ projectOf(lab.data.value.projects, sel.projectId)?.name }}<template v-if="gate?.externalRef">，編號 {{ refLabel(gate.externalRef) }}</template></p>
          <h2 id="ev-h" class="d-h2" style="margin-top: 4px">{{ clean(sel.title) }}</h2>
          <p v-if="item" class="d-meta" style="margin-top: 6px">對應工作項目 <NuxtLink :to="`/lab/d/w/${item.id}`" style="text-decoration: underline; text-underline-offset: 3px">{{ item.code }}</NuxtLink></p>

          <div v-if="ev" class="d-callout" :class="ev.state === 'satisfied' ? 'is-ok' : ev.state === 'manual' ? '' : 'is-fault'" style="margin-top: 18px">
            <p class="d-h3" style="display: flex; gap: 8px; align-items: center"><LabMark :tone="ev.state === 'satisfied' ? 'proven' : ev.state === 'manual' ? 'idle' : 'fault'" :size="16" />{{ ev.headline }}</p>
            <ul class="d-check"><li v-for="c in ev.checks" :key="c.label"><LabMark :tone="c.ok ? 'proven' : 'fault'" :size="14" /><span>{{ c.label }}，{{ c.detail }}</span></li></ul>
            <h4 class="d-h3" style="margin-top: 6px">已有的證據</h4>
            <p v-if="!trace || !(trace.evidence.length || trace.reviews.length)" class="d-meta">沒有任何證據或審查紀錄。</p>
            <ul v-else class="d-meta"><li v-for="e in trace.evidence" :key="e.id">{{ clean(e.title) }}，{{ e.result === 'Pass' ? '通過' : e.result === 'Fail' ? '失敗' : '尚未測試' }}</li><li v-for="rv in trace.reviews" :key="rv.id">{{ rv.reviewer }} 審查：{{ rv.verdict === 'Pass' ? '通過' : '要求修改' }}</li></ul>
            <p class="d-meta">判斷規則由設計環境推導，不是 Supervisor 的定義。</p>
          </div>

          <h3 class="d-h3" style="margin: 26px 0 14px">追溯鏈</h3>
          <LabDChain v-if="trace" :trace="trace" />
          <p v-else class="d-meta">沒有對應的工作項目，沒有可追溯的鏈。</p>
        </div>
      </section>

      <aside class="d-panel d-sticky" aria-labelledby="dc-h">
        <div class="d-panel-h"><h2 id="dc-h" class="d-h2">決定</h2></div>
        <div v-if="sel" class="d-panel-b d-stack">
          <template v-if="gate && ev">
            <div>
              <h3 class="d-h3" style="margin-bottom: 6px">放行後會發生什麼</h3>
              <ul v-if="unblocks.length"><li v-for="u in unblocks.slice(0, 5)" :key="u.id" style="display: flex; gap: 10px; padding: 4px 0"><LabMark :tone="STATUS_TONE[u.status]" :size="14" /><span>{{ clean(u.title) }}，現在{{ STATUS_LABEL[u.status] }}</span></li></ul>
              <p v-else class="d-meta">沒有被追蹤的工作在等這個決定。核准或駁回只會記錄在設計資料庫，系統不會自動動作。</p>
            </div>

            <div v-if="mode === 'idle'" class="d-stack" style="gap: 10px">
              <button v-if="ev.canApprove" type="button" class="d-btn is-primary" @click="mode = 'approve'">核准</button>
              <p v-else class="d-callout is-need">不能直接核准：{{ ev.headline }}。先補齊證據，或駁回。</p>
              <button type="button" class="d-btn" @click="mode = 'reject'">駁回</button>
              <button v-if="ev.needsException" type="button" class="d-btn is-quiet" style="justify-content: flex-start; text-decoration: underline; text-underline-offset: 3px" @click="mode = 'exception'">改以人工例外決定…</button>
            </div>

            <div v-else-if="mode === 'approve' || mode === 'reject'" class="d-stack" style="gap: 10px">
              <h3 class="d-h3">確認{{ mode === 'approve' ? '核准' : '駁回' }}</h3>
              <label class="lab-sr" for="d-note">備註（選填）</label>
              <textarea id="d-note" v-model="note" class="d-field" placeholder="給之後的自己留一句話（選填）" />
              <div style="display: flex; gap: 8px"><button type="button" class="d-btn" :class="mode === 'approve' ? 'is-primary' : 'is-danger'" :disabled="lab.busy.value" @click="submit">確認{{ mode === 'approve' ? '核准' : '駁回' }}</button><button type="button" class="d-btn is-quiet" @click="mode = 'idle'">取消</button></div>
            </div>

            <div v-else class="d-stack" style="gap: 10px">
              <h3 class="d-h3">人工例外決定</h3>
              <p class="d-callout is-fault">必要證據沒有齊全。這不是驗證通過，而是你個人承擔風險的例外，會被記錄成例外。</p>
              <label class="d-h3" for="d-why">例外原因（至少 20 字）</label>
              <textarea id="d-why" v-model="note" class="d-field" placeholder="為什麼可以在沒有證據下放行？" />
              <label style="display: flex; gap: 10px; align-items: flex-start"><input v-model="ack" type="checkbox" style="margin-top: 5px">我知道必要證據缺少或未通過</label>
              <label class="d-meta" for="d-type">請輸入「例外」兩個字確認</label>
              <input id="d-type" v-model="typed" class="d-input" autocomplete="off">
              <div style="display: flex; gap: 8px"><button type="button" class="d-btn is-danger" :disabled="!exceptionReady || lab.busy.value" @click="submit">以例外放行</button><button type="button" class="d-btn is-quiet" @click="mode = 'idle'">取消</button></div>
            </div>
            <p class="d-meta">此決定寫入設計環境，不會送到 Supervisor。</p>
          </template>
          <template v-else>
            <p class="d-callout is-need">{{ sel.why }}</p>
            <p class="d-meta">這不是 Human Gate，沒有核准或駁回。到工作項目處理它。</p>
            <NuxtLink v-if="item" :to="`/lab/d/w/${item.id}`" class="d-btn is-primary">開啟工作項目</NuxtLink>
          </template>
        </div>
      </aside>
    </div>
  </div>
</template>
