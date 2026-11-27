<script setup lang="ts">
import type { Tone } from '~/utils/lab'

/** Status as shape + color + words. */
defineProps<{ tone: Tone, label: string }>()
</script>

<template>
  <span class="d-pill" :class="`is-${tone}`"><LabMark :tone="tone" :size="14" />{{ label }}</span>
</template>
