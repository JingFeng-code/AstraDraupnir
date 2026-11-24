<script setup lang="ts">
import { clean, isTestData, SEVERITY_LABEL, STATUS_LABEL, TYPE_LABEL } from '~/lib/engineering'

const ws = useWorkspace()
const route = useRoute()
const item = computed(() => ws.byId.value.items.get(route.params.id as string))
const trace = computed(() => (item.value ? ws.traceOf(item.value) : null))
const project = computed(() => (item.value ? ws.byId.value.projects.get(item.value.projectId) : null))
</script>

<template>
  <div v-if="item && trace">
    <p class="ui-meta"><NuxtLink :to="`/projects/${item.projectId}`" style="text-decoration: underline; text-underline-offset: 3px">回到 {{ project?.name }}</NuxtLink>，{{ item.code }}</p>
    <h1 class="ui-h1" style="margin-top: 6px">{{ clean(item.title) }} <span v-if="isTestData(item.title)" class="ui-tag is-test" style="vertical-align: middle">測試資料</span></h1>
    <p class="ui-lead">{{ TYPE_LABEL[item.type] }}<template v-if="item.severity">，{{ SEVERITY_LABEL[item.severity] }}</template>，{{ STATUS_LABEL[item.status] }}<template v-if="item.assignee">，指派給 {{ item.assignee }}</template>。</p>
    <div class="ui-split is-2r">
      <section class="ui-panel" aria-labelledby="wc-h">
        <div class="ui-panel-h"><h2 id="wc-h" class="ui-h2">追溯鏈</h2><span class="ui-source">資料來自 {{ trace.runs[0]?.source ?? '設計資料庫' }} 匯入</span></div>
        <div class="ui-panel-b"><UiChain :trace="trace" /></div>
      </section>
      <aside class="ui-panel ui-sticky" style="padding: 22px" aria-label="操作"><UiOps :item="item" /></aside>
    </div>
  </div>
  <p v-else class="ui-lead">{{ ws.data.value.ready ? '找不到這個工作項目。' : '讀取中…' }}</p>
</template>
