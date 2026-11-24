<script setup lang="ts">
import '~/assets/lab/tokens.css'
import '~/assets/lab/c.css'

useHead({ htmlAttrs: { 'data-lab': 'c' }, title: 'Draupnir 結構圖（方向 C）' })
const lab = useLab()
const route = useRoute()
onMounted(async () => { lab.initTheme(); await lab.load() })
const nav = [
  { to: '/lab/c', label: '結構圖', on: () => route.path === '/lab/c' || route.path.startsWith('/lab/c/w/') },
  { to: '/lab/c/agents', label: 'Agent 時間軸', on: () => route.path.startsWith('/lab/c/agents') },
  { to: '/lab/c/gates', label: 'Human Gate', on: () => route.path.startsWith('/lab/c/gates') },
]
const projectId = computed({
  get: () => (route.path.startsWith('/lab/c/p/') ? (route.params.id as string) : ''),
  set: (id: string) => { if (id) navigateTo(`/lab/c/p/${id}`) },
})
</script>

<template>
  <div class="c-app">
    <header class="c-bar">
      <NuxtLink to="/lab/c" class="c-brand" aria-label="Draupnir 結構圖首頁">
        <svg width="28" height="14" viewBox="0 0 28 14" aria-hidden="true"><path d="M1 7h9" stroke="currentColor" stroke-width="2" stroke-dasharray="3 2.5" /><path d="M12 7h9" stroke="var(--fix)" stroke-width="2" /><circle cx="23.5" cy="7" r="3.6" fill="var(--fix)" /></svg>
        Draupnir
      </NuxtLink>
      <nav class="c-nav" aria-label="主要">
        <NuxtLink v-for="n in nav" :key="n.to" :to="n.to" :class="{ 'is-on': n.on() }" :aria-current="n.on() ? 'page' : undefined">{{ n.label }}</NuxtLink>
      </nav>
      <select v-model="projectId" class="c-proj" aria-label="選擇專案">
        <option value="" disabled>專案…</option>
        <option v-for="p in lab.data.value.projects" :key="p.id" :value="p.id">{{ p.name }}</option>
      </select>
      <div class="c-bar-r">
        <span v-if="lab.pendingGates.value.length" class="c-need"><LabMark tone="needs" :size="11" />{{ lab.pendingGates.value.length }} 個決定在等你</span>
        <LabTheme />
        <NuxtLink to="/lab" class="c-switch">換方向</NuxtLink>
      </div>
    </header>
    <main id="main" class="c-main">
      <p v-if="lab.error.value" role="alert" style="color: var(--fault)">{{ lab.error.value }}</p>
      <slot />
    </main>
    <p v-if="lab.toast.value" class="c-toast" role="status" @click="lab.toast.value = null">{{ lab.toast.value }}</p>
  </div>
</template>
