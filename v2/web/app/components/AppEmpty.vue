<script setup lang="ts">
withDefaults(defineProps<{
  title: string
  description?: string
}>(), {
  description: undefined,
})
</script>

<template>
  <div class="rounded-2xl border border-dashed border-line bg-surface px-6 py-10 text-center">
    <p class="text-base font-semibold text-ink">{{ title }}</p>
    <p v-if="description" class="mx-auto mt-2 max-w-prose text-sm text-ink-muted">{{ description }}</p>
    <div v-if="$slots.default" class="mt-4 flex justify-center">
      <slot />
    </div>
  </div>
</template>
