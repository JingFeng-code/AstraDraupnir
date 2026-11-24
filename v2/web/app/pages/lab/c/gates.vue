<script setup lang="ts">
import { clean, projectOf, refLabel, STATUS_LABEL, STATUS_TONE } from '~/utils/lab'
import type { HumanGate } from '~/types/api'

definePageMeta({ layout: 'lab-c' })
const lab = useLab()
const pending = computed(() => lab.pendingGates.value)
const decided = computed(() => lab.data.value.gates.filter(g => g.status !== 'Pending'))
const open = ref<HumanGate | null>(null)
const dlg = ref<HTMLDialogElement | null>(null)
const choice = ref<'approve' | 'reject'>('approve')
const note = ref('')
const item = computed(() => (open.value?.workItemId ? lab.byId.value.items.get(open.value.workItemId) : undefined))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const unblocks = computed(() => (item.value ? [item.value, ...lab.dependentsOf(item.value.id)].filter(x => x.status !== 'Done') : []))
function show(g: HumanGate) { open.value = g; choice.value = 'approve'; note.value = ''; nextTick(() => dlg.value?.showModal()) }
function close() { dlg.value?.close(); open.value = null }
async function submit() {
  if (!open.value) return
  await lab.decide(open.value, choice.value === 'approve', note.value)
  close()
}
</script>

<template>
  <div>
    <h1 class="c-h1">Human Gate</h1>
    <p class="c-sub">每個決定都是結構圖上的一個節點。打開它，看它連到哪些證據、核准後會放行什麼，再做決定。</p>
    <p v-if="!pending.length" class="c-sub" style="margin-top: 20px">目前沒有在等你的決定。</p>
    <ul class="c-gates">
      <li v-for="g in pending" :key="g.id">
        <button type="button" class="c-gate" @click="show(g)">
          <b style="display: flex; gap: 8px; align-items: flex-start"><LabMark tone="needs" :size="14" />{{ clean(g.title) }}</b>
          <small>{{ projectOf(lab.data.value.projects, g.projectId)?.name }}<template v-if="g.externalRef">，編號 {{ refLabel(g.externalRef) }}</template></small>
        </button>
      </li>
    </ul>
    <section v-if="decided.length" style="margin-top: 36px">
      <h2 style="font-size: 13px; font-weight: 700; color: var(--ink-3)">已決定</h2>
      <ul>
        <li v-for="g in decided" :key="g.id" style="display: flex; gap: 8px; padding: 8px 0; border-bottom: 1px solid var(--line)"><LabMark :tone="g.status === 'Approved' ? 'proven' : 'fault'" />{{ clean(g.title) }}，{{ g.status === 'Approved' ? '已核准' : '已駁回' }}<template v-if="g.note">，{{ g.note }}</template></li>
      </ul>
    </section>

    <dialog ref="dlg" class="c-dialog" aria-labelledby="cd-h" @close="open = null">
      <div v-if="open" class="c-dlg">
        <div><h2 id="cd-h">{{ clean(open.title) }}</h2><p class="c-code">{{ projectOf(lab.data.value.projects, open.projectId)?.name }}<template v-if="open.externalRef">，編號 {{ refLabel(open.externalRef) }}</template></p></div>
        <div><h3>它連到的證據鏈</h3><div v-if="trace" style="margin-top: 6px"><LabTrace :trace="trace" /></div><p v-else class="lab-muted">沒有關聯的工作項目，沒有可附上的證據鏈。</p></div>
        <div>
          <h3>核准後會放行</h3>
          <ul v-if="unblocks.length"><li v-for="u in unblocks.slice(0, 5)" :key="u.id" style="display: flex; gap: 8px; padding: 3px 0"><LabMark :tone="STATUS_TONE[u.status]" :size="12" />{{ clean(u.title) }}，現在{{ STATUS_LABEL[u.status] }}</li></ul>
          <p v-else class="lab-muted">沒有被追蹤的工作在等這個決定。核准或駁回只會記錄你的決定，系統不會自動動作。</p>
        </div>
        <fieldset style="border: 0; padding: 0"><legend class="lab-sr">你的決定</legend>
          <label style="margin-right: 18px"><input v-model="choice" type="radio" value="approve"> 核准</label>
          <label><input v-model="choice" type="radio" value="reject"> 駁回</label>
        </fieldset>
        <div><label class="lab-sr" for="cd-note">備註（選填）</label><textarea id="cd-note" v-model="note" class="c-note" placeholder="給之後的自己留一句話（選填）" /></div>
        <div class="c-dlg-foot">
          <button type="button" class="lab-btn" @click="close">先不決定</button>
          <button type="button" class="lab-btn" :class="choice === 'approve' ? 'is-primary' : 'is-danger'" :disabled="lab.busy.value" @click="submit">確認{{ choice === 'approve' ? '核准' : '駁回' }}</button>
        </div>
      </div>
    </dialog>
  </div>
</template>
