<script setup lang="ts">
import type { WorkItem, WorkStatus } from '~/types/api'
import {
  boardColumns,
  dragDataType,
  parseWorkStatus,
  priorityLabels,
  severityLabels,
  workStatusLabels,
  workTypeLabels,
} from '~/utils/labels'

const props = withDefaults(defineProps<{
  items: WorkItem[]
  busyId?: string | null
}>(), {
  busyId: null,
})

const emit = defineEmits<{
  move: [item: WorkItem, status: WorkStatus]
}>()

const dragging = ref<string | null>(null)

function itemsIn(status: WorkStatus): WorkItem[] {
  return props.items.filter(item => item.status === status)
}

function onDragStart(event: DragEvent, item: WorkItem) {
  dragging.value = item.id
  if (event.dataTransfer) {
    event.dataTransfer.setData(dragDataType, item.id)
    event.dataTransfer.effectAllowed = 'move'
  }
}

function onDrop(event: DragEvent, status: WorkStatus) {
  const id = event.dataTransfer?.getData(dragDataType) || dragging.value
  dragging.value = null
  const item = props.items.find(candidate => candidate.id === id)
  if (item && item.status !== status) emit('move', item, status)
}

function onSelect(event: Event, item: WorkItem) {
  const select = event.target as HTMLSelectElement
  const status = parseWorkStatus(select.value)
  select.value = item.status
  if (status && status !== item.status) emit('move', item, status)
}
</script>

<template>
  <div class="grid gap-4 overflow-x-auto pb-2 md:grid-cols-3 xl:grid-cols-6" role="list" aria-label="工作項目看板">
    <section
      v-for="status in boardColumns"
      :key="status"
      class="min-w-[15rem] rounded-xl border border-line bg-surface-muted/60 p-3"
      :data-testid="`board-column-${status}`"
      :aria-labelledby="`board-title-${status}`"
      @dragover.prevent
      @drop="onDrop($event, status)"
    >
      <h3 :id="`board-title-${status}`" class="mb-2 flex items-center justify-between gap-2 text-sm font-bold text-ink">
        <span>{{ workStatusLabels[status].label }}</span>
        <span class="rounded-md bg-surface px-1.5 py-0.5 font-mono text-xs text-ink-subtle">{{ itemsIn(status).length }}</span>
      </h3>

      <ul v-if="itemsIn(status).length" class="space-y-2">
        <li
          v-for="item in itemsIn(status)"
          :key="item.id"
          :data-testid="`board-card-${item.code}`"
          class="rounded-lg border border-line bg-surface p-3"
          :class="{ 'opacity-60': busyId === item.id }"
          draggable="true"
          @dragstart="onDragStart($event, item)"
        >
          <p class="font-mono text-[11px] text-ink-subtle">{{ item.code }}</p>
          <p class="mt-0.5 text-sm font-semibold text-ink">{{ item.title }}</p>
          <p class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-ink-muted">
            <span>{{ workTypeLabels[item.type] }}</span>
            <span aria-hidden="true">·</span>
            <span>{{ priorityLabels[item.priority] }}</span>
          </p>
          <p v-if="item.type === 'Bug' && item.severity" class="mt-1 text-[11px]" :data-testid="`board-severity-${item.code}`">
            {{ severityLabels[item.severity].label }}
          </p>
          <label class="mt-2 block">
            <span class="text-[11px] font-medium text-ink-subtle">變更狀態</span>
            <select
              class="field-input mt-0.5 py-1 text-xs"
              :value="item.status"
              :disabled="busyId === item.id"
              :aria-label="`變更 ${item.code} 狀態`"
              @change="onSelect($event, item)"
            >
              <option v-for="option in boardColumns" :key="option" :value="option">
                {{ workStatusLabels[option].label }}
              </option>
            </select>
          </label>
        </li>
      </ul>
      <p v-else class="rounded-lg border border-dashed border-line px-3 py-4 text-center text-xs text-ink-subtle">
        沒有項目
      </p>
    </section>
  </div>
</template>
