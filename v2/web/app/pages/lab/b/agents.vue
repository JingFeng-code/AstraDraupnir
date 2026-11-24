<script setup lang="ts">
import { ago, clean, RUN_LABEL, runTone, type Tone } from '~/utils/lab'
import type { AgentRun } from '~/types/api'

definePageMeta({ layout: 'lab-b' })
const lab = useLab()
const who = ref('all')
const runs = computed(() => lab.data.value.runs
  .filter(r => who.value === 'all' || r.agent === who.value)
  .sort((a, b) => (b.finishedAt ?? b.startedAt ?? '').localeCompare(a.finishedAt ?? a.startedAt ?? '')))
const selId = ref('')
watchEffect(() => { if (!runs.value.some(r => r.id === selId.value)) selId.value = runs.value[0]?.id ?? '' })
const sel = computed<AgentRun | undefined>(() => runs.value.find(r => r.id === selId.value))
const item = computed(() => (sel.value?.workItemId ? lab.byId.value.items.get(sel.value.workItemId) : undefined))
const trace = computed(() => (item.value ? lab.traceOf(item.value) : null))
const chip = (t: Tone) => ({ proven: 'is-fix', moving: 'is-move', needs: 'is-need', fault: 'is-fault', idle: '' })[t]
</script>

<template>
  <div class="b-split">
    <section class="b-pane" aria-labelledby="ar-h">
      <div class="b-pane-h"><h1 id="ar-h">Agent 執行</h1><span>{{ runs.length }} 筆</span></div>
      <div class="b-filters" role="group" aria-label="依 Agent 篩選">
        <button type="button" :aria-pressed="who === 'all'" @click="who = 'all'">全部</button>
        <button v-for="a in lab.agents.value" :key="a.name" type="button" :aria-pressed="who === a.name" @click="who = a.name">{{ a.name }}<template v-if="a.live.length"> ({{ a.live.length }} 進行中)</template></button>
      </div>
      <table class="b-table">
        <thead><tr><th scope="col">Agent</th><th scope="col">任務</th><th scope="col">結果</th><th scope="col">次數</th></tr></thead>
        <tbody>
          <tr v-for="r in runs" :key="r.id" data-row :class="{ 'is-on': r.id === selId }" :aria-selected="r.id === selId" @click="selId = r.id">
            <td><button type="button" class="lab-link" style="font-weight: 600" @click.stop="selId = r.id">{{ r.agent }}</button></td>
            <td>{{ r.workItemId && lab.byId.value.items.get(r.workItemId) ? clean(lab.byId.value.items.get(r.workItemId)!.title) : r.externalTaskId }}<small style="display: block; color: var(--ink-2)">{{ ago(r.finishedAt ?? r.startedAt) }}</small></td>
            <td><span class="b-chip" :class="chip(runTone(r.status))"><LabMark :tone="runTone(r.status)" :size="11" />{{ RUN_LABEL[r.status] ?? r.status }}</span></td>
            <td>{{ r.attempt }}</td>
          </tr>
        </tbody>
      </table>
    </section>
    <section class="b-pane" aria-labelledby="ad-h">
      <div class="b-pane-h"><h2 id="ad-h">這次執行的脈絡</h2></div>
      <div v-if="sel" class="b-pad">
        <h3 class="b-h1">{{ sel.agent }}，{{ RUN_LABEL[sel.status] ?? sel.status }}</h3>
        <p class="b-meta">{{ sel.externalTaskId }}<template v-if="sel.model">，模型 {{ sel.model }}</template>，第 {{ sel.attempt }} 次，{{ ago(sel.finishedAt ?? sel.startedAt) }}</p>
        <template v-if="item && trace">
          <p style="margin-top: 14px; font-weight: 600"><NuxtLink :to="`/lab/b/w/${item.id}`" class="lab-link">{{ clean(item.title) }}</NuxtLink></p>
          <LabBChain :trace="trace" />
        </template>
        <p v-else class="b-meta" style="margin-top: 14px">這次執行沒有對應的工作項目。</p>
      </div>
    </section>
  </div>
</template>
