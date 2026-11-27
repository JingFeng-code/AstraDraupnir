<script setup lang="ts">
import { STATUS_LABEL, type Trace } from '~/utils/lab'
import type { WorkItem } from '~/types/api'

definePageMeta({ layout: 'lab-a' })
const lab = useLab()
const route = useRoute()
const project = computed(() => lab.byId.value.projects.get(route.params.id as string))
const items = computed(() => lab.data.value.items.filter(i => i.projectId === route.params.id))
const reqs = computed(() => lab.data.value.requirements.filter(r => r.projectId === route.params.id))
const gates = computed(() => lab.pendingGates.value.filter(g => g.projectId === route.params.id))
const counts = computed(() => (['Done', 'InReview', 'InProgress', 'Ready', 'Backlog', 'Blocked'] as const).map(s => [s, items.value.filter(i => i.status === s).length] as const).filter(([, n]) => n))
const itemsOf = (reqId: string) => items.value.filter(i => i.requirementId === reqId)
const REQ_LABEL = { Draft: '草稿', Approved: '已核定', Implemented: '已實作', Verified: '已驗證' } as const
const threadFor = (list: WorkItem[]): Trace | null => (list[0] ? lab.traceOf(list[0]) : null)
</script>

<template>
  <div v-if="project">
    <header class="a-head">
      <h1 class="a-h1">{{ project.name }}</h1>
      <p class="a-sub">{{ project.description }}</p>
      <p class="a-sentence" style="margin-top: 10px">
        共 <b>{{ items.length }}</b> 個工作項目：<template v-for="([s, n], i) in counts" :key="s">{{ i ? '，' : '' }}{{ STATUS_LABEL[s] }} <b>{{ n }}</b></template>。
      </p>
    </header>

    <section v-if="gates.length" class="a-need" style="max-width: 700px">
      <h2><LabMark tone="needs" :size="14" />這個專案有 {{ gates.length }} 個決定在等你</h2>
      <ul><li v-for="g in gates" :key="g.id"><p class="a-need-t">{{ g.title }}</p><NuxtLink to="/lab/a/gates" class="a-link">去決定</NuxtLink></li></ul>
    </section>

    <section class="a-section" aria-labelledby="req-h">
      <h2 id="req-h" class="a-h2">需求與它們的追溯</h2>
      <p v-if="!reqs.length" class="a-empty">這個專案還沒有需求。</p>
      <ul>
        <li v-for="r in reqs" :key="r.id" class="a-req">
          <span class="a-req-code">{{ r.code }}</span>
          <div>
            <p class="a-req-t">{{ r.title }}</p>
            <p class="a-req-m">{{ REQ_LABEL[r.status] }}<template v-if="itemsOf(r.id).length">，{{ itemsOf(r.id).length }} 個工作項目
              <template v-if="itemsOf(r.id)[0]">，例如 <NuxtLink :to="`/lab/a/w/${itemsOf(r.id)[0]!.id}`" class="a-link" style="font-weight: 500">{{ itemsOf(r.id)[0]!.title }}</NuxtLink></template></template>
            </p>
          </div>
          <LabTrace v-if="threadFor(itemsOf(r.id))" :trace="threadFor(itemsOf(r.id))!" compact />
        </li>
      </ul>
    </section>

    <section class="a-section" aria-labelledby="log-h">
      <h2 id="log-h" class="a-h2">這個專案的日誌</h2>
      <LabAStream :project-id="project.id" :limit="30" />
    </section>
  </div>
  <p v-else class="a-empty">{{ lab.data.value.ready ? '找不到這個專案。' : '讀取中…' }}</p>
</template>
