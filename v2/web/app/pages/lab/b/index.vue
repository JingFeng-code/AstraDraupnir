<script setup lang="ts">
import { clean, projectOf, refLabel, STATUS_LABEL, STATUS_TONE } from '~/utils/lab'

definePageMeta({ layout: 'lab-b' })
const lab = useLab()
const route = useRoute()
type Row = (typeof lab.attention.value)[number]
const rows = computed(() => lab.attention.value)
const GROUPS = [['gate', '等你決定'], ['blocked', '被擋住'], ['fault', 'Agent 有問題']] as const
const selId = ref<string>('')
watchEffect(() => { if (!selId.value || !rows.value.some(r => r.id === selId.value)) selId.value = (route.query.sel as string) || rows.value[0]?.id || '' })
const sel = computed<Row | undefined>(() => rows.value.find(r => r.id === selId.value))
const item = computed(() => (sel.value?.itemId ? lab.byId.value.items.get(sel.value.itemId) : undefined))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const gate = computed(() => (sel.value?.kind === 'gate' ? lab.data.value.gates.find(g => g.id === sel.value!.id) : undefined))
const unblocks = computed(() => (item.value ? [item.value, ...lab.dependentsOf(item.value.id)].filter(x => x.status !== 'Done') : []))
const choice = ref<'approve' | 'reject' | null>(null)
const note = ref('')
watch(selId, () => { choice.value = null; note.value = '' })
async function confirm() {
  if (!gate.value || !choice.value) return
  const at = rows.value.findIndex(r => r.id === selId.value)
  await lab.decide(gate.value, choice.value === 'approve', note.value)
  choice.value = null
  note.value = ''
  selId.value = rows.value[Math.min(at, rows.value.length - 1)]?.id ?? ''
}
function onKey(e: KeyboardEvent) {
  const t = e.target as HTMLElement
  if (['TEXTAREA', 'INPUT', 'SELECT'].includes(t.tagName) || e.ctrlKey || e.metaKey) return
  const i = rows.value.findIndex(r => r.id === selId.value)
  if (e.key === 'j') selId.value = rows.value[Math.min(i + 1, rows.value.length - 1)]?.id ?? selId.value
  else if (e.key === 'k') selId.value = rows.value[Math.max(i - 1, 0)]?.id ?? selId.value
  else if (gate.value && e.key === 'a') choice.value = 'approve'
  else if (gate.value && e.key === 'r') choice.value = 'reject'
  else if (e.key === 'Escape') choice.value = null
}
onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
const itemLink = computed(() => (item.value ? `/lab/b/w/${item.value.id}` : ''))
</script>

<template>
  <div class="b-desk">
    <section class="b-pane" aria-labelledby="q-h">
      <div class="b-pane-h"><h1 id="q-h">要處理的事</h1><span>{{ rows.length }} 項，按 j / k 切換</span></div>
      <p v-if="!rows.length" class="b-empty">目前沒有需要你處理的事。</p>
      <template v-for="[kind, label] in GROUPS" :key="kind">
        <template v-if="rows.some(r => r.kind === kind)">
          <h2 class="b-q-group">{{ label }}</h2>
          <ul>
            <li v-for="r in rows.filter(x => x.kind === kind)" :key="r.id">
              <button type="button" class="b-q" :class="{ 'is-on': r.id === selId }" :aria-current="r.id === selId ? 'true' : undefined" style="width: 100%; text-align: left" @click="selId = r.id">
                <LabMark :tone="kind === 'gate' ? 'needs' : 'fault'" :size="14" />
                <span><span class="b-q-t" style="display: block">{{ clean(r.title) }}</span><span class="b-q-m">{{ projectOf(lab.data.value.projects, r.projectId)?.name }}，{{ r.why }}</span></span>
              </button>
            </li>
          </ul>
        </template>
      </template>
    </section>

    <section class="b-pane" aria-labelledby="chain-h">
      <div class="b-pane-h"><h2 id="chain-h">證據鏈</h2><span>從需求一路到決定</span></div>
      <div v-if="sel" class="b-pad">
        <h3 class="b-h1">{{ clean(sel.title) }}</h3>
        <p class="b-meta">
          {{ projectOf(lab.data.value.projects, sel.projectId)?.name }}<template v-if="gate?.externalRef">，編號 {{ refLabel(gate.externalRef) }}</template>
          <template v-if="item">，<NuxtLink :to="itemLink" class="lab-link">開啟工作項目 {{ item.code }}</NuxtLink></template>
        </p>
        <LabBChain v-if="trace" :trace="trace" />
        <p v-else class="b-meta" style="margin-top: 16px">這個項目沒有對應的工作項目，沒有可追溯的鏈。</p>
      </div>
    </section>

    <aside class="b-pane" aria-labelledby="d-h">
      <div class="b-pane-h"><h2 id="d-h">決定</h2><span v-if="gate">只有你能決定</span></div>
      <template v-if="sel">
        <template v-if="gate">
          <div class="b-sec">
            <h3 class="b-side-h">核准後會發生什麼</h3>
            <ul v-if="unblocks.length" class="b-impact">
              <li v-for="u in unblocks.slice(0, 5)" :key="u.id"><LabMark :tone="STATUS_TONE[u.status]" :size="12" /><span>{{ clean(u.title) }}，現在{{ STATUS_LABEL[u.status] }}</span></li>
            </ul>
            <p v-else class="lab-muted">沒有被追蹤的工作在等這個決定。核准或駁回只會記錄你的決定，系統不會自動動作。</p>
          </div>
          <div class="b-sec">
            <div v-if="!choice" class="b-actions">
              <button type="button" class="lab-btn is-primary" @click="choice = 'approve'">核准 <span class="b-kbd">a</span></button>
              <button type="button" class="lab-btn" @click="choice = 'reject'">駁回 <span class="b-kbd">r</span></button>
              <NuxtLink :to="`/lab/b/gates/${gate.id}`" class="lab-btn" style="justify-content: center">逐步審閱後再決定</NuxtLink>
            </div>
            <template v-else>
              <h3 class="b-side-h">確認{{ choice === 'approve' ? '核准' : '駁回' }}</h3>
              <label class="lab-sr" for="b-note">備註（選填）</label>
              <textarea id="b-note" v-model="note" class="b-note" placeholder="給之後的自己留一句話（選填）" />
              <div class="b-actions">
                <button type="button" class="lab-btn" :class="choice === 'approve' ? 'is-primary' : 'is-danger'" :disabled="lab.busy.value" @click="confirm">確認{{ choice === 'approve' ? '核准' : '駁回' }}</button>
                <button type="button" class="lab-btn" @click="choice = null">取消 <span class="b-kbd">Esc</span></button>
              </div>
            </template>
          </div>
        </template>
        <div v-else class="b-sec">
          <h3 class="b-side-h">{{ sel.why }}</h3>
          <p class="lab-muted">這不是 Human Gate，沒有核准或駁回。到工作項目處理它。</p>
          <div v-if="item" class="b-actions"><NuxtLink :to="itemLink" class="lab-btn is-primary" style="justify-content: center">開啟工作項目</NuxtLink></div>
        </div>
      </template>
    </aside>
  </div>
</template>
