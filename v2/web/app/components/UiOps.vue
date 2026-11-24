<script setup lang="ts">
import { clean, projectOf, STATUS_LABEL, STATUS_TONE } from '~/lib/engineering'
import type { WorkItem, WorkStatus } from '~/types/api'

/** Work item operations. Moves that are not allowed stay visible with the reason. */
const props = defineProps<{ item: WorkItem, compact?: boolean }>()
const ws = useWorkspace()
const moves = computed(() => ws.allowedMoves(props.item))
const needs = computed(() => ws.dependenciesOf(props.item.id))
const waiting = computed(() => ws.dependentsOf(props.item.id))
const asking = ref(false)
const reason = ref('')
const undo = ref<WorkStatus | null>(null)
watch(() => props.item.id, () => { asking.value = false; reason.value = ''; undo.value = null })
async function move(to: WorkStatus) {
  if (to === 'Blocked' && !asking.value) { asking.value = true; return }
  const from = props.item.status
  await ws.setStatus(props.item, to, to === 'Blocked' ? reason.value : undefined)
  if (props.item.status === to) undo.value = from
  asking.value = false
  reason.value = ''
}
async function revert() { if (undo.value) { await ws.setStatus(props.item, undo.value); undo.value = null } }
</script>

<template>
  <div class="ui-stack">
    <div>
      <p class="ui-meta">{{ item.code }}，{{ projectOf(ws.data.value.projects, item.projectId)?.name }}</p>
      <h2 class="ui-h2" style="margin-top: 4px">{{ clean(item.title) }}</h2>
      <p style="margin-top: 10px"><UiPill :tone="STATUS_TONE[item.status]" :label="STATUS_LABEL[item.status]" /></p>
      <p v-if="item.blockedReason" class="ui-callout is-fault" style="margin-top: 10px">被擋住的原因：{{ item.blockedReason }}</p>
    </div>
    <div>
      <h3 class="ui-h3" style="margin-bottom: 10px">操作</h3>
      <ul class="ui-stack" style="gap: 8px">
        <li v-for="m in moves" :key="m.to">
          <button type="button" class="ui-btn" :class="{ 'is-danger': m.to === 'Blocked' }" style="width: 100%; justify-content: flex-start" :disabled="!m.ok || ws.busy.value" :aria-describedby="m.ok ? undefined : `dw-${m.to}`" @click="move(m.to)">
            <UiMark :tone="STATUS_TONE[m.to]" :size="14" />{{ m.to === 'Blocked' ? '標記為被擋住' : `移到${STATUS_LABEL[m.to]}` }}
          </button>
          <p v-if="!m.ok" :id="`dw-${m.to}`" class="ui-meta" style="margin-top: 4px">{{ m.why }}</p>
        </li>
      </ul>
      <div v-if="asking" class="ui-stack" style="margin-top: 12px">
        <label class="lab-sr" for="dw-reason">被擋住的原因</label>
        <textarea id="dw-reason" v-model="reason" class="ui-field" placeholder="被擋住的原因（必填）" />
        <div style="display: flex; gap: 8px"><button type="button" class="ui-btn is-danger" :disabled="!reason.trim()" @click="move('Blocked')">確認標記為被擋住</button><button type="button" class="ui-btn is-quiet" @click="asking = false">取消</button></div>
      </div>
      <p v-if="undo" class="ui-meta" style="margin-top: 10px">已更新。<button type="button" style="text-decoration: underline; font-weight: 600" @click="revert">復原成「{{ STATUS_LABEL[undo] }}」</button></p>
    </div>
    <div v-if="!compact">
      <h3 class="ui-h3" style="margin-bottom: 6px">先要完成</h3>
      <p v-if="!needs.length" class="ui-meta">沒有先決條件</p>
      <ul><li v-for="d in needs" :key="d.id" style="display: flex; gap: 10px; padding: 6px 0"><UiMark :tone="STATUS_TONE[d.status]" :size="14" /><NuxtLink :to="`/work/${d.id}`" style="text-decoration: underline; text-underline-offset: 3px">{{ clean(d.title) }}</NuxtLink></li></ul>
      <h3 class="ui-h3" style="margin: 14px 0 6px">正在等它</h3>
      <p v-if="!waiting.length" class="ui-meta">沒有項目在等它</p>
      <ul><li v-for="d in waiting" :key="d.id" style="display: flex; gap: 10px; padding: 6px 0"><UiMark :tone="STATUS_TONE[d.status]" :size="14" /><NuxtLink :to="`/work/${d.id}`" style="text-decoration: underline; text-underline-offset: 3px">{{ clean(d.title) }}</NuxtLink></li></ul>
    </div>
  </div>
</template>
