<script setup lang="ts">
import { clean, projectOf, refLabel, STATUS_LABEL, STATUS_TONE } from '~/lib/engineering'
import { gateEvidence } from '~/lib/evidenceRules'

/** Human Gate review: queue, evidence, decision. Approval is allowed only when required evidence stands; otherwise it is a recorded manual exception. */
const ws = useWorkspace()
const route = useRoute()
const tab = ref<'gates' | 'issues'>(route.query.tab === 'issues' ? 'issues' : 'gates')
const gates = computed(() => ws.pendingGates.value)
const issues = computed(() => ws.attention.value.filter(a => a.kind !== 'gate'))
const rows = computed(() => (tab.value === 'gates' ? gates.value.map(g => ({ id: g.id, title: g.title, projectId: g.projectId, itemId: g.workItemId ?? undefined, why: '' })) : issues.value))
const selId = ref('')
watchEffect(() => { if (!rows.value.some(r => r.id === selId.value)) selId.value = rows.value[0]?.id ?? '' })
const sel = computed(() => rows.value.find(r => r.id === selId.value))
const gate = computed(() => (tab.value === 'gates' ? gates.value.find(g => g.id === selId.value) : undefined))
const item = computed(() => (sel.value?.itemId ? ws.byId.value.items.get(sel.value.itemId) : undefined))
const trace = computed(() => (item.value ? ws.traceOf(item.value) : null))
const ev = computed(() => (gate.value ? gateEvidence(gate.value, trace.value) : null))
const unblocks = computed(() => (item.value ? [item.value, ...ws.dependentsOf(item.value.id)].filter(x => x.status !== 'Done') : []))

