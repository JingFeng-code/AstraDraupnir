<script setup lang="ts">
import { ago, clean, RUN_LABEL, runTone } from '~/utils/lab'

definePageMeta({ layout: 'lab-c' })
const lab = useLab()
const stamp = (r: { startedAt?: string | null, finishedAt?: string | null }) => r.startedAt ?? r.finishedAt ?? ''
const RECENT = 6
const recent = (runs: any[]) => [...runs].sort((x, y) => stamp(x).localeCompare(stamp(y))).slice(-RECENT)
const title = (r: { workItemId?: string | null, externalTaskId: string }) => (r.workItemId && lab.byId.value.items.get(r.workItemId) ? clean(lab.byId.value.items.get(r.workItemId)!.title) : r.externalTaskId)
</script>

<template>
  <div>
    <h1 class="c-h1">Agent 時間軸</h1>
    <p class="c-sub">每個 Agent 一條軌，執行依時間由舊到新排列。邊框代表狀態：藍色進行中，紅色有問題。</p>
    <div class="c-swim">
      <section v-for="a in lab.agents.value" :key="a.name" class="c-swim-row" :aria-label="a.name">
        <div class="c-swim-who">
          <b><LabMark :tone="a.live.some(r => r.status === 'AGENT_LOST') ? 'fault' : a.live.length ? 'moving' : 'idle'" />{{ a.name }}</b>
          <small>{{ a.live.length ? `進行中 ${a.live.length} 筆` : '閒置' }}，完成 {{ a.done }} 筆</small>
          <small v-if="a.runs.length > 6">另有 {{ a.runs.length - 6 }} 筆較早的執行</small>
          <small v-if="a.model">{{ a.model }}</small>
        </div>
        <ol class="c-swim-track">
          <li v-for="r in recent(a.runs)" :key="r.id">
            <NuxtLink :to="r.workItemId ? `/lab/c/w/${r.workItemId}` : '/lab/c/agents'" class="c-run" :class="{ 'is-fault': runTone(r.status) === 'fault', 'is-moving': runTone(r.status) === 'moving' }">
              <LabMark :tone="runTone(r.status)" :size="12" :label="RUN_LABEL[r.status] ?? r.status" />
              <span>{{ title(r) }}<small>{{ RUN_LABEL[r.status] ?? r.status }}，{{ ago(r.finishedAt ?? r.startedAt) }}</small></span>
            </NuxtLink>
          </li>
        </ol>
      </section>
    </div>
  </div>
</template>
