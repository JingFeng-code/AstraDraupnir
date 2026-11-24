<script setup lang="ts">
withDefaults(defineProps<{
  tone?: 'error' | 'success' | 'info'
  message: string
  details?: string[]
}>(), {
  tone: 'error',
  details: () => [],
})

const toneClass = {
  error: 'border-[#c98a8a] bg-[#fdf1f1] text-[#7a1d1d]',
  success: 'border-[#9dc3a6] bg-[#eef7f0] text-[#1e4d2b]',
  info: 'border-line bg-surface-muted text-ink-muted',
}
</script>

<template>
  <div
    class="rounded-xl border px-4 py-3 text-sm"
    :class="toneClass[tone]"
    :role="tone === 'error' ? 'alert' : 'status'"
  >
    <p class="font-medium">{{ message }}</p>
    <ul v-if="details.length" class="mt-1 list-disc pl-5">
      <li v-for="detail in details" :key="detail">{{ detail }}</li>
    </ul>
  </div>
</template>
