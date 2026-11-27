<script setup lang="ts">
import { clean, isTestData, SEVERITY_LABEL, STATUS_LABEL, STATUS_TONE, TYPE_LABEL } from '~/utils/lab'

definePageMeta({ layout: 'lab-b' })
const lab = useLab()
const route = useRoute()
const item = computed(() => lab.byId.value.items.get(route.params.id as string))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const project = computed(() => (item.value ? lab.byId.value.projects.get(item.value.projectId) : null))
const needs = computed(() => lab.dependenciesOf(item.value?.id ?? ''))
const waiting = computed(() => lab.dependentsOf(item.value?.id ?? ''))
</script>

<template>
  <div v-if="item && trace" class="b-brief">
    <section class="b-pane" aria-labelledby="wi-h">
      <div class="b-pane-h"><h1 id="wi-h">{{ item.code }}</h1><NuxtLink :to="`/lab/b/p/${item.projectId}`" class="lab-link" style="font-size: 12px">回到 {{ project?.name }}</NuxtLink></div>
      <div class="b-pad">
        <h2 class="b-h1">{{ clean(item.title) }}<span v-if="isTestData(item.title)" class="b-chip" style="margin-left: 8px; vertical-align: middle">測試資料</span></h2>
        <p class="b-meta">{{ TYPE_LABEL[item.type] }}<template v-if="item.severity">，{{ SEVERITY_LABEL[item.severity] }}</template>，{{ STATUS_LABEL[item.status] }}<template v-if="item.assignee">，由 {{ item.assignee }} 負責</template></p>
        <LabBChain :trace="trace" />
        <h3 class="b-side-h" style="margin-top: 8px">相依</h3>
        <dl style="display: grid; grid-template-columns: 90px minmax(0, 1fr); gap: 6px 12px">
          <dt class="lab-muted">要先完成</dt>
          <dd><span v-if="!needs.length" class="lab-muted">沒有先決條件</span><ul><li v-for="d in needs" :key="d.id" style="display: flex; gap: 8px; align-items: center"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/b/w/${d.id}`" class="lab-link">{{ clean(d.title) }}</NuxtLink></li></ul></dd>
          <dt class="lab-muted">正在等它</dt>
          <dd><span v-if="!waiting.length" class="lab-muted">沒有項目在等它</span><ul><li v-for="d in waiting" :key="d.id" style="display: flex; gap: 8px; align-items: center"><LabMark :tone="STATUS_TONE[d.status]" :size="12" /><NuxtLink :to="`/lab/b/w/${d.id}`" class="lab-link">{{ clean(d.title) }}</NuxtLink></li></ul></dd>
        </dl>
      </div>
    </section>
    <aside class="b-pane" aria-labelledby="wo-h">
      <div class="b-pane-h"><h2 id="wo-h">操作</h2></div>
      <LabBOps :item="item" />
    </aside>
  </div>
  <p v-else class="b-empty">{{ lab.data.value.ready ? '找不到這個工作項目。' : '讀取中…' }}</p>
</template>
