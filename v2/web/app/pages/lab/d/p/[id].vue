<script setup lang="ts">
import { clean, isTestData, STATUS_LABEL, STATUS_ORDER, STATUS_TONE, trust } from '~/utils/lab'
import type { WorkStatus } from '~/types/api'

definePageMeta({ layout: 'lab-d' })
const lab = useLab()
const route = useRoute()
const project = computed(() => lab.byId.value.projects.get(route.params.id as string))
const items = computed(() => lab.data.value.items.filter(i => i.projectId === route.params.id))
const reqs = computed(() => lab.data.value.requirements.filter(r => r.projectId === route.params.id))
const gates = computed(() => lab.pendingGates.value.filter(g => g.projectId === route.params.id))
const tab = ref<'items' | 'reqs'>('items')
const filter = ref<'all' | WorkStatus>('all')
const shown = computed(() => items.value.filter(i => filter.value === 'all' || i.status === filter.value))
const selId = ref('')
watchEffect(() => { if (!shown.value.some(i => i.id === selId.value)) selId.value = shown.value[0]?.id ?? '' })
const sel = computed(() => shown.value.find(i => i.id === selId.value))
const counts = computed(() => Object.fromEntries([...STATUS_ORDER, 'Blocked'].map(s => [s, items.value.filter(i => i.status === s).length])))
const itemsOf = (rid: string) => items.value.filter(i => i.requirementId === rid)
const REQ: Record<string, string> = { Verified: '已驗證', Implemented: '已實作', Partial: '部分完成', Blocked: '被擋住', NotImplemented: '未實作' }
</script>

<template>
  <div v-if="project">
    <h1 class="d-h1">{{ project.name }}</h1>
    <p class="d-lead">{{ items.length }} 個工作項目，{{ reqs.length }} 條需求<template v-if="gates.length">，{{ gates.length }} 個 Human Gate 在等你決定</template>。</p>
    <div v-if="gates.length" class="d-callout is-need" style="margin-top: 18px; max-width: 820px">
      <p class="d-h3">待決定的 Human Gate</p>
      <NuxtLink v-for="g in gates" :key="g.id" to="/lab/d/review" style="text-decoration: underline; text-underline-offset: 3px">{{ clean(g.title) }}</NuxtLink>
    </div>

    <div class="d-tabs" role="tablist" aria-label="專案內容">
      <button type="button" role="tab" :aria-selected="tab === 'items'" @click="tab = 'items'">工作項目 {{ items.length }}</button>
      <button type="button" role="tab" :aria-selected="tab === 'reqs'" @click="tab = 'reqs'">需求追溯 {{ reqs.length }}</button>
    </div>

    <div v-if="tab === 'items'" class="d-split is-2r">
      <section class="d-panel">
        <div style="display: flex; flex-wrap: wrap; gap: 8px; padding: 18px 22px 8px" role="group" aria-label="依狀態篩選">
          <button type="button" class="d-btn" :class="{ 'is-primary': filter === 'all' }" :aria-pressed="filter === 'all'" style="min-height: 36px; padding: 0 14px" @click="filter = 'all'">全部 {{ items.length }}</button>
          <button v-for="s in ([...STATUS_ORDER, 'Blocked'] as WorkStatus[])" :key="s" type="button" class="d-btn" :class="{ 'is-primary': filter === s }" :aria-pressed="filter === s" :disabled="!counts[s]" style="min-height: 36px; padding: 0 14px" @click="filter = s">{{ STATUS_LABEL[s] }} {{ counts[s] }}</button>
        </div>
        <table class="d-table">
          <thead><tr><th scope="col">工作項目</th><th scope="col">狀態</th><th scope="col" class="d-hide-sm">追溯</th><th scope="col" class="d-hide-sm">負責</th></tr></thead>
          <tbody>
            <tr v-for="i in shown" :key="i.id" :class="{ 'is-on': i.id === selId }" :aria-selected="i.id === selId" @click="selId = i.id">
              <td><button type="button" style="text-align: left; font-weight: 600" @click.stop="selId = i.id">{{ clean(i.title) }}</button> <span v-if="isTestData(i.title)" class="d-tag is-test">測試資料</span><span class="d-meta" style="display: block">{{ i.code }}，信得過 {{ trust(lab.traceOf(i)).proven }} / {{ trust(lab.traceOf(i)).total }} 站</span></td>
              <td><LabDPill :tone="STATUS_TONE[i.status]" :label="STATUS_LABEL[i.status]" /></td>
              <td class="d-hide-sm"><LabDThread :trace="lab.traceOf(i)" /></td>
              <td class="d-hide-sm" style="white-space: nowrap">{{ i.assignee ?? '未指派' }}</td>
            </tr>
          </tbody>
        </table>
      </section>
      <aside v-if="sel" class="d-panel d-sticky" style="padding: 22px" aria-label="選取的工作項目">
        <LabDOps :item="sel" compact />
        <NuxtLink :to="`/lab/d/w/${sel.id}`" class="d-btn" style="margin-top: 16px; width: 100%">開啟完整追溯</NuxtLink>
      </aside>
    </div>

    <section v-else class="d-panel" style="margin-top: 24px">
      <ul>
        <li v-for="r in reqs" :key="r.id" class="d-row" style="align-items: center">
          <span class="d-meta" style="width: 70px">{{ r.code }}</span>
          <div style="flex: 1; min-width: 0"><p class="d-row-t">{{ r.title }}</p><p class="d-row-m">{{ REQ[r.status] ?? r.status }}<template v-if="itemsOf(r.id).length">，{{ itemsOf(r.id).length }} 個工作項目</template><template v-else>，沒有對應的工作項目</template></p></div>
          <LabDThread v-if="itemsOf(r.id)[0]" :trace="lab.traceOf(itemsOf(r.id)[0]!)" />
        </li>
      </ul>
    </section>
  </div>
  <p v-else class="d-lead">{{ lab.data.value.ready ? '找不到這個專案。' : '讀取中…' }}</p>
</template>
