<script setup lang="ts">
const lab = useLab()
onMounted(() => lab.initTheme())
const dark = computed(() => lab.theme.value === 'dark')
</script>

<template>
  <button type="button" class="lab-btn lab-theme" :aria-pressed="dark" aria-label="切換深色與淺色主題" @click="lab.toggleTheme()">
    <svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true">
      <circle cx="7" cy="7" r="5.2" fill="none" stroke="currentColor" stroke-width="1.6" />
      <path d="M7 1.8a5.2 5.2 0 0 1 0 10.4z" fill="currentColor" />
    </svg>
    <span>{{ dark ? '深色' : '淺色' }}</span>
  </button>
</template>
