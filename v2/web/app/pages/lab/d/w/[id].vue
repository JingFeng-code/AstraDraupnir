<script setup lang="ts">
import { clean, isTestData, SEVERITY_LABEL, STATUS_LABEL, TYPE_LABEL } from '~/utils/lab'

definePageMeta({ layout: 'lab-d' })
const lab = useLab()
const route = useRoute()
const item = computed(() => lab.byId.value.items.get(route.params.id as string))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const project = computed(() => (item.value ? lab.byId.value.projects.get(item.value.projectId) : null))
</script>

<template>
  <div v-if="item && trace">
    <p class="d-meta"><NuxtLink :to="`/lab/d/p/${item.projectId}`" style="text-decoration: underline; text-underline-offset: 3px">回到 {{ project?.name }}</NuxtLink>，{{ item.code }}</p>
    <h1 class="d-h1" style="margin-top: 6px">{{ clean(item.title) }} <span v-if="isTestData(item.title)" class="d-tag is-test" style="vertical-align: middle">測試資料</span></h1>
    <p class="d-lead">{{ TYPE_LABEL[item.type] }}<template v-if="item.severity">，{{ SEVERITY_LABEL[item.severity] }}</template>，{{ STATUS_LABEL[item.status] }}<template v-if="item.assignee">，指派給 {{ item.assignee }}</template>。</p>
    <div class="d-split is-2r">
      <section class="d-panel" aria-labelledby="wc-h">
        <div class="d-panel-h"><h2 id="wc-h" class="d-h2">追溯鏈</h2><span class="d-source">資料來自 {{ trace.runs[0]?.source ?? '設計資料庫' }} 匯入</span></div>
        <div class="d-panel-b"><LabDChain :trace="trace" /></div>
      </section>
      <aside class="d-panel d-sticky" style="padding: 22px" aria-label="操作"><LabDOps :item="item" /></aside>
    </div>
  </div>
  <p v-else class="d-lead">{{ lab.data.value.ready ? '找不到這個工作項目。' : '讀取中…' }}</p>
</template>
