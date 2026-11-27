<script setup lang="ts">
import { ago, RUN_LABEL, runTone } from '~/utils/lab'

definePageMeta({ layout: 'lab-a' })
const lab = useLab()
const itemOf = (id: string | null) => (id ? lab.byId.value.items.get(id) : null)
</script>

<template>
  <div style="max-width: 760px">
    <header class="a-head">
      <h1 class="a-h1">Agent</h1>
      <p class="a-sub">誰現在在做什麼，以及每個 Agent 最近的執行。資料來自 Supervisor 與 Gleipnir 匯入的真實紀錄。</p>
    </header>
    <section v-for="ag in lab.agents.value" :key="ag.name" class="a-section" style="margin-top: 32px" :aria-labelledby="`ag-${ag.name}`">
      <h2 :id="`ag-${ag.name}`" class="a-h2" style="display: flex; gap: 10px; align-items: center">
        <LabMark :tone="ag.live.length ? (ag.live.some(r => r.status === 'AGENT_LOST') ? 'fault' : 'moving') : 'idle'" />{{ ag.name }}
        <span class="a-sub" style="margin: 0; font-weight: 400; font-size: 14px">{{ ag.live.length ? `進行中 ${ag.live.length} 筆` : '閒置' }}，共完成 {{ ag.done }} 筆<template v-if="ag.model">，模型 {{ ag.model }}</template></span>
      </h2>
      <ul class="a-log">
        <li v-for="r in ag.runs.slice(0, 7)" :key="r.id">
          <LabMark :tone="runTone(r.status)" :label="RUN_LABEL[r.status] ?? r.status" />
          <div>
            <p class="a-ev-t"><NuxtLink v-if="itemOf(r.workItemId)" :to="`/lab/a/w/${r.workItemId}`">{{ itemOf(r.workItemId)!.title }}</NuxtLink><template v-else>{{ r.externalTaskId }}</template></p>
            <p class="a-ev-m">{{ RUN_LABEL[r.status] ?? r.status }}，第 {{ r.attempt }} 次，{{ ago(r.finishedAt ?? r.startedAt) }}</p>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>
