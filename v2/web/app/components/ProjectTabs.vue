<script setup lang="ts">
import { canImport } from '~/utils/permissions'

const route = useRoute()
const auth = useAuth()
const id = computed(() => String(route.params.id))
const base = computed(() => `/projects/${id.value}`)

const tabs = computed(() => [
  { label: '總覽', to: base.value, exact: true },
  { label: '需求', to: `${base.value}/requirements` },
  { label: '工作項目', to: `${base.value}/work-items` },
  { label: 'Sprint／Milestone', to: `${base.value}/sprints` },
  { label: 'Agent 執行', to: `${base.value}/agents` },
  { label: '證據與審查', to: `${base.value}/evidence` },
  { label: 'Human Gate', to: `${base.value}/gates` },
  { label: '交付報告', to: `${base.value}/reports` },
  { label: '匯入 Supervisor', to: `${base.value}/import` },
].filter(tab => tab.label !== '匯入 Supervisor' || canImport(auth.state.value.user)))

function isActive(to: string, exact = false): boolean {
  return exact ? route.path === to : route.path === to || route.path.startsWith(`${to}/`)
}
</script>

<template>
  <nav class="min-w-0" aria-label="專案導覽">
    <ul class="ui-tabs !mt-0 flex-wrap">
      <li v-for="tab in tabs" :key="tab.to">
        <NuxtLink
          :to="tab.to"
          class="inline-block text-sm"
          :class="{ 'is-on': isActive(tab.to, tab.exact) }"
          :aria-current="isActive(tab.to, tab.exact) ? 'page' : undefined"
        >
          {{ tab.label }}
        </NuxtLink>
      </li>
    </ul>
  </nav>
</template>
