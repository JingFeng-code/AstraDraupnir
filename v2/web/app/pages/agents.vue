<script setup lang="ts">
import { ago, clean, isTestData, RUN_LABEL, runTone } from '~/lib/engineering'
import { agentName, clock, newest } from '~/lib/evidenceRules'

const ws = useWorkspace()
const who = ref('all')
const runs = computed(() => ws.data.value.runs
  .filter(r => who.value === 'all' || r.agent === who.value)
  .sort((a, b) => (b.finishedAt ?? b.startedAt ?? '').localeCompare(a.finishedAt ?? a.startedAt ?? '')))
const itemOf = (id: string | null) => (id ? ws.byId.value.items.get(id) : undefined)
const last = computed(() => clock(newest(ws.data.value.runs.map(r => r.finishedAt ?? r.startedAt))))
const sources = computed(() => [...new Set(ws.data.value.runs.map(r => r.source))].join('、'))
const unknownCount = computed(() => ws.data.value.runs.filter(r => r.agent === 'Unknown').length)
</script>

<template>
  <div>
    <h1 class="ui-h1">Agent</h1>
    <p class="ui-lead">每個 Agent 的執行紀錄與它做過的工作。</p>
    <div class="ui-callout" style="margin-top: 20px; max-width: 820px">
      <p class="ui-h3">這些是 {{ sources }} 的匯入紀錄，不是即時狀態</p>
      <p class="ui-meta">最後一筆紀錄：{{ last }}。Draupnir 目前沒有連到正在執行的 Agent，所以不顯示「正在做什麼」。標示「測試資料」的紀錄是模擬資料，不是真實的執行。</p>
      <p v-if="unknownCount" class="ui-meta">{{ unknownCount }} 筆紀錄的原始資料沒有寫明執行者，顯示為「未確認」，沒有依記憶補上。</p>
    </div>

    <section class="ui-panel" style="margin-top: 24px">
      <div class="ui-panel-h"><h2 class="ui-h2">執行紀錄 {{ runs.length }} 筆</h2>
        <label class="ui-meta" style="display: flex; gap: 10px; align-items: center">依 Agent 篩選
          <select v-model="who" class="ui-input"><option value="all">全部</option><option v-for="a in ws.agents.value" :key="a.name" :value="a.name">{{ agentName(a.name) }}</option></select></label>
      </div>
      <table class="ui-table">
        <thead><tr><th scope="col">Agent</th><th scope="col">工作</th><th scope="col">結果</th><th scope="col" class="ui-hide-sm">次數</th><th scope="col">紀錄時間</th><th scope="col" class="ui-hide-sm">來源</th></tr></thead>
        <tbody>
          <tr v-for="r in runs.slice(0, 40)" :key="r.id" @click="r.workItemId && navigateTo(`/work/${r.workItemId}`)">
            <td style="font-weight: 600">{{ agentName(r.agent) }}</td>
            <td class="ui-wide">{{ itemOf(r.workItemId) ? clean(itemOf(r.workItemId)!.title) : r.externalTaskId }} <span v-if="itemOf(r.workItemId) && isTestData(itemOf(r.workItemId)!.title)" class="ui-tag is-test">測試資料</span></td>
            <td><UiPill :tone="runTone(r.status)" :label="r.status === 'DONE' ? '已完成' : `紀錄停在「${RUN_LABEL[r.status] ?? r.status}」`" /></td>
            <td class="ui-hide-sm">{{ r.attempt }}</td>
            <td style="white-space: nowrap">{{ clock(r.finishedAt ?? r.startedAt) }}</td>
            <td class="ui-hide-sm"><span class="ui-tag">{{ r.source }} 匯入</span></td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>
