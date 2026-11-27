<script setup lang="ts">
import { clean, isTestData, STATUS_LABEL, STATUS_ORDER, STATUS_TONE, trust, type Tone } from '~/utils/lab'
import type { WorkStatus } from '~/types/api'

definePageMeta({ layout: 'lab-b' })
const lab = useLab()
const route = useRoute()
const project = computed(() => lab.byId.value.projects.get(route.params.id as string))
const items = computed(() => lab.data.value.items.filter(i => i.projectId === route.params.id))
const filter = ref<'all' | WorkStatus>('all')
const shown = computed(() => items.value.filter(i => filter.value === 'all' || i.status === filter.value))
const selId = ref('')
watchEffect(() => { if (!shown.value.some(i => i.id === selId.value)) selId.value = shown.value[0]?.id ?? '' })
const sel = computed(() => shown.value.find(i => i.id === selId.value))
const trace = computed(() => (sel.value ? lab.traceOf(sel.value) : null))
const counts = computed(() => Object.fromEntries([...STATUS_ORDER, 'Blocked'].map(s => [s, items.value.filter(i => i.status === s).length])))
const chipClass = (t: Tone) => ({ proven: 'is-fix', moving: 'is-move', needs: 'is-need', fault: 'is-fault', idle: '' })[t]
const reqs = computed(() => lab.data.value.requirements.filter(r => r.projectId === route.params.id))
</script>

<template>
  <div v-if="project" class="b-brief">
    <section class="b-pane" aria-labelledby="pi-h">
      <div class="b-pane-h"><h1 id="pi-h">{{ project.name }}</h1><span>{{ items.length }} 個工作項目，{{ reqs.length }} 條需求</span></div>
      <div class="b-filters" role="group" aria-label="依狀態篩選">
        <button type="button" :aria-pressed="filter === 'all'" @click="filter = 'all'">全部 {{ items.length }}</button>
        <button v-for="s in [...STATUS_ORDER, 'Blocked'] as WorkStatus[]" :key="s" type="button" :aria-pressed="filter === s" :disabled="!counts[s]" @click="filter = s">{{ STATUS_LABEL[s] }} {{ counts[s] }}</button>
      </div>
      <table class="b-table">
        <thead><tr><th scope="col">項目</th><th scope="col">狀態</th><th scope="col">可信度</th><th scope="col">負責</th></tr></thead>
        <tbody>
          <tr v-for="i in shown" :key="i.id" data-row :class="{ 'is-on': i.id === selId }" :aria-selected="i.id === selId" @click="selId = i.id">
            <td>
              <button type="button" class="lab-link" style="text-align: left; font-weight: 600" @click.stop="selId = i.id">{{ clean(i.title) }}</button>
              <span v-if="isTestData(i.title)" class="b-chip" style="margin-left: 6px">測試資料</span>
              <small style="display: block; color: var(--ink-2)">{{ i.code }}</small>
            </td>
            <td><span class="b-chip" :class="chipClass(STATUS_TONE[i.status])"><LabMark :tone="STATUS_TONE[i.status]" :size="11" />{{ STATUS_LABEL[i.status] }}</span></td>
            <td>{{ trust(lab.traceOf(i)).proven }} / {{ trust(lab.traceOf(i)).total }} 站確認</td>
            <td>{{ i.assignee ?? '未指派' }}</td>
          </tr>
        </tbody>
      </table>
      <p v-if="!shown.length" class="b-empty">這個狀態沒有項目。</p>
    </section>
    <aside class="b-pane" aria-labelledby="po-h">
      <div class="b-pane-h"><h2 id="po-h">{{ sel ? sel.code : '選一個項目' }}</h2><NuxtLink v-if="sel" :to="`/lab/b/w/${sel.id}`" class="lab-link" style="font-size: 12px">完整頁面</NuxtLink></div>
      <template v-if="sel && trace">
        <div class="b-sec"><p style="font-weight: 700">{{ clean(sel.title) }}</p></div>
        <LabBOps :item="sel" />
        <div class="b-sec"><h3 class="b-side-h">證據鏈</h3><LabBChain :trace="trace" /></div>
      </template>
    </aside>
  </div>
  <p v-else class="b-empty">{{ lab.data.value.ready ? '找不到這個專案。' : '讀取中…' }}</p>
</template>
