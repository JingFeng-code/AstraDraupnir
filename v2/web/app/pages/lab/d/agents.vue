<script setup lang="ts">
import { ago, clean, isTestData, RUN_LABEL, runTone } from '~/utils/lab'
import { agentName, clock, newest } from '~/utils/labD'

definePageMeta({ layout: 'lab-d' })
const lab = useLab()
const who = ref('all')
const runs = computed(() => lab.data.value.runs
  .filter(r => who.value === 'all' || r.agent === who.value)
  .sort((a, b) => (b.finishedAt ?? b.startedAt ?? '').localeCompare(a.finishedAt ?? a.startedAt ?? '')))
const itemOf = (id: string | null) => (id ? lab.byId.value.items.get(id) : undefined)
const last = computed(() => clock(newest(lab.data.value.runs.map(r => r.finishedAt ?? r.startedAt))))
const sources = computed(() => [...new Set(lab.data.value.runs.map(r => r.source))].join('、'))
const unknownCount = computed(() => lab.data.value.runs.filter(r => r.agent === 'Unknown').length)
</script>

<template>
  <div>
    <h1 class="d-h1">Agent</h1>
    <p class="d-lead">每個 Agent 的執行紀錄與它做過的工作。</p>
    <div class="d-callout" style="margin-top: 20px; max-width: 820px">
      <p class="d-h3">這些是 {{ sources }} 的匯入紀錄，不是即時狀態</p>
      <p class="d-meta">最後一筆紀錄：{{ last }}。目前設計環境沒有連到正在執行的 Agent，所以不顯示「正在做什麼」。標示「測試資料」的進行中執行是為了讓畫面看得到各種狀態而安排的模擬。</p>
      <p v-if="unknownCount" class="d-meta">{{ unknownCount }} 筆紀錄的原始資料沒有寫明執行者，顯示為「未確認」，沒有依記憶補上。</p>
    </div>

    <section class="d-panel" style="margin-top: 24px">
      <div class="d-panel-h"><h2 class="d-h2">執行紀錄 {{ runs.length }} 筆</h2>
        <label class="d-meta" style="display: flex; gap: 10px; align-items: center">依 Agent 篩選
          <select v-model="who" class="d-input"><option value="all">全部</option><option v-for="a in lab.agents.value" :key="a.name" :value="a.name">{{ agentName(a.name) }}</option></select></label>
      </div>
      <table class="d-table">
        <thead><tr><th scope="col">Agent</th><th scope="col">工作</th><th scope="col">結果</th><th scope="col" class="d-hide-sm">次數</th><th scope="col">時間</th><th scope="col" class="d-hide-sm">來源</th></tr></thead>
        <tbody>
          <tr v-for="r in runs.slice(0, 40)" :key="r.id" @click="r.workItemId && navigateTo(`/lab/d/w/${r.workItemId}`)">
            <td style="font-weight: 600">{{ agentName(r.agent) }}</td>
            <td class="d-wide">{{ itemOf(r.workItemId) ? clean(itemOf(r.workItemId)!.title) : r.externalTaskId }} <span v-if="itemOf(r.workItemId) && isTestData(itemOf(r.workItemId)!.title)" class="d-tag is-test">測試資料</span></td>
            <td><LabDPill :tone="runTone(r.status)" :label="RUN_LABEL[r.status] ?? r.status" /></td>
            <td class="d-hide-sm">{{ r.attempt }}</td>
            <td style="white-space: nowrap">{{ ago(r.finishedAt ?? r.startedAt) }}</td>
            <td class="d-hide-sm"><span class="d-tag">{{ r.source }} 匯入</span></td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>
