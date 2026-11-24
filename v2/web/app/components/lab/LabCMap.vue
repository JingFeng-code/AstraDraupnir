<script setup lang="ts">
import { clean, isTestData, STATUS_LABEL, STATUS_TONE, STOPS, type StopState } from '~/utils/lab'
import type { WorkItem } from '~/types/api'

/** The map: one row per work item, five lanes for what each stage can prove. Selecting a row lights its dependency chain. */
const props = defineProps<{ items: WorkItem[], modelValue: string, showProject?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [id: string] }>()
const lab = useLab()
const rel = computed(() => {
  const s = props.items.find(i => i.id === props.modelValue)
  if (!s) return new Set<string>()
  return new Set([...lab.dependenciesOf(s.id), ...lab.dependentsOf(s.id)].map(i => i.id))
})
const tone = (s: StopState) => (s === 'missing' || s === 'na' ? 'idle' : s)
const waitingOn = (i: WorkItem) => lab.dependenciesOf(i.id).filter(d => d.status !== 'Done')
</script>

<template>
  <div class="c-map">
    <div class="c-lanes" role="table" aria-label="工作項目與五站追溯">
      <div class="c-hrow" role="row"><div class="c-lane-h" role="columnheader">工作項目</div>
      <div v-for="s in STOPS" :key="s" class="c-lane-h" role="columnheader">{{ s }}</div></div>
      <div v-for="i in items" :key="i.id" class="c-row" :class="{ 'is-on': i.id === modelValue, 'is-rel': rel.has(i.id) }" role="row">
        <div role="cell">
          <button type="button" class="c-name" :aria-pressed="i.id === modelValue" @click="emit('update:modelValue', i.id)">{{ clean(i.title) }}</button>
          <span class="c-code">{{ i.code }}<template v-if="showProject">，{{ lab.byId.value.projects.get(i.projectId)?.name }}</template>，{{ STATUS_LABEL[i.status] }}<span v-if="isTestData(i.title)" class="c-test" style="margin-left: 6px">測試資料</span></span>
          <span v-if="waitingOn(i).length" class="c-dep">等 {{ waitingOn(i).map(d => clean(d.title)).slice(0, 1).join('') }}<template v-if="waitingOn(i).length > 1"> 等 {{ waitingOn(i).length }} 項</template></span>
        </div>
        <div v-for="st in lab.traceOf(i).stops" :key="st.name" class="c-cell" role="cell">
          <LabMark :tone="tone(st.state)" :dashed="st.state === 'missing'" :size="14" :label="`${st.name}：${st.note}`" />
          <span>{{ st.note }}</span>
        </div>
      </div>
    </div>
  </div>
</template>
