<script setup lang="ts">
withDefaults(defineProps<{
  title: string
  description?: string
}>(), {
  description: undefined,
})
</script>

<template>
  <div class="ui-panel ui-empty-state">
    <span class="ui-empty-shape" aria-hidden="true"></span>
    <p class="ui-h3">{{ title }}</p>
    <p v-if="description" class="ui-meta">{{ description }}</p>
    <div v-if="$slots.default" class="ui-empty-action"><slot /></div>
  </div>
</template>

<style scoped>
.ui-empty-state { display: grid; justify-items: center; gap: 10px; padding: 34px 22px; text-align: center; }
.ui-empty-shape { width: 30px; height: 30px; border-radius: 9px; border: 2px dashed var(--c-line-strong); }
.ui-empty-action { margin-top: 8px; }
</style>
