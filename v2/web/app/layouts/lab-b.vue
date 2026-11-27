<script setup lang="ts">
import '~/assets/lab/tokens.css'
import '~/assets/lab/b.css'

useHead({ htmlAttrs: { 'data-lab': 'b' }, title: 'Draupnir 審查桌（方向 B）' })
const lab = useLab()
const route = useRoute()
onMounted(async () => { lab.initTheme(); await lab.load() })
const tabs = [
  { to: '/lab/b', label: '審查桌', exact: true },
  { to: '/lab/b/agents', label: 'Agent 執行' },
]
const active = (t: { to: string, exact?: boolean }) => (t.exact ? route.path === t.to || route.path.startsWith('/lab/b/gates') : route.path.startsWith(t.to))
const projectId = computed({
  get: () => (route.path.startsWith('/lab/b/p/') ? (route.params.id as string) : ''),
  set: (id: string) => { if (id) navigateTo(`/lab/b/p/${id}`) },
})
</script>

<template>
  <div class="b-app">
    <header class="b-bar">
      <NuxtLink to="/lab/b" class="b-brand" aria-label="Draupnir 審查桌首頁">
        <svg width="26" height="14" viewBox="0 0 26 14" aria-hidden="true">
          <path d="M1 7h8" stroke="currentColor" stroke-width="2" stroke-dasharray="3 2.5" /><path d="M11 7h8" stroke="var(--fix)" stroke-width="2" /><circle cx="21.5" cy="7" r="3.6" fill="var(--fix)" />
        </svg>
        <b>Draupnir</b>
      </NuxtLink>
      <nav class="b-tabs" aria-label="主要">
        <NuxtLink v-for="t in tabs" :key="t.to" :to="t.to" class="b-tab" :class="{ 'is-on': active(t) }" :aria-current="active(t) ? 'page' : undefined">{{ t.label }}</NuxtLink>
        <label class="b-tab b-select" :class="{ 'is-on': !!projectId }">
          <span class="lab-sr">選擇專案</span>
          <select v-model="projectId" class="b-proj" aria-label="選擇專案">
            <option value="" disabled>專案…</option>
            <option v-for="p in lab.data.value.projects" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
        </label>
      </nav>
      <div class="b-bar-r">
        <span v-if="lab.pendingGates.value.length" class="b-need"><LabMark tone="needs" :size="11" />{{ lab.pendingGates.value.length }} 個決定在等你</span>
        <LabTheme />
        <NuxtLink to="/lab" class="b-switch">換方向</NuxtLink>
      </div>
    </header>
    <main class="b-body" id="main">
      <p v-if="lab.error.value" class="b-error" role="alert">{{ lab.error.value }}</p>
      <slot />
    </main>
    <p v-if="lab.toast.value" class="b-toast" role="status" @click="lab.toast.value = null">{{ lab.toast.value }}</p>
  </div>
</template>
