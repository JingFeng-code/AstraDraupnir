<script setup lang="ts">
import type { Milestone, Paged, Sprint, SprintStatus, WorkItem } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDate } from '~/utils/format'
import { sprintStatusLabels } from '~/utils/labels'
import type { Tone } from '~/lib/engineering'

const route = useRoute()
const api = useApi()
const ws = useWorkspace()
const id = computed(() => String(route.params.id))
const sprints = ref<Sprint[]>([])
const milestones = ref<Milestone[]>([])
const items = ref<WorkItem[]>([])
const loading = ref(true)
const loadError = ref<ApiError | null>(null)
const editingSprint = ref<string | null>(null)
const editingMilestone = ref<string | null>(null)
const sprintOpen = ref(false)
const milestoneOpen = ref(false)
const saving = ref(false)
const statuses: SprintStatus[] = ['Planned', 'Active', 'Closed']
const sprintTone: Record<SprintStatus, Tone> = { Planned: 'idle', Active: 'moving', Closed: 'proven' }
const sprintForm = reactive({ name: '', startDate: '', endDate: '', goal: '', status: 'Planned' as SprintStatus })
const milestoneForm = reactive({ name: '', dueDate: '', status: 'Planned' })

const timeline = computed(() => [...sprints.value].sort((a, b) => a.startDate.localeCompare(b.startDate)))
const summary = computed(() => ({
  active: sprints.value.filter(s => s.status === 'Active').length,
  total: sprints.value.length,
  done: items.value.filter(i => i.status === 'Done').length,
  work: items.value.length,
  milestones: milestones.value.length,
}))
function progress(sprint: Sprint) {
  const linked = items.value.filter(i => i.sprintId === sprint.id)
  return { linked, done: linked.filter(i => i.status === 'Done').length, total: linked.length }
}
function resetSprint() {
  editingSprint.value = null
  sprintOpen.value = false
  Object.assign(sprintForm, { name: '', startDate: '', endDate: '', goal: '', status: 'Planned' })
}
function resetMilestone() {
  editingMilestone.value = null
  milestoneOpen.value = false
  Object.assign(milestoneForm, { name: '', dueDate: '', status: 'Planned' })
}
async function load() {
  loading.value = true
  loadError.value = null
  try {
    const [s, m, w] = await Promise.all([
      api.get<Sprint[]>(`/api/projects/${id.value}/sprints`),
      api.get<Milestone[]>(`/api/projects/${id.value}/milestones`),
      api.get<Paged<WorkItem>>(`/api/projects/${id.value}/work-items`, { pageSize: 100 }),
    ])
    sprints.value = s
    milestones.value = m
    items.value = w.items
  } catch (caught) {
    loadError.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}
function editSprint(sprint: Sprint) {
  editingSprint.value = sprint.id
  Object.assign(sprintForm, { name: sprint.name, startDate: sprint.startDate, endDate: sprint.endDate, goal: sprint.goal, status: sprint.status })
  sprintOpen.value = true
}
async function saveSprint() {
  if (saving.value) return
  saving.value = true
  loadError.value = null
  try {
    const body = { name: sprintForm.name.trim(), startDate: sprintForm.startDate, endDate: sprintForm.endDate, goal: sprintForm.goal.trim(), status: sprintForm.status }
    if (editingSprint.value) await api.put<Sprint>(`/api/projects/${id.value}/sprints/${editingSprint.value}`, body)
    else await api.post<Sprint>(`/api/projects/${id.value}/sprints`, body)
    ws.toast.value = editingSprint.value ? 'Sprint 已更新' : 'Sprint 已建立'
    resetSprint()
    await load()
  } catch (caught) {
    loadError.value = toApiError(caught)
  } finally {
    saving.value = false
  }
}
function editMilestone(milestone: Milestone) {
  editingMilestone.value = milestone.id
  Object.assign(milestoneForm, { name: milestone.name, dueDate: milestone.dueDate, status: milestone.status })
  milestoneOpen.value = true
}
async function saveMilestone() {
  if (saving.value) return
  saving.value = true
  loadError.value = null
  try {
    const body = { name: milestoneForm.name.trim(), dueDate: milestoneForm.dueDate, status: milestoneForm.status }
    if (editingMilestone.value) await api.put<Milestone>(`/api/projects/${id.value}/milestones/${editingMilestone.value}`, body)
    else await api.post<Milestone>(`/api/projects/${id.value}/milestones`, body)
    ws.toast.value = editingMilestone.value ? 'Milestone 已更新' : 'Milestone 已建立'
    resetMilestone()
    await load()
  } catch (caught) {
    loadError.value = toApiError(caught)
  } finally {
    saving.value = false
  }
}
function onAssign(sprintId: string, event: Event) {
  const select = event.target as HTMLSelectElement
  const item = items.value.find(candidate => candidate.id === select.value)
  select.value = ''
  if (item) void assign(item, sprintId)
}
async function assign(item: WorkItem, sprintId: string) {
  try {
    await api.put<WorkItem>(`/api/projects/${id.value}/work-items/${item.id}`, { ...item, sprintId: sprintId || null })
    ws.toast.value = sprintId ? `${item.code} 已排入 Sprint` : `${item.code} 已移出 Sprint`
    await load()
  } catch (caught) {
    loadError.value = toApiError(caught)
  }
}
watch(id, load, { immediate: true })
</script>

<template>
  <div>
    <h1 class="ui-h1">Sprint 與 Milestone</h1>
    <p class="ui-lead">把工作排進短週期，並用 Milestone 標記必須交付的時間點。同一專案同時只能有一個 Active Sprint。</p>

    <AppLoading v-if="loading" />
    <div v-else-if="loadError" class="ui-callout is-fault" style="margin-top: 20px">
      <p>{{ loadError.detail }}</p>
      <button type="button" class="ui-btn is-quiet" @click="load">重新載入</button>
    </div>
    <template v-else>
      <section class="ui-stats" aria-label="Sprint 摘要">
        <div class="ui-stat"><b>{{ summary.active }}</b><span>Active Sprint</span></div>
        <div class="ui-stat"><b>{{ summary.total }}</b><span>Sprint 總數</span></div>
        <div class="ui-stat"><b>{{ summary.done }} / {{ summary.work }}</b><span>工作項目完成</span></div>
      </section>

      <section class="ui-panel" style="margin-top: 24px" aria-labelledby="sprint-h">
        <div class="ui-panel-h">
          <div><h2 id="sprint-h" class="ui-h2">Sprint</h2><p class="ui-meta">依日期排序；點編輯可改狀態、日期與目標。</p></div>
          <button type="button" class="ui-btn is-primary" data-testid="new-sprint" @click="editingSprint = null; sprintOpen = !sprintOpen">{{ sprintOpen && !editingSprint ? '收起表單' : '新增 Sprint' }}</button>
        </div>
        <form v-if="sprintOpen" class="ui-panel-b ui-stack" data-testid="sprint-form" @submit.prevent="saveSprint">
          <div class="ui-grid-2" style="margin-top: 0; gap: 16px">
            <label class="ui-stack" style="gap: 6px"><span class="ui-meta">名稱</span><input v-model="sprintForm.name" required class="ui-input" data-testid="sprint-name"></label>
            <label class="ui-stack" style="gap: 6px"><span class="ui-meta">狀態</span><select v-model="sprintForm.status" class="ui-input"><option v-for="s in statuses" :key="s" :value="s">{{ sprintStatusLabels[s].label }}</option></select></label>
            <label class="ui-stack" style="gap: 6px"><span class="ui-meta">開始日期</span><input v-model="sprintForm.startDate" type="date" required class="ui-input" data-testid="sprint-start"></label>
            <label class="ui-stack" style="gap: 6px"><span class="ui-meta">結束日期</span><input v-model="sprintForm.endDate" type="date" required class="ui-input" data-testid="sprint-end"></label>
          </div>
          <label class="ui-stack" style="gap: 6px"><span class="ui-meta">目標</span><input v-model="sprintForm.goal" class="ui-input" data-testid="sprint-goal"></label>
          <div style="display: flex; gap: 8px; justify-content: flex-end"><button type="button" class="ui-btn is-quiet" @click="resetSprint">取消</button><button type="submit" class="ui-btn is-primary" :disabled="saving" data-testid="sprint-submit">{{ saving ? '儲存中…' : (editingSprint ? '儲存 Sprint' : '建立 Sprint') }}</button></div>
        </form>
        <AppEmpty v-if="!sprints.length" title="還沒有 Sprint" description="建立第一個 Sprint，讓工作有明確的短週期。"><button type="button" class="ui-btn is-primary" @click="sprintOpen = true">建立 Sprint</button></AppEmpty>
        <div v-else class="ui-panel-b" style="padding-top: 0">
          <article v-for="sprint in timeline" :key="sprint.id" class="ui-panel" style="margin-top: 14px; box-shadow: none; background: var(--c-sunken)" :data-testid="`sprint-${sprint.id}`">
            <div class="ui-panel-h" style="padding: 16px 18px 8px">
              <div><h3 class="ui-h3">{{ sprint.name }}</h3><p class="ui-meta">{{ formatDate(sprint.startDate) }} – {{ formatDate(sprint.endDate) }} · {{ sprint.goal || '沒有填寫目標' }}</p></div>
              <div style="display: flex; gap: 8px; align-items: center"><UiPill :tone="sprintTone[sprint.status]" :label="sprintStatusLabels[sprint.status].label" /><button type="button" class="ui-btn is-quiet" :data-testid="`sprint-edit-${sprint.id}`" @click="editSprint(sprint)">編輯</button></div>
            </div>
            <div class="ui-panel-b" style="padding-top: 8px">
              <div class="ui-bar" role="img" :aria-label="`${progress(sprint).done} / ${progress(sprint).total} 已完成`"><i :style="{ width: (progress(sprint).total ? progress(sprint).done / progress(sprint).total * 100 : 0) + '%', background: 'var(--c-fix)' }" /></div>
              <p class="ui-meta" style="margin-top: 6px">{{ progress(sprint).done }} / {{ progress(sprint).total }} 個工作完成</p>
              <div v-if="progress(sprint).linked.length" style="display: flex; flex-wrap: wrap; gap: 8px; margin-top: 12px">
                <span v-for="item in progress(sprint).linked" :key="item.id" class="ui-tag"><NuxtLink :to="`/work/${item.id}`">{{ item.code }}</NuxtLink><button type="button" class="ui-btn is-quiet" style="min-height: 22px; padding: 0 4px" :aria-label="`移除 ${item.code}`" @click="assign(item, '')">移除</button></span>
              </div>
              <div class="ui-meta" style="display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-top: 12px">
                <span style="flex: none">加入工作</span>
                <select class="ui-input" style="min-height: 34px; flex: 1 1 180px; min-width: 0; width: auto" :data-testid="`sprint-assign-${sprint.id}`" :aria-label="`把工作加入 ${sprint.name}`" @change="onAssign(sprint.id, $event)">
                  <option value="">選擇工作項目…</option><option v-for="item in items.filter(i => i.sprintId !== sprint.id)" :key="item.id" :value="item.id">{{ item.code }} {{ item.title }}</option>
                </select>
              </div>
            </div>
          </article>
        </div>
      </section>

      <section class="ui-panel" style="margin-top: 24px" aria-labelledby="milestone-h">
        <div class="ui-panel-h"><div><h2 id="milestone-h" class="ui-h2">Milestone</h2><p class="ui-meta">交付里程碑與截止日期；完成後仍保留紀錄。</p></div><button type="button" class="ui-btn" data-testid="new-milestone" @click="editingMilestone = null; milestoneOpen = !milestoneOpen">{{ milestoneOpen && !editingMilestone ? '收起表單' : '新增 Milestone' }}</button></div>
        <form v-if="milestoneOpen" class="ui-panel-b ui-stack" data-testid="milestone-form" @submit.prevent="saveMilestone">
          <div class="ui-grid-2" style="margin-top: 0; gap: 16px">
            <label class="ui-stack" style="gap: 6px"><span class="ui-meta">名稱</span><input v-model="milestoneForm.name" required class="ui-input" data-testid="milestone-name"></label>
            <label class="ui-stack" style="gap: 6px"><span class="ui-meta">截止日期</span><input v-model="milestoneForm.dueDate" type="date" required class="ui-input" data-testid="milestone-due"></label>
            <label class="ui-stack" style="gap: 6px"><span class="ui-meta">狀態</span><select v-model="milestoneForm.status" class="ui-input"><option>Planned</option><option>Active</option><option>Closed</option></select></label>
          </div>
          <div style="display: flex; gap: 8px; justify-content: flex-end"><button type="button" class="ui-btn is-quiet" @click="resetMilestone">取消</button><button type="submit" class="ui-btn is-primary" :disabled="saving" data-testid="milestone-submit">{{ saving ? '儲存中…' : (editingMilestone ? '儲存 Milestone' : '建立 Milestone') }}</button></div>
        </form>
        <AppEmpty v-if="!milestones.length" title="還沒有 Milestone" description="建立里程碑，標記下一個必須交付的時間點。" />
        <div v-else class="ui-panel-b" style="padding-top: 0">
          <article v-for="milestone in milestones" :key="milestone.id" class="ui-row" style="align-items: center"><UiMark tone="needs" :size="18" /><div style="flex: 1; min-width: 0"><p class="ui-row-t">{{ milestone.name }}</p><p class="ui-row-m">截止 {{ formatDate(milestone.dueDate) }} · {{ milestone.status }}</p></div><button type="button" class="ui-btn is-quiet" :data-testid="`milestone-edit-${milestone.id}`" @click="editMilestone(milestone)">編輯</button></article>
        </div>
      </section>
    </template>
  </div>
</template>
