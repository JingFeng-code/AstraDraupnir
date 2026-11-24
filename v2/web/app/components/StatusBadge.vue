<script setup lang="ts">
import type { Labeled } from '~/utils/labels'

defineProps<{
  value: Labeled
}>()
</script>

<template>
  <span class="inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold leading-5" :class="value.className">
    {{ value.label }}
  </span>
</template>
