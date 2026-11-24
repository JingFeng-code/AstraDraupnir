<script setup lang="ts">
import type { Tone } from '~/lib/engineering'

/** Status as shape + color + words. */
defineProps<{ tone: Tone, label: string }>()
</script>

<template>
  <span class="ui-pill" :class="`is-${tone}`"><UiMark :tone="tone" :size="14" />{{ label }}</span>
</template>
