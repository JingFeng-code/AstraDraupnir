<script setup lang="ts">
import type { Labeled } from '~/utils/labels'
import type { Tone } from '~/lib/engineering'

withDefaults(defineProps<{
  value: Labeled
  tone?: Tone
}>(), {
  tone: 'idle',
})
</script>

<template>
  <span class="ui-status-badge" :class="value.className">
    <UiMark :tone="tone" :size="12" />
    {{ value.label }}
  </span>
</template>

<style scoped>
.ui-status-badge { display: inline-flex; align-items: center; gap: 6px; border-radius: 8px; padding: 4px 10px; font-size: 14px; font-weight: 600; line-height: 20px; white-space: nowrap; }
</style>
