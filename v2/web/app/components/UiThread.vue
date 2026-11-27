<script setup lang="ts">
import { STOP_STATE_LABEL, type StopState, type Trace } from '~/lib/engineering'

/** The five-station thread for lists: requirement, work, execution, evidence, decision. */
const props = defineProps<{ trace: Trace }>()
const tone = (s: StopState) => (s === 'missing' || s === 'na' ? 'idle' : s)
const solid = (i: number) => {
  const a = props.trace.stops[i]!.state
  const b = props.trace.stops[i + 1]?.state
  return !!b && !['missing', 'na'].includes(a) && !['missing', 'na'].includes(b)
}
const summary = computed(() => props.trace.stops.map(s => `${s.name}${STOP_STATE_LABEL[s.state]}`).join('，'))
</script>

<template>
  <span class="ui-thread" role="img" :aria-label="`追溯：${summary}`" :title="trace.stops.map(s => `${s.name}：${s.note}`).join('\n')">
    <template v-for="(s, i) in trace.stops" :key="s.name">
      <UiMark v-if="s.state !== 'na'" :tone="tone(s.state)" :dashed="s.state === 'missing'" :size="16" />
      <svg v-else width="16" height="16" viewBox="0 0 16 16" aria-hidden="true"><circle cx="8" cy="8" r="2" fill="var(--c-line-strong)" /></svg>
      <i v-if="i < trace.stops.length - 1" :class="{ 'is-solid': solid(i) }" />
    </template>
  </span>
</template>
