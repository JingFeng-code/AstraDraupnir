<script setup lang="ts">
const route = useRoute()
const id = computed(() => String(route.params.id))
const base = computed(() => `/projects/${id.value}`)

const tabs = computed(() => [
  { label: '總覽', to: base.value, exact: true },
  { label: '需求', to: `${base.value}/requirements` },
  { label: '工作項目', to: `${base.value}/work-items` },
  { label: 'Sprint／Milestone', to: `${base.value}/sprints` },
  { label: 'Agent 執行', to: `${base.value}/agents` },
  { label: '證據與 Review', to: `${base.value}/evidence` },
  { label: 'Human Gate', to: `${base.value}/gates` },
  { label: '交付報告', to: `${base.value}/reports` },
  { label: '匯入 Supervisor', to: `${base.value}/import` },
])

function isActive(to: string, exact = false): boolean {
  return exact ? route.path === to : route.path === to || route.path.startsWith(`${to}/`)
}
</script>

<template>
  <nav class="site-container overflow-x-auto border-b border-line bg-surface-muted/60" aria-label="專案導覽">
    <ul class="flex min-w-max gap-1 pb-1">
      <li v-for="tab in tabs" :key="tab.to">
        <NuxtLink
          :to="tab.to"
          class="inline-block rounded-t-lg border-b-2 px-3 py-2 text-sm font-medium no-underline"
          :class="isActive(tab.to, tab.exact)
            ? 'border-navy-700 bg-paper text-navy-800'
            : 'border-transparent text-ink-muted hover:bg-paper hover:text-navy-700'"
          :aria-current="isActive(tab.to, tab.exact) ? 'page' : undefined"
        >
          {{ tab.label }}
        </NuxtLink>
      </li>
    </ul>
  </nav>
</template>
