<script setup lang="ts">
import { STOP_STATE_LABEL, STOPS, type StopState, type Trace } from '~/utils/lab'

/**
 * The Astra thread: five stops from requirement to decision.
 * A filled disc is a fixed point (that stage is proven); a dashed ring is nothing to stand on yet.
 * compact: marks only, for lists. full: with stage names and what each stop says.
 */
const props = withDefaults(defineProps<{ trace: Trace, compact?: boolean }>(), { compact: false })

const summary = computed(() => props.trace.stops.map(s => `${s.name}${STOP_STATE_LABEL[s.state]}`).join('，'))
function tone(state: StopState) {
  return state === 'proven' ? 'proven' : state === 'moving' ? 'moving' : state === 'needs' ? 'needs' : state === 'fault' ? 'fault' : 'idle'
}
/** The link after a stop is solid only when both of its ends stand on something. */
function solid(i: number) {
  const a = props.trace.stops[i]!.state
  const b = props.trace.stops[i + 1]?.state
  return !!b && a !== 'missing' && a !== 'na' && b !== 'missing' && b !== 'na'
}
</script>

<template>
  <span v-if="props.compact" class="lt lt-compact" role="img" :aria-label="`追溯線：${summary}`">
    <template v-for="(stop, i) in props.trace.stops" :key="stop.name">
      <span class="lt-stop" :title="`${stop.name}：${stop.note}`">
        <LabMark v-if="stop.state !== 'na'" :tone="tone(stop.state)" :dashed="stop.state === 'missing'" :size="12" />
        <i v-else class="lt-na" />
      </span>
      <i v-if="i < props.trace.stops.length - 1" class="lt-link" :class="{ 'is-solid': solid(i) }" aria-hidden="true" />
    </template>
  </span>
  <ol v-else class="lt-full" :aria-label="`追溯線：${summary}`">
    <li v-for="(stop, i) in props.trace.stops" :key="stop.name">
      <span class="lt-rail">
        <LabMark v-if="stop.state !== 'na'" :tone="tone(stop.state)" :dashed="stop.state === 'missing'" :size="15" />
        <i v-else class="lt-na" />
        <i v-if="i < props.trace.stops.length - 1" class="lt-link" :class="{ 'is-solid': solid(i) }" aria-hidden="true" />
      </span>
      <span class="lt-name">{{ stop.name }}</span>
      <span class="lt-note">{{ stop.note }}</span>
    </li>
  </ol>
</template>

<style>
.lt { display: inline-flex; align-items: center; }
.lt-stop { display: inline-flex; align-items: center; }
.lt-link { display: block; flex: none; width: 8px; height: 0; border-top: 2px dashed var(--line-strong); }
.lt-link.is-solid { border-top-style: solid; border-top-color: var(--fix); }
.lt-compact .lt-link { width: 9px; border-top-width: 1.5px; }
.lt-na { width: 5px; height: 5px; border-radius: 50%; background: var(--line-strong); margin: 0 3px; flex: none; }
.lt-full { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 0 10px; max-width: 760px; }
.lt-full li { display: flex; flex-direction: column; min-width: 0; }
.lt-rail { display: flex; align-items: center; gap: 6px; height: 18px; margin-bottom: 6px; }
.lt-rail .lt-link { flex: 1; width: auto; }
.lt-name { font-weight: 700; font-size: 14px; }
.lt-note { font-size: 13px; color: var(--ink-2); line-height: 1.4; padding-right: 8px; overflow-wrap: anywhere; }
@media (max-width: 640px) { .lt-full { grid-template-columns: 1fr; gap: 10px; } .lt-full li { flex-direction: row; align-items: baseline; gap: 10px; } .lt-rail { margin: 0; } .lt-rail .lt-link { display: none; } }
</style>
