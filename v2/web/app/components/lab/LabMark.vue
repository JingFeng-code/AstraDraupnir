<script setup lang="ts">
import type { Tone } from '~/utils/lab'

/** A status as shape and color together. proven: filled disc, moving: half disc, needs: diamond, fault: square, idle: ring. */
const props = withDefaults(defineProps<{ tone: Tone, dashed?: boolean, size?: number, label?: string }>(), { size: 14 })
</script>

<template>
  <span class="lm" :class="`lm-${props.tone}`" :role="props.label ? 'img' : undefined" :aria-label="props.label" :aria-hidden="props.label ? undefined : true">
    <svg :width="props.size" :height="props.size" viewBox="0 0 14 14" focusable="false">
      <template v-if="props.tone === 'proven'"><circle cx="7" cy="7" r="5.5" fill="currentColor" /></template>
      <template v-else-if="props.tone === 'moving'">
        <circle cx="7" cy="7" r="5" fill="none" stroke="currentColor" stroke-width="1.8" />
        <path d="M7 2a5 5 0 0 0 0 10z" fill="currentColor" />
      </template>
      <template v-else-if="props.tone === 'needs'"><path d="M7 1 13 7 7 13 1 7z" fill="currentColor" /></template>
      <template v-else-if="props.tone === 'fault'"><rect x="2" y="2" width="10" height="10" rx="1.5" fill="currentColor" /></template>
      <template v-else>
        <circle cx="7" cy="7" r="5" fill="none" stroke="currentColor" stroke-width="1.8" :stroke-dasharray="props.dashed ? '2.2 2.2' : undefined" />
      </template>
    </svg>
  </span>
</template>

<style>
.lm { display: inline-flex; flex: none; line-height: 0; }
.lm-proven { color: var(--fix); }
.lm-moving { color: var(--move); }
.lm-needs { color: var(--need); }
.lm-fault { color: var(--fault); }
.lm-idle { color: var(--idle); }
</style>
