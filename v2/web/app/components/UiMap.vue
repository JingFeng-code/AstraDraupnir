<script setup lang="ts">
import { clean, isTestData, STATUS_LABEL, STOPS, type StopState } from '~/lib/engineering'
import type { WorkItem } from '~/types/api'

/** Quality map: one row per work item, five stations. Selecting a row marks the items it depends on or that wait on it. */
const props = defineProps<{ items: WorkItem[], modelValue: string, showProject?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [id: string] }>()
const ws = useWorkspace()
const rel = computed(() => {
  const s = props.items.find(i => i.id === props.modelValue)
  return s ? new Set([...ws.dependenciesOf(s.id), ...ws.dependentsOf(s.id)].map(i => i.id)) : new Set<string>()
})
const tone = (s: StopState) => (s === 'missing' || s === 'na' ? 'idle' : s)
const waitingOn = (i: WorkItem) => ws.dependenciesOf(i.id).filter(d => d.status !== 'Done')
</script>

<template>
  <div class="ui-lanes" role="table" aria-label="工作項目與五站追溯">
    <div class="ui-hrow" role="row"><div role="columnheader">工作項目</div><div v-for="s in STOPS" :key="s" role="columnheader">{{ s }}</div></div>
    <div v-for="i in items" :key="i.id" class="ui-rrow" :class="{ 'is-on': i.id === modelValue, 'is-rel': rel.has(i.id) }" role="row" @click="emit('update:modelValue', i.id)">
      <div role="cell">
        <button type="button" class="ui-row-t" style="text-align: left" :aria-pressed="i.id === modelValue" @click.stop="emit('update:modelValue', i.id)">{{ clean(i.title) }}</button>
        <span class="ui-row-m">{{ i.code }}<template v-if="showProject">，{{ ws.byId.value.projects.get(i.projectId)?.name }}</template>，{{ STATUS_LABEL[i.status] }} <span v-if="isTestData(i.title)" class="ui-tag is-test">測試資料</span></span>
        <span v-if="waitingOn(i).length" class="ui-row-m">等 {{ clean(waitingOn(i)[0]!.title) }}<template v-if="waitingOn(i).length > 1">等 {{ waitingOn(i).length }} 項</template></span>
      </div>
      <div v-for="st in ws.traceOf(i).stops" :key="st.name" class="ui-cell" role="cell">
        <UiMark :tone="tone(st.state)" :dashed="st.state === 'missing'" :size="18" :label="`${st.name}：${st.note}`" />
        <span>{{ st.note }}</span>
      </div>
    </div>
  </div>
</template>
