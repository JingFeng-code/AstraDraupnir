<script setup lang="ts">
import { clean, dayLabel, isTestData, projectOf, STATUS_TONE } from '~/lib/engineering'
import { agentName, clock, newest } from '~/lib/evidenceRules'

const ws = useWorkspace()
const d = computed(() => ws.data.value)
const gates = computed(() => ws.pendingGates.value)
const trouble = computed(() => ws.attention.value.filter(a => a.kind !== 'gate'))
const active = computed(() => d.value.items.filter(i => i.status === 'InProgress' || i.status === 'InReview').length)
const filter = ref<'all' | 'moving' | 'proven' | 'problem'>('all')
const FILTERS = [['all', '全部'], ['moving', '進行中'], ['proven', '已完成'], ['problem', '有問題']] as const
const more = ref(false)
const events = computed(() => ws.events.value.filter(e => filter.value === 'all' || (filter.value === 'problem' ? e.tone === 'fault' || e.tone === 'needs' : e.tone === filter.value)).slice(0, more.value ? 60 : 12))
const groups = computed(() => {
  const out: { day: string, rows: typeof events.value }[] = []
  for (const e of events.value) {
    const day = dayLabel(e.at)
    const last = out[out.length - 1]
    if (last && last.day === day) last.rows.push(e)
    else out.push({ day, rows: [e] })
  }
  return out
})
const lastRecord = computed(() => clock(newest(d.value.runs.map(r => r.finishedAt ?? r.startedAt))))
const traceFor = (id: string | null) => { const i = id ? ws.byId.value.items.get(id) : null; return i ? ws.traceOf(i) : null }
const progress = (pid: string) => {
  const its = d.value.items.filter(i => i.projectId === pid)
  return { done: its.filter(i => i.status === 'Done').length, total: its.length, blocked: its.filter(i => i.status === 'Blocked').length }
}
const agentRows = computed(() => ws.agents.value.map(a => ({ ...a, last: newest(a.runs.map(r => r.finishedAt ?? r.startedAt)) })))
</script>

<template>
  <div>
    <h1 class="ui-h1">工程工作空間</h1>
    <p class="ui-lead">{{ d.projects.length }} 個專案、{{ d.items.length }} 個工作項目。先看有什麼在等你，再看最近發生了什麼。</p>

    <section class="ui-panel ui-stats" aria-label="現況">
      <NuxtLink to="/review" class="ui-stat"><b>{{ gates.length }}</b><span>待你決定的 Human Gate</span></NuxtLink>
      <NuxtLink to="/review?tab=issues" class="ui-stat"><b>{{ trouble.length }}</b><span>需要處理：被擋住或 Agent 異常</span></NuxtLink>
      <div class="ui-stat"><b>{{ active }}</b><span>進行中與審查中的工作</span></div>
    </section>
    <p class="ui-meta" style="margin-top: 8px">待決定與需要處理是兩個不同的數字，不會相加。待決定只算 Human Gate；需要處理算被擋住的工作與異常的 Agent 執行。</p>

    <div class="ui-grid-2">
      <section class="ui-panel" aria-labelledby="log-h">
        <div class="ui-panel-h">
          <h2 id="log-h" class="ui-h2">活動日誌</h2>
          <span class="ui-source">Supervisor 匯入紀錄，最後一筆 {{ lastRecord }}</span>
        </div>
        <div style="display: flex; gap: 8px; padding: 0 22px 14px" role="group" aria-label="篩選">
          <button v-for="[k, t] in FILTERS" :key="k" type="button" class="ui-btn" :class="{ 'is-primary': filter === k }" :aria-pressed="filter === k" style="min-height: 36px; padding: 0 14px" @click="filter = k">{{ t }}</button>
        </div>
        <template v-for="g in groups" :key="g.day">
          <h3 class="ui-meta" style="padding: 8px 22px 4px; font-weight: 600">{{ g.day }}</h3>
          <NuxtLink v-for="e in g.rows" :key="e.id" :to="e.itemId ? `/work/${e.itemId}` : '/'" class="ui-row">
            <UiMark :tone="e.tone" :size="18" :label="e.verb" />
            <div style="min-width: 0; flex: 1">
              <p class="ui-row-t">{{ agentName(e.agent) }} {{ e.verb }} {{ clean(e.title) }} <span v-if="isTestData(e.title)" class="ui-tag is-test">測試資料</span></p>
              <p class="ui-row-m">{{ projectOf(d.projects, e.projectId)?.name ?? '未歸屬專案' }}，紀錄時間 {{ clock(e.at) }}，來源 Supervisor 匯入</p>
            </div>
            <UiThread v-if="traceFor(e.itemId)" :trace="traceFor(e.itemId)!" />
          </NuxtLink>
        </template>
        <p v-if="!groups.length" class="ui-empty">這個篩選條件下沒有事件。</p>
        <div v-else-if="!more" style="padding: 14px 22px 20px"><button type="button" class="ui-btn" @click="more = true">顯示更多</button></div>
      </section>

      <div class="ui-stack" style="gap: 24px">
        <section class="ui-panel" aria-labelledby="ag-h">
          <div class="ui-panel-h"><h2 id="ag-h" class="ui-h2">Agent</h2><NuxtLink to="/agents" class="ui-meta" style="text-decoration: underline; text-underline-offset: 3px">全部紀錄</NuxtLink></div>
          <p class="ui-meta" style="padding: 0 22px 10px">以下是匯入的歷史紀錄，不是即時連線狀態。</p>
          <ul>
            <li v-for="a in agentRows" :key="a.name" class="ui-row">
              <UiMark tone="idle" :dashed="a.name === 'Unknown'" :size="18" :label="a.name === 'Unknown' ? '執行者未確認' : '沒有即時連線'" />
              <div><p class="ui-row-t">{{ agentName(a.name) }}</p><p class="ui-row-m">{{ a.runs.length }} 筆紀錄，最後 {{ clock(a.last) }}</p></div>
            </li>
          </ul>
        </section>

        <section class="ui-panel" aria-labelledby="pj-h">
          <div class="ui-panel-h"><h2 id="pj-h" class="ui-h2">專案</h2></div>
          <ul>
            <li v-for="p in d.projects" :key="p.id">
              <NuxtLink :to="`/projects/${p.id}`" class="ui-row" style="align-items: center">
                <div style="flex: 1; min-width: 0">
                  <p class="ui-row-t">{{ p.name }}</p>
                  <div class="ui-bar" style="margin-top: 8px" role="img" :aria-label="`${progress(p.id).done} / ${progress(p.id).total} 已完成`"><i :style="{ width: (progress(p.id).total ? progress(p.id).done / progress(p.id).total * 100 : 0) + '%', background: 'var(--c-fix)' }" /><i :style="{ width: (progress(p.id).total ? progress(p.id).blocked / progress(p.id).total * 100 : 0) + '%', background: 'var(--c-fault)' }" /></div>
                </div>
                <span class="ui-meta" style="white-space: nowrap">{{ progress(p.id).done }} / {{ progress(p.id).total }}</span>
              </NuxtLink>
            </li>
          </ul>
        </section>
      </div>
    </div>
  </div>
</template>
