<script setup lang="ts">
import { STATUS_LABEL, STATUS_TONE } from '~/utils/lab'
import type { WorkItem, WorkStatus } from '~/types/api'

/** Operations for one work item. Only moves that make sense are offered; refused ones say why. */
const props = defineProps<{ item: WorkItem }>()
const lab = useLab()
const moves = computed(() => lab.allowedMoves(props.item))
const asking = ref(false)
const reason = ref('')
const undo = ref<WorkStatus | null>(null)
watch(() => props.item.id, () => { asking.value = false; reason.value = ''; undo.value = null })

async function move(to: WorkStatus) {
  if (to === 'Blocked' && !asking.value) { asking.value = true; return }
  const from = props.item.status
  await lab.setStatus(props.item, to, to === 'Blocked' ? reason.value : undefined)
  if (props.item.status === to) undo.value = from
  asking.value = false
  reason.value = ''
}
async function revert() {
  if (undo.value) { await lab.setStatus(props.item, undo.value); undo.value = null }
}
</script>

<template>
  <div>
    <div class="b-sec">
      <h3 class="b-side-h">現在</h3>
      <p style="display: flex; gap: 8px; align-items: center; font-weight: 600"><LabMark :tone="STATUS_TONE[item.status]" />{{ STATUS_LABEL[item.status] }}</p>
      <p v-if="item.blockedReason" class="lab-muted" style="margin-top: 4px">原因：{{ item.blockedReason }}</p>
    </div>
    <div class="b-sec">
      <h3 class="b-side-h">可以做的</h3>
      <ul class="b-moves">
        <li v-for="m in moves" :key="m.to">
          <button type="button" class="lab-btn" :class="{ 'is-danger': m.to === 'Blocked' }" :disabled="!m.ok || lab.busy.value" :aria-describedby="m.ok ? undefined : `bw-${m.to}`" @click="move(m.to)">
            <LabMark :tone="STATUS_TONE[m.to]" :size="11" />{{ m.to === 'Blocked' ? '標記為被擋住' : `移到${STATUS_LABEL[m.to]}` }}
          </button>
          <p v-if="!m.ok" :id="`bw-${m.to}`" class="b-why">{{ m.why }}</p>
        </li>
      </ul>
      <div v-if="asking" style="margin-top: 10px">
        <label class="lab-sr" for="bw-reason">被擋住的原因</label>
        <textarea id="bw-reason" v-model="reason" class="b-note" placeholder="被擋住的原因（必填）" />
        <div class="b-actions">
          <button type="button" class="lab-btn is-danger" :disabled="!reason.trim()" @click="move('Blocked')">確認標記為被擋住</button>
          <button type="button" class="lab-btn" @click="asking = false">取消</button>
        </div>
      </div>
      <p v-if="undo" class="b-why" style="margin-top: 10px">已更新。<button type="button" class="lab-link" @click="revert">復原成「{{ STATUS_LABEL[undo] }}」</button></p>
    </div>
  </div>
</template>

<style>
.b-moves { display: grid; gap: 10px; }
.b-moves .lab-btn { width: 100%; justify-content: flex-start; gap: 8px; min-height: 36px; }
.b-why { margin-top: 3px; font-size: 12px; color: var(--ink-2); }
</style>
