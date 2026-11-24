<script setup lang="ts">
import type { Milestone, Sprint, SprintStatus } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDate } from '~/utils/format'
import { sprintStatusLabels } from '~/utils/labels'

const route = useRoute()
const api = useApi()
const id = computed(() => String(route.params.id))

const sprints = ref<Sprint[]>([])
const milestones = ref<Milestone[]>([])
const loading = ref(true)
const loadError = ref<ApiError | null>(null)

const sprintOpen = ref(false)
const sprintSubmitting = ref(false)
const sprintError = ref<ApiError | null>(null)
const sprintForm = reactive({ name: '', startDate: '', endDate: '', goal: '', status: 'Planned' as SprintStatus })

const milestoneOpen = ref(false)
const milestoneSubmitting = ref(false)
const milestoneError = ref<ApiError | null>(null)
const milestoneForm = reactive({ name: '', dueDate: '', status: 'Planned' })

const sprintStatuses: SprintStatus[] = ['Planned', 'Active', 'Closed']

async function load() {
  loading.value = true
  loadError.value = null
  try {
    [sprints.value, milestones.value] = await Promise.all([
      api.get<Sprint[]>(`/api/projects/${id.value}/sprints`),
      api.get<Milestone[]>(`/api/projects/${id.value}/milestones`),
    ])
  } catch (caught) {
    loadError.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

async function createSprint() {
  if (sprintSubmitting.value) return
  sprintError.value = null
  sprintSubmitting.value = true
  try {
    await api.post<Sprint>(`/api/projects/${id.value}/sprints`, {
      name: sprintForm.name.trim(),
      startDate: sprintForm.startDate,
      endDate: sprintForm.endDate,
      goal: sprintForm.goal.trim(),
      status: sprintForm.status,
    })
    sprintForm.name = ''
    sprintForm.goal = ''
    sprintOpen.value = false
    await load()
  } catch (caught) {
    sprintError.value = toApiError(caught)
  } finally {
    sprintSubmitting.value = false
  }
}

async function createMilestone() {
  if (milestoneSubmitting.value) return
  milestoneError.value = null
  milestoneSubmitting.value = true
  try {
    await api.post<Milestone>(`/api/projects/${id.value}/milestones`, {
      name: milestoneForm.name.trim(),
      dueDate: milestoneForm.dueDate,
      status: milestoneForm.status,
    })
    milestoneForm.name = ''
    milestoneOpen.value = false
    await load()
  } catch (caught) {
    milestoneError.value = toApiError(caught)
  } finally {
    milestoneSubmitting.value = false
  }
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="site-container py-6">
    <AppLoading v-if="loading" />
    <AppNotice v-else-if="loadError" :message="loadError.detail" />
    <div v-else class="space-y-8">
      <section aria-labelledby="sprints-title">
        <div class="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 id="sprints-title" class="text-xl font-bold text-ink">Sprint</h2>
            <p class="text-sm text-ink-muted">同一專案同時最多一個 Active Sprint。</p>
          </div>
          <button type="button" class="btn btn-primary" data-testid="new-sprint" @click="sprintOpen = !sprintOpen">
            {{ sprintOpen ? '收起表單' : '新增 Sprint' }}
          </button>
        </div>

        <form v-if="sprintOpen" class="panel mt-4 space-y-4 p-5" data-testid="sprint-form" @submit.prevent="createSprint">
          <div class="grid gap-4 sm:grid-cols-4">
            <div class="sm:col-span-2">
              <label for="sprint-name" class="field-label">名稱</label>
              <input id="sprint-name" v-model="sprintForm.name" required class="field-input" data-testid="sprint-name">
            </div>
            <div>
              <label for="sprint-start" class="field-label">開始日期</label>
              <input id="sprint-start" v-model="sprintForm.startDate" type="date" required class="field-input" data-testid="sprint-start">
            </div>
            <div>
              <label for="sprint-end" class="field-label">結束日期</label>
              <input id="sprint-end" v-model="sprintForm.endDate" type="date" required class="field-input" data-testid="sprint-end">
            </div>
          </div>
          <div class="grid gap-4 sm:grid-cols-3">
            <div class="sm:col-span-2">
              <label for="sprint-goal" class="field-label">目標</label>
              <input id="sprint-goal" v-model="sprintForm.goal" class="field-input">
            </div>
            <div>
              <label for="sprint-status" class="field-label">狀態</label>
              <select id="sprint-status" v-model="sprintForm.status" class="field-input">
                <option v-for="status in sprintStatuses" :key="status" :value="status">{{ sprintStatusLabels[status].label }}</option>
              </select>
            </div>
          </div>
          <AppNotice v-if="sprintError" :message="sprintError.detail" :details="Object.values(sprintError.fields).flat()" />
          <div class="flex justify-end gap-2">
            <button type="button" class="btn btn-secondary" @click="sprintOpen = false">取消</button>
            <button type="submit" class="btn btn-primary" :disabled="sprintSubmitting" data-testid="sprint-submit">
              {{ sprintSubmitting ? '建立中…' : '建立 Sprint' }}
            </button>
          </div>
        </form>

        <AppEmpty v-if="!sprints.length" class="mt-4" title="還沒有 Sprint" description="建立 Sprint 後，工作項目就能排入衝刺。" />
        <ul v-else class="mt-4 grid gap-3 md:grid-cols-2">
          <li v-for="sprint in sprints" :key="sprint.id" class="panel p-4">
            <div class="flex items-center justify-between gap-2">
              <h3 class="text-base font-bold text-ink">{{ sprint.name }}</h3>
              <StatusBadge :value="sprintStatusLabels[sprint.status]" />
            </div>
            <p class="mt-1 text-xs text-ink-subtle">{{ formatDate(sprint.startDate) }} – {{ formatDate(sprint.endDate) }}</p>
            <p v-if="sprint.goal" class="mt-2 text-sm text-ink-muted">{{ sprint.goal }}</p>
          </li>
        </ul>
      </section>

      <section aria-labelledby="milestones-title">
        <div class="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 id="milestones-title" class="text-xl font-bold text-ink">Milestone</h2>
            <p class="text-sm text-ink-muted">交付里程碑與截止日期。</p>
          </div>
          <button type="button" class="btn btn-secondary" data-testid="new-milestone" @click="milestoneOpen = !milestoneOpen">
            {{ milestoneOpen ? '收起表單' : '新增 Milestone' }}
          </button>
        </div>

        <form v-if="milestoneOpen" class="panel mt-4 space-y-4 p-5" data-testid="milestone-form" @submit.prevent="createMilestone">
          <div class="grid gap-4 sm:grid-cols-3">
            <div>
              <label for="milestone-name" class="field-label">名稱</label>
              <input id="milestone-name" v-model="milestoneForm.name" required class="field-input" data-testid="milestone-name">
            </div>
            <div>
              <label for="milestone-due" class="field-label">截止日期</label>
              <input id="milestone-due" v-model="milestoneForm.dueDate" type="date" required class="field-input" data-testid="milestone-due">
            </div>
            <div>
              <label for="milestone-status" class="field-label">狀態</label>
              <input id="milestone-status" v-model="milestoneForm.status" class="field-input">
            </div>
          </div>
          <AppNotice v-if="milestoneError" :message="milestoneError.detail" :details="Object.values(milestoneError.fields).flat()" />
          <div class="flex justify-end gap-2">
            <button type="button" class="btn btn-secondary" @click="milestoneOpen = false">取消</button>
            <button type="submit" class="btn btn-primary" :disabled="milestoneSubmitting" data-testid="milestone-submit">
              {{ milestoneSubmitting ? '建立中…' : '建立 Milestone' }}
            </button>
          </div>
        </form>

        <AppEmpty v-if="!milestones.length" class="mt-4" title="還沒有 Milestone" description="建立里程碑後，可以把工作項目連到它。" />
        <ul v-else class="mt-4 grid gap-3 md:grid-cols-2">
          <li v-for="milestone in milestones" :key="milestone.id" class="panel p-4">
            <h3 class="text-base font-bold text-ink">{{ milestone.name }}</h3>
            <p class="mt-1 text-xs text-ink-subtle">截止 {{ formatDate(milestone.dueDate) }} · {{ milestone.status }}</p>
          </li>
        </ul>
      </section>
    </div>
  </div>
</template>
