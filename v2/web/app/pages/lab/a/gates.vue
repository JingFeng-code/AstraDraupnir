<script setup lang="ts">
import { clean, projectOf, refLabel } from '~/utils/lab'
import type { HumanGate } from '~/types/api'

definePageMeta({ layout: 'lab-a' })
const lab = useLab()
const pending = computed(() => lab.pendingGates.value)
const decided = computed(() => lab.data.value.gates.filter(g => g.status !== 'Pending'))
const asking = ref<{ id: string, approve: boolean } | null>(null)
const note = ref('')
const itemOf = (g: HumanGate) => (g.workItemId ? lab.byId.value.items.get(g.workItemId) : null)
const unblocks = (g: HumanGate) => {
  const it = itemOf(g)
  return it ? [it, ...lab.dependentsOf(it.id)].filter(x => x.status !== 'Done') : []
}
async function confirm(g: HumanGate) {
  if (!asking.value) return
  await lab.decide(g, asking.value.approve, note.value)
  asking.value = null
  note.value = ''
}
</script>

<template>
  <div>
    <header class="a-head">
      <h1 class="a-h1">Human Gate</h1>
      <p class="a-sub">只有你能決定。每個決定都會說明核准後會發生什麼，確認後才會送出。</p>
    </header>
    <p v-if="!pending.length" class="a-empty">目前沒有在等你的決定。</p>
    <section v-for="g in pending" :key="g.id" :id="g.id" class="a-gate" :aria-labelledby="`g-${g.id}`">
      <h2 :id="`g-${g.id}`" style="font-size: var(--s-17); font-weight: 700; display: flex; gap: 10px; align-items: flex-start"><LabMark tone="needs" :size="16" />{{ clean(g.title) }}</h2>
      <p class="a-gate-m">{{ projectOf(lab.data.value.projects, g.projectId)?.name }}<template v-if="g.externalRef">，編號 {{ refLabel(g.externalRef) }}</template></p>
      <div class="a-gate-body">
        <div v-if="itemOf(g)" style="margin-bottom: 10px"><LabTrace :trace="lab.traceOf(itemOf(g)!)" /></div>
        <p class="a-impact">
          <template v-if="unblocks(g).length"><b>核准後</b>，這 {{ unblocks(g).length }} 項會解除等待：{{ unblocks(g).map(i => i.title).slice(0, 3).join('、') }}<template v-if="unblocks(g).length > 3">…</template></template>
          <template v-else>沒有被追蹤的工作在等這個決定。核准或駁回只會記錄你的決定，系統不會自動動作。</template>
        </p>
        <template v-if="asking?.id !== g.id">
          <button type="button" class="lab-btn is-primary" @click="asking = { id: g.id, approve: true }">核准</button>
          <button type="button" class="lab-btn" style="margin-left: 8px" @click="asking = { id: g.id, approve: false }">駁回</button>
        </template>
        <div v-else class="a-confirm">
          <label class="lab-sr" :for="`n-${g.id}`">給之後的自己留一句話（選填）</label>
          <textarea :id="`n-${g.id}`" v-model="note" class="a-note" placeholder="給之後的自己留一句話（選填）" />
          <div style="display: flex; gap: 8px">
            <button type="button" class="lab-btn" :class="asking.approve ? 'is-primary' : 'is-danger'" :disabled="lab.busy.value" @click="confirm(g)">確認{{ asking.approve ? '核准' : '駁回' }}</button>
            <button type="button" class="lab-btn" @click="asking = null">取消</button>
          </div>
        </div>
      </div>
    </section>

    <section v-if="decided.length" class="a-section" style="margin-top: 48px">
      <h2 class="a-h2">已決定</h2>
      <ul>
        <li v-for="g in decided" :key="g.id" class="a-req" style="grid-template-columns: 16px minmax(0, 1fr)">
          <LabMark :tone="g.status === 'Approved' ? 'proven' : 'fault'" /><div><p class="a-req-t">{{ clean(g.title) }}</p><p class="a-req-m">{{ g.status === 'Approved' ? '已核准' : '已駁回' }}<template v-if="g.note">，{{ g.note }}</template></p></div>
        </li>
      </ul>
    </section>
  </div>
</template>
