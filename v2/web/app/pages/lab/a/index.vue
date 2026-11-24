<script setup lang="ts">
import { clean, projectOf } from '~/utils/lab'

definePageMeta({ layout: 'lab-a' })
const lab = useLab()
const topNeeds = computed(() => lab.attention.value.slice(0, 4))
const more = computed(() => Math.max(0, lab.attention.value.length - topNeeds.value.length))
const counts = computed(() => {
  const items = lab.data.value.items
  return { projects: lab.data.value.projects.length, items: items.length, moving: items.filter(i => i.status === 'InProgress' || i.status === 'InReview').length }
})
const target = (a: { kind: string, itemId?: string }) => (a.kind === 'gate' ? '/lab/a/gates' : a.itemId ? `/lab/a/w/${a.itemId}` : '/lab/a')
</script>

<template>
  <div class="a-home">
    <div>
      <header class="a-head">
        <h1 class="a-h1">今天的交付</h1>
        <p class="a-sub">{{ counts.projects }} 個專案、{{ counts.items }} 個工作項目，現在有 {{ counts.moving }} 個在進行或審查中。</p>
      </header>

      <section v-if="lab.attention.value.length" class="a-need" aria-labelledby="need-h">
        <h2 id="need-h"><LabMark tone="needs" :size="14" />需要你，共 {{ lab.attention.value.length }} 項</h2>
        <ul>
          <li v-for="a in topNeeds" :key="a.id">
            <div>
              <p class="a-need-t">{{ clean(a.title) }}</p>
              <p class="a-need-m">{{ projectOf(lab.data.value.projects, a.projectId)?.name }}，{{ a.why }}</p>
            </div>
            <NuxtLink :to="target(a)" class="a-link">{{ a.kind === 'gate' ? '去決定' : '查看' }}</NuxtLink>
          </li>
        </ul>
        <p v-if="more" class="a-need-m" style="margin-top: 8px">還有 {{ more }} 項，<NuxtLink to="/lab/a/gates" class="a-link">到 Human Gate 頁看全部</NuxtLink></p>
      </section>

      <LabAStream :limit="40" />
    </div>

    <aside class="a-aside" aria-label="此刻">
      <section>
        <h2>Agent 此刻</h2>
        <ul>
          <li v-for="ag in lab.agents.value.slice(0, 6)" :key="ag.name" class="a-agent">
            <LabMark :tone="ag.live.length ? (ag.live.some(r => r.status === 'AGENT_LOST') ? 'fault' : 'moving') : 'idle'" />
            <div>
              <b>{{ ag.name }}</b>
              <span v-if="ag.live.length">{{ ag.live.map(r => r.externalTaskId).join('、') }}</span>
              <span v-else>閒置，已完成 {{ ag.done }} 筆</span>
            </div>
          </li>
        </ul>
        <NuxtLink to="/lab/a/agents" class="a-link" style="font-size: 14px">看每個 Agent 的紀錄</NuxtLink>
      </section>
      <section aria-labelledby="legend-h">
        <h2 id="legend-h">怎麼讀追溯線</h2>
        <p class="a-ev-m" style="margin: 0 0 8px">每個項目有五站：需求、工作、執行、證據、決定。</p>
        <div class="a-legend">
          <div><LabMark tone="proven" :size="12" />已確認，這一站站得住</div>
          <div><LabMark tone="moving" :size="12" />進行中</div>
          <div><LabMark tone="needs" :size="12" />在等你決定</div>
          <div><LabMark tone="fault" :size="12" />有問題</div>
          <div><LabMark tone="idle" dashed :size="12" />還沒有，虛線代表沒有東西可以依靠</div>
        </div>
      </section>
    </aside>
  </div>
</template>
