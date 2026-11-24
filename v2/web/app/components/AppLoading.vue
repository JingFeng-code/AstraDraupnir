<script setup lang="ts">
withDefaults(defineProps<{
  label?: string
}>(), {
  label: '載入中…',
})
</script>

<template>
  <p class="flex items-center gap-2 py-6 text-sm text-ink-muted" role="status">
    <span class="inline-block h-3 w-3 animate-spin rounded-full border-2 border-line border-t-navy-700" aria-hidden="true"></span>
    {{ label }}
  </p>
</template>
