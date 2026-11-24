<script setup lang="ts">
import { clean, projectOf, refLabel, STATUS_LABEL, STATUS_TONE } from '~/utils/lab'

/** Guided decision: read the evidence, see the consequence, then decide. One step at a time. */
definePageMeta({ layout: 'lab-b' })
const lab = useLab()
const route = useRoute()
const gate = computed(() => lab.data.value.gates.find(g => g.id === route.params.id))
const item = computed(() => (gate.value?.workItemId ? lab.byId.value.items.get(gate.value.workItemId) : undefined))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const unblocks = computed(() => (item.value ? [item.value, ...lab.dependentsOf(item.value.id)].filter(x => x.status !== 'Done') : []))
const STEPS = ['讀證據', '看後果', '決定'] as const
const step = ref(0)
const choice = ref<'approve' | 'reject'>('approve')
const note = ref('')
const done = ref(false)
async function submit() {
  if (!gate.value) return
  await lab.decide(gate.value, choice.value === 'approve', note.value)
  done.value = true
}
</script>

<template>
  <div v-if="gate" class="b-brief" style="grid-template-columns: minmax(0, 1fr)">
    <section class="b-pane" aria-labelledby="gf-h" style="border-right: 0">
      <div class="b-pane-h"><h1 id="gf-h">Human Gate，逐步決定</h1><NuxtLink to="/lab/b" class="lab-link" style="font-size: 12px">回到審查桌</NuxtLink></div>
      <div class="b-pad" style="max-width: 760px; margin: 0 auto; width: 100%; padding-top: 32px">
        <h2 class="b-h1">{{ clean(gate.title) }}</h2>
        <p class="b-meta">{{ projectOf(lab.data.value.projects, gate.projectId)?.name }}<template v-if="gate.externalRef">，編號 {{ refLabel(gate.externalRef) }}</template></p>

        <template v-if="gate.status !== 'Pending' || done">
          <p class="b-sec" style="margin-top: 20px; padding-left: 0; display: flex; gap: 8px; align-items: center"><LabMark :tone="gate.status === 'Approved' ? 'proven' : 'fault'" />這個決定已{{ gate.status === 'Approved' ? '核准' : '駁回' }}。<template v-if="gate.note">備註：{{ gate.note }}</template></p>
          <NuxtLink to="/lab/b" class="lab-btn" style="margin-top: 12px">回到審查桌</NuxtLink>
        </template>
        <template v-else>
          <ol class="b-steps" aria-label="決定的步驟" style="margin-top: 22px">
            <li v-for="(s, i) in STEPS" :key="s" :class="{ 'is-done': i < step, 'is-on': i === step }" :aria-current="i === step ? 'step' : undefined">{{ i + 1 }}. {{ s }}</li>
          </ol>

          <div v-if="step === 0">
            <LabBChain v-if="trace" :trace="trace" />
            <p v-else class="lab-muted">這個決定沒有關聯到工作項目，沒有可附上的證據鏈。</p>
          </div>
          <div v-else-if="step === 1">
            <h3 class="b-side-h">核准後會發生什麼</h3>
            <ul v-if="unblocks.length" class="b-impact">
              <li v-for="u in unblocks.slice(0, 6)" :key="u.id"><LabMark :tone="STATUS_TONE[u.status]" :size="12" /><span>{{ clean(u.title) }}，現在{{ STATUS_LABEL[u.status] }}</span></li>
            </ul>
            <p v-else class="lab-muted">沒有被追蹤的工作在等這個決定。核准或駁回只會記錄你的決定，系統不會自動動作。</p>
          </div>
          <div v-else>
            <fieldset style="border: 0; padding: 0">
              <legend class="b-side-h">你的決定</legend>
              <label style="display: flex; gap: 8px; padding: 6px 0"><input v-model="choice" type="radio" value="approve">核准</label>
              <label style="display: flex; gap: 8px; padding: 6px 0"><input v-model="choice" type="radio" value="reject">駁回</label>
            </fieldset>
            <label class="b-side-h" for="gf-note" style="display: block; margin-top: 10px">備註（選填）</label>
            <textarea id="gf-note" v-model="note" class="b-note" placeholder="給之後的自己留一句話" />
          </div>

          <div class="b-actions" style="grid-auto-flow: column; justify-content: start; margin-top: 18px">
            <button v-if="step > 0" type="button" class="lab-btn" @click="step--">上一步</button>
            <button v-if="step < 2" type="button" class="lab-btn is-primary" @click="step++">下一步：{{ STEPS[step + 1] }}</button>
            <button v-else type="button" class="lab-btn" :class="choice === 'approve' ? 'is-primary' : 'is-danger'" :disabled="lab.busy.value" @click="submit">確認{{ choice === 'approve' ? '核准' : '駁回' }}</button>
          </div>
        </template>
      </div>
    </section>
  </div>
  <p v-else class="b-empty">{{ lab.data.value.ready ? '找不到這個決定。' : '讀取中…' }}</p>
</template>
