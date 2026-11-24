<script setup lang="ts">
import { clean, projectOf, STATUS_LABEL, STATUS_TONE } from '~/utils/lab'
import type { WorkItem, WorkStatus } from '~/types/api'

/** Details and actions for the selected node. Actions that are not allowed stay visible with the reason. */
const props = defineProps<{ item: WorkItem }>()
const lab = useLab()
const moves = computed(() => lab.allowedMoves(props.item))
const needs = computed(() => lab.dependenciesOf(props.item.id))
const waiting = computed(() => lab.dependentsOf(props.item.id))
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
async function revert() { if (undo.value) { await lab.setStatus(props.item, undo.value); undo.value = null } }
</script>

<template>
  <aside class="c-drawer" aria-label="選取的工作項目">
    <p class="c-code">{{ item.code }}，{{ projectOf(lab.data.value.projects, item.projectId)?.name }}</p>
    <h2>{{ clean(item.title) }}</h2>
    <p style="display: flex; gap: 8px; align-items: center; margin-top: 6px"><LabMark :tone="STATUS_TONE[item.status]" />{{ STATUS_LABEL[item.status] }}<template v-if="item.assignee">，由 {{ item.assignee }} 負責</template></p>
    <p v-if="item.blockedReason" class="c-why" style="color: var(--fault)">被擋住的原因：{{ item.blockedReason }}</p>

    <h3>操作</h3>
    <div class="c-actions">
      <template v-for="m in moves" :key="m.to">
        <button type="button" class="lab-btn" :class="{ 'is-danger': m.to === 'Blocked' }" :disabled="!m.ok || lab.busy.value" :aria-describedby="m.ok ? undefined : `cw-${m.to}`" @click="move(m.to)">
          <LabMark :tone="STATUS_TONE[m.to]" :size="11" />{{ m.to === 'Blocked' ? '標記為被擋住' : `移到${STATUS_LABEL[m.to]}` }}
        </button>
        <p v-if="!m.ok" :id="`cw-${m.to}`" class="c-why">{{ m.why }}</p>
      </template>
    </div>
    <div v-if="asking" style="margin-top: 10px">
      <label class="lab-sr" for="cw-reason">被擋住的原因</label>
      <textarea id="cw-reason" v-model="reason" class="c-note" placeholder="被擋住的原因（必填）" />
      <div class="c-actions"><button type="button" class="lab-btn is-danger" :disabled="!reason.trim()" @click="move('Blocked')">確認標記為被擋住</button><button type="button" class="lab-btn" @click="asking = false">取消</button></div>
    </div>
    <p v-if="undo" class="c-why" style="margin-top: 8px">已更新。<button type="button" class="lab-link" @click="revert">復原成「{{ STATUS_LABEL[undo] }}」</button></p>

    <h3>先要完成</h3>
    <p v-if="!needs.length" class="lab-muted">沒有先決條件</p>
    <ul class="c-kv"><li v-for="d in needs" :key="d.id"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/c/w/${d.id}`" class="lab-link">{{ clean(d.title) }}</NuxtLink></li></ul>
    <h3>正在等它</h3>
    <p v-if="!waiting.length" class="lab-muted">沒有項目在等它</p>
    <ul class="c-kv"><li v-for="d in waiting" :key="d.id"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/c/w/${d.id}`" class="lab-link">{{ clean(d.title) }}</NuxtLink></li></ul>
    <p style="margin-top: 16px"><NuxtLink :to="`/lab/c/w/${item.id}`" class="lab-link">開啟完整追溯</NuxtLink></p>
  </aside>
</template>
