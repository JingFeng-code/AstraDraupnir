<script setup lang="ts">
import '~/assets/lab/tokens.css'
import '~/assets/lab/d.css'
import { clock, newest } from '~/utils/labD'

useHead({ htmlAttrs: { 'data-lab': 'd' }, title: 'Draupnir（方向 D）' })
const lab = useLab()
const route = useRoute()
onMounted(async () => { lab.initTheme(); await lab.load() })

const gates = computed(() => lab.pendingGates.value.length)
const projectId = computed(() => (route.path.startsWith('/lab/d/p/') ? (route.params.id as string) : ''))
const project = computed(() => lab.byId.value.projects.get(projectId.value))
const nav = [
  { to: '/lab/d', label: '總覽', exact: true },
  { to: '/lab/d/review', label: '審查與決策', count: true },
  { to: '/lab/d/trace', label: '追溯' },
  { to: '/lab/d/agents', label: 'Agent' },
]
const on = (n: { to: string, exact?: boolean }) => (n.exact ? route.path === n.to : route.path.startsWith(n.to))
const v2 = [
  ['work-items', '工作項目與看板'], ['requirements', '需求'], ['sprints', 'Sprint 與 Milestone'], ['evidence', '證據'], ['gates', 'Human Gate'], ['agents', 'Agent 執行'], ['reports', '報告'],
]
const snapshotAt = computed(() => clock(newest(lab.data.value.runs.map(r => r.finishedAt ?? r.startedAt))))
</script>

<template>
  <div class="d-app">
    <aside class="d-side" aria-label="工作空間">
      <NuxtLink to="/lab/d" class="d-brand" aria-label="Draupnir 總覽">
        <svg class="d-brand-mark" width="30" height="30" viewBox="0 0 30 30" aria-hidden="true">
          <rect x="1" y="1" width="28" height="28" rx="9" fill="currentColor" />
          <path d="M6 18h5" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-dasharray="1 4" />
          <path d="M13 18h5" stroke="#fff" stroke-width="2.4" stroke-linecap="round" />
          <path d="m23 9.5 1.3 2.7 3 .4-2.2 2.1.5 3-2.6-1.4-2.6 1.4.5-3-2.2-2.1 3-.4z" fill="#fff" />
        </svg>
        Draupnir
      </NuxtLink>
      <nav class="d-nav" aria-label="主要">
        <NuxtLink v-for="n in nav" :key="n.to" :to="n.to" :class="{ 'is-on': on(n) }" :aria-current="on(n) ? 'page' : undefined">
          {{ n.label }}<span v-if="n.count && gates" class="d-count" :aria-label="`${gates} 個 Human Gate 待決定`">{{ gates }}</span>
        </NuxtLink>
      </nav>
      <div class="d-projects">
        <p class="d-nav-h">專案</p>
        <nav class="d-nav" aria-label="專案">
          <NuxtLink v-for="p in lab.data.value.projects" :key="p.id" :to="`/lab/d/p/${p.id}`" :class="{ 'is-on': projectId === p.id }" :aria-current="projectId === p.id ? 'page' : undefined">{{ p.name }}</NuxtLink>
        </nav>
      </div>
      <div class="d-side-foot">
        <LabTheme />
        <NuxtLink to="/lab" class="d-meta" style="text-decoration: underline; text-underline-offset: 3px">回到方向選擇</NuxtLink>
      </div>
    </aside>

    <div class="d-main">
      <header class="d-top">
        <nav class="d-crumb" aria-label="位置">
          <NuxtLink to="/lab/d">工作空間</NuxtLink>
          <template v-if="project"><span aria-hidden="true">/</span><span style="color: var(--ink); font-weight: 600">{{ project.name }}</span></template>
        </nav>
        <div class="d-top-r">
          <span class="d-source">資料快照 {{ snapshotAt }}</span>
          <NuxtLink v-if="project" :to="`/projects/${project.id}`" class="d-btn is-quiet">在 V2 開啟</NuxtLink>
        </div>
      </header>
      <p class="d-banner" role="note"><span class="d-tag is-test">設計環境</span>這裡的決定與狀態變更只寫入本機設計資料庫，不會送到 Supervisor，也不是正式決定。</p>
      <main id="main" class="d-content">
        <p v-if="lab.error.value" role="alert" style="color: var(--fault)">{{ lab.error.value }}</p>
        <slot />
        <nav v-if="project" class="d-sec" aria-label="V2 模組">
          <p class="d-meta" style="margin-bottom: 8px">既有 V2 模組（原功能，另開頁面）</p>
          <div style="display: flex; flex-wrap: wrap; gap: 8px">
            <NuxtLink v-for="[k, label] in v2" :key="k" :to="`/projects/${project.id}/${k}`" class="d-btn">{{ label }}</NuxtLink>
          </div>
        </nav>
      </main>
    </div>
    <p v-if="lab.toast.value" class="d-toast" role="status" @click="lab.toast.value = null">{{ lab.toast.value }}</p>
  </div>
</template>
