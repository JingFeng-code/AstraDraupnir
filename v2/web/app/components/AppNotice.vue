<script setup lang="ts">
withDefaults(defineProps<{
  tone?: 'error' | 'success' | 'info'
  message: string
  details?: string[]
}>(), {
  tone: 'error',
  details: () => [],
})
</script>

<template>
  <div
    class="ui-callout ui-notice"
    :class="`is-${tone}`"
    :role="tone === 'error' ? 'alert' : 'status'"
  >
    <p class="ui-notice-message">{{ message }}</p>
    <ul v-if="details.length" class="ui-notice-details">
      <li v-for="detail in details" :key="detail">{{ detail }}</li>
    </ul>
    <div v-if="$slots.default"><slot /></div>
  </div>
</template>

<style scoped>
.ui-notice { padding: 16px 18px; gap: 8px; }
.ui-notice.is-error { background: var(--c-fault-bg); box-shadow: inset 4px 0 0 var(--c-fault); color: var(--c-ink); }
.ui-notice.is-success { background: var(--c-fix-bg); box-shadow: inset 4px 0 0 var(--c-fix); color: var(--c-ink); }
.ui-notice.is-info { background: var(--c-sunken); box-shadow: inset 4px 0 0 var(--c-line-strong); color: var(--c-ink); }
.ui-notice-message { font-size: 16px; font-weight: 600; }
.ui-notice-details { margin-left: 18px; list-style: disc; font-size: 14px; color: var(--c-ink-2); }
</style>
