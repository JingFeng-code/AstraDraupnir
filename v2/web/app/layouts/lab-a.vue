<script setup lang="ts">
import '~/assets/lab/tokens.css'
import '~/assets/lab/a.css'

useHead({ htmlAttrs: { 'data-lab': 'a' }, title: 'Draupnir 日誌（方向 A）' })
const lab = useLab()
const route = useRoute()
onMounted(async () => { lab.initTheme(); await lab.load() })
const needs = (projectId: string) => lab.pendingGates.value.filter(g => g.projectId === projectId).length
const projects = computed(() => [...lab.data.value.projects].sort((a, b) => needs(b.id) - needs(a.id) || a.name.localeCompare(b.name)))
const nav = [
  { to: '/lab/a', label: '日誌', exact: true },
  { to: '/lab/a/agents', label: 'Agent' },
  { to: '/lab/a/gates', label: 'Human Gate' },
]
const active = (n: { to: string, exact?: boolean }) => (n.exact ? route.path === n.to : route.path.startsWith(n.to))
</script>

<template>
  <div class="a-shell">
    <aside class="a-rail" aria-label="導覽">
      <NuxtLink to="/lab/a" class="a-brand" aria-label="Draupnir 日誌首頁">
        <svg width="26" height="14" viewBox="0 0 26 14" aria-hidden="true">
          <path d="M1 7h8" stroke="currentColor" stroke-width="2" stroke-dasharray="3 2.5" />
          <path d="M11 7h8" stroke="var(--fix)" stroke-width="2" />
          <circle cx="21.5" cy="7" r="3.6" fill="var(--fix)" />
        </svg>
        <span class="a-brand-name">Draupnir</span>
      </NuxtLink>
      <nav aria-label="主要">
        <NuxtLink v-for="n in nav" :key="n.to" :to="n.to" class="a-nav" :class="{ 'is-on': active(n) }" :aria-current="active(n) ? 'page' : undefined">
          {{ n.label }}
          <span v-if="n.label === 'Human Gate' && lab.pendingGates.value.length" class="a-count a-count-need"><LabMark tone="needs" :size="10" />{{ lab.pendingGates.value.length }}</span>
        </NuxtLink>
      </nav>
      <h2 class="a-rail-h">專案</h2>
      <nav aria-label="專案">
        <NuxtLink v-for="p in projects" :key="p.id" :to="`/lab/a/p/${p.id}`" class="a-nav a-proj" :class="{ 'is-on': route.path === `/lab/a/p/${p.id}` }" :aria-current="route.path === `/lab/a/p/${p.id}` ? 'page' : undefined">
          <span class="a-proj-name">{{ p.name }}</span>
          <span v-if="needs(p.id)" class="a-count a-count-need" :aria-label="`${needs(p.id)} 個待決定`"><LabMark tone="needs" :size="10" />{{ needs(p.id) }}</span>
        </NuxtLink>
      </nav>
      <div class="a-rail-foot">
        <LabTheme />
        <NuxtLink to="/lab" class="a-switch">換一個設計方向</NuxtLink>
      </div>
    </aside>
    <main class="a-main" id="main">
      <p v-if="lab.error.value" class="a-error" role="alert">{{ lab.error.value }}</p>
      <slot />
    </main>
    <p v-if="lab.toast.value" class="a-toast" role="status" @click="lab.toast.value = null">{{ lab.toast.value }}</p>
  </div>
</template>