const mode = ref<'idle' | 'approve' | 'reject' | 'exception'>('idle')
const note = ref('')
const ack = ref(false)
const typed = ref('')
const approvalNeedsReason = computed(() => mode.value === 'approve' && !!gate.value && !gate.value.workItemId)
const approvalNoteReady = computed(() => !approvalNeedsReason.value || note.value.trim().length >= 20)
watch(selId, () => { mode.value = 'idle'; note.value = ''; ack.value = false; typed.value = '' })
const exceptionReady = computed(() => note.value.trim().length >= 20 && ack.value && typed.value === '例外')
async function submit() {
  if (!gate.value) return
  const m = mode.value
  // A no-work-item gate is human judgement, but the API requires a written reason.
  if (m === 'approve' && !gate.value.workItemId && !approvalNoteReady.value) return
  const prefix = m === 'exception' ? '【人工例外決定：缺少或未通過必要證據】' : ''
  const at = rows.value.findIndex(r => r.id === selId.value)
  await ws.decide(gate.value, m !== 'reject', `${prefix}${note.value}`.trim())
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
    <h1 class="ui-h1">審查與決策</h1>
    <p class="ui-lead">看證據，看後果，再決定。必要證據不齊全時，不能直接核准。</p>
    <div class="ui-tabs" role="tablist" aria-label="清單">
      <button type="button" role="tab" :aria-selected="tab === 'gates'" @click="tab = 'gates'">待決定的 Human Gate {{ gates.length }}</button>
      <button type="button" role="tab" :aria-selected="tab === 'issues'" @click="tab = 'issues'">需要處理 {{ issues.length }}</button>
    </div>

    <div class="ui-split is-3">
      <section class="ui-panel" aria-label="清單">
        <p class="ui-meta" style="padding: 14px 22px 4px">按 <span class="ui-kbd">j</span> <span class="ui-kbd">k</span> 切換</p>
        <p v-if="!rows.length" class="ui-empty">{{ tab === 'gates' ? '目前沒有待決定的 Human Gate。' : '目前沒有需要處理的事。' }}</p>
        <ul>
          <li v-for="r in rows" :key="r.id">
            <button type="button" class="ui-row" :class="{ 'is-on': r.id === selId }" :aria-current="r.id === selId ? 'true' : undefined" @click="selId = r.id">
              <UiMark :tone="tab === 'gates' ? 'needs' : 'fault'" :size="18" />
              <div><p class="ui-row-t">{{ clean(r.title) }}</p><p class="ui-row-m">{{ projectOf(ws.data.value.projects, r.projectId)?.name }}<template v-if="r.why">，{{ r.why }}</template></p></div>
            </button>
          </li>
        </ul>
      </section>

      <section class="ui-panel" aria-labelledby="ev-h">
        <div v-if="sel" class="ui-panel-b" style="padding-top: 22px">
          <p class="ui-meta">{{ projectOf(ws.data.value.projects, sel.projectId)?.name }}<template v-if="gate?.externalRef">，編號 {{ refLabel(gate.externalRef) }}</template></p>
          <h2 id="ev-h" class="ui-h2" style="margin-top: 4px">{{ clean(sel.title) }}</h2>
          <p v-if="item" class="ui-meta" style="margin-top: 6px">對應工作項目 <NuxtLink :to="`/work/${item.id}`" style="text-decoration: underline; text-underline-offset: 3px">{{ item.code }}</NuxtLink></p>

          <div v-if="ev" class="ui-callout" :class="ev.state === 'satisfied' ? 'is-ok' : ev.state === 'manual' ? '' : 'is-fault'" style="margin-top: 18px">
            <p class="ui-h3" style="display: flex; gap: 8px; align-items: center"><UiMark :tone="ev.state === 'satisfied' ? 'proven' : ev.state === 'manual' ? 'idle' : 'fault'" :size="16" />{{ ev.headline }}</p>
            <ul class="ui-check"><li v-for="c in ev.checks" :key="c.label"><UiMark :tone="c.ok ? 'proven' : 'fault'" :size="14" /><span>{{ c.label }}，{{ c.detail }}</span></li></ul>
            <h4 class="ui-h3" style="margin-top: 6px">已有的證據</h4>
            <p v-if="!trace || !(trace.evidence.length || trace.reviews.length)" class="ui-meta">沒有任何證據或審查紀錄。</p>
            <ul v-else class="ui-meta"><li v-for="e in trace.evidence" :key="e.id">{{ clean(e.title) }}，{{ e.result === 'Pass' ? '通過' : e.result === 'Fail' ? '失敗' : '尚未測試' }}</li><li v-for="rv in trace.reviews" :key="rv.id">{{ rv.reviewer }} 審查：{{ rv.verdict === 'Pass' ? '通過' : '要求修改' }}</li></ul>
            <p class="ui-meta">判斷規則由 Draupnir 推導，不是 Supervisor 的定義。</p>
          </div>

          <h3 class="ui-h3" style="margin: 26px 0 14px">追溯鏈</h3>
          <UiChain v-if="trace" :trace="trace" />
          <p v-else class="ui-meta">沒有對應的工作項目，沒有可追溯的鏈。</p>
        </div>
      </section>

      <aside class="ui-panel ui-sticky" aria-labelledby="dc-h">
        <div class="ui-panel-h"><h2 id="dc-h" class="ui-h2">決定</h2></div>
        <div v-if="sel" class="ui-panel-b ui-stack">
          <template v-if="gate && ev">
            <div>
              <h3 class="ui-h3" style="margin-bottom: 6px">放行後會發生什麼</h3>
              <ul v-if="unblocks.length"><li v-for="u in unblocks.slice(0, 5)" :key="u.id" style="display: flex; gap: 10px; padding: 4px 0"><UiMark :tone="STATUS_TONE[u.status]" :size="14" /><span>{{ clean(u.title) }}，現在{{ STATUS_LABEL[u.status] }}</span></li></ul>
              <p v-else class="ui-meta">沒有被追蹤的工作在等這個決定。核准或駁回只會記錄在 Draupnir，系統不會自動動作。</p>
            </div>

            <div v-if="mode === 'idle'" class="ui-stack" style="gap: 10px">
              <button v-if="ev.canApprove" type="button" class="ui-btn is-primary" @click="mode = 'approve'">核准</button>
              <p v-else class="ui-callout is-need">不能直接核准：{{ ev.headline }}。先補齊證據，或駁回。</p>
              <button type="button" class="ui-btn" @click="mode = 'reject'">駁回</button>
              <button v-if="ev.needsException" type="button" class="ui-btn is-quiet" style="justify-content: flex-start; text-decoration: underline; text-underline-offset: 3px" @click="mode = 'exception'">改以人工例外決定…</button>
            </div>

            <div v-else-if="mode === 'approve' || mode === 'reject'" class="ui-stack" style="gap: 10px">
              <h3 class="ui-h3">確認{{ mode === 'approve' ? '核准' : '駁回' }}</h3>
              <label class="lab-sr" for="ui-note">{{ approvalNeedsReason ? '核准理由（至少 20 字，必填）' : '備註（選填）' }}</label>
              <textarea id="ui-note" v-model="note" class="ui-field" :placeholder="approvalNeedsReason ? '沒有工作項目的人工判斷理由，至少 20 字' : '給之後的自己留一句話（選填）'" />
              <p v-if="approvalNeedsReason && !approvalNoteReady" class="ui-meta" data-testid="review-reason-required">沒有對應工作項目的 Gate 必須寫明至少 20 字核准理由。</p>
              <div style="display: flex; gap: 8px"><button type="button" class="ui-btn" :class="mode === 'approve' ? 'is-primary' : 'is-danger'" :disabled="ws.busy.value || (mode === 'approve' && !approvalNoteReady)" @click="submit">確認{{ mode === 'approve' ? '核准' : '駁回' }}</button><button type="button" class="ui-btn is-quiet" @click="mode = 'idle'">取消</button></div>
            </div>

            <div v-else class="ui-stack" style="gap: 10px">
              <h3 class="ui-h3">人工例外決定</h3>
              <p class="ui-callout is-fault">必要證據沒有齊全。這不是驗證通過，而是你個人承擔風險的例外，會被記錄成例外。</p>
              <label class="ui-h3" for="ui-why">例外原因（至少 20 字）</label>
              <textarea id="ui-why" v-model="note" class="ui-field" placeholder="為什麼可以在沒有證據下放行？" />
              <label style="display: flex; gap: 10px; align-items: flex-start"><input v-model="ack" type="checkbox" style="margin-top: 5px">我知道必要證據缺少或未通過</label>
              <label class="ui-meta" for="ui-type">請輸入「例外」兩個字確認</label>
              <input id="ui-type" v-model="typed" class="ui-input" autocomplete="off">
              <div style="display: flex; gap: 8px"><button type="button" class="ui-btn is-danger" :disabled="!exceptionReady || ws.busy.value" @click="submit">以例外放行</button><button type="button" class="ui-btn is-quiet" @click="mode = 'idle'">取消</button></div>
            </div>
            <p class="ui-meta">此決定只記錄在 Draupnir，沒有同步到 Supervisor（同步整合尚未驗證）。</p>
          </template>
          <template v-else>
            <p class="ui-callout is-need">{{ sel.why }}</p>
            <p class="ui-meta">這不是 Human Gate，沒有核准或駁回。到工作項目處理它。</p>
            <NuxtLink v-if="item" :to="`/work/${item.id}`" class="ui-btn is-primary">開啟工作項目</NuxtLink>
          </template>
        </div>
      </aside>
    </div>
  </div>
</template>
