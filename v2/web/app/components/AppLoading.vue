<script setup lang="ts">
withDefaults(defineProps<{
  label?: string
}>(), {
  label: '載入中…',
})
</script>

<template>
  <div class="ui-panel ui-loading" role="status">
    <span class="ui-loading-mark" aria-hidden="true"></span>
    <span>{{ label }}</span>
  </div>
</template>

<style scoped>
.ui-loading { display: flex; align-items: center; gap: 12px; padding: 20px 22px; color: var(--c-ink-2); font-size: 15px; }
.ui-loading-mark { width: 16px; height: 16px; flex: none; border-radius: 50%; border: 2px solid var(--c-line-strong); border-top-color: var(--c-brand); animation: ui-spin 900ms linear infinite; }
@keyframes ui-spin { to { transform: rotate(360deg); } }
</style>
