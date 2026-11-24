<script setup lang="ts">
import { STOP_STATE_LABEL } from '~/utils/lab'

definePageMeta({ layout: 'lab-d' })
const lab = useLab()
const scope = ref<'live' | 'all'>('live')
const items = computed(() => lab.data.value.items.filter(i => scope.value === 'all' || i.status !== 'Done')
  .sort((a, b) => Number(b.status === 'Blocked') - Number(a.status === 'Blocked') || a.code.localeCompare(b.code)))
const selId = ref('')
const sel = computed(() => items.value.find(i => i.id === selId.value))
const doneCount = computed(() => lab.data.value.items.filter(i => i.status === 'Done').length)
</script>

<template>
  <div>
    <h1 class="d-h1">追溯與品質</h1>
    <p class="d-lead">每一列是一件工作，每一欄是它在需求、工作、執行、證據、決定各站能證明什麼。實心代表站得住，虛線代表還沒有東西可以依靠。</p>
    <div style="display: flex; flex-wrap: wrap; gap: 8px 22px; margin-top: 16px" aria-label="圖例">
      <span v-for="k in (['proven', 'moving', 'needs', 'fault', 'missing'] as const)" :key="k" style="display: inline-flex; gap: 8px; align-items: center; color: var(--ink-2)"><LabMark :tone="k === 'missing' ? 'idle' : k" :dashed="k === 'missing'" :size="16" />{{ STOP_STATE_LABEL[k] }}</span>
    </div>
    <div style="display: flex; gap: 8px; margin-top: 18px" role="group" aria-label="顯示範圍">
      <button type="button" class="d-btn" :class="{ 'is-primary': scope === 'live' }" :aria-pressed="scope === 'live'" @click="scope = 'live'">未完成 {{ lab.data.value.items.length - doneCount }}</button>
      <button type="button" class="d-btn" :class="{ 'is-primary': scope === 'all' }" :aria-pressed="scope === 'all'" @click="scope = 'all'">全部 {{ lab.data.value.items.length }}</button>
    </div>
    <div class="d-split" :class="sel ? 'is-2r' : ''">
      <section class="d-panel" style="overflow-x: auto"><LabDMap v-model="selId" :items="items" show-project /></section>
      <aside v-if="sel" class="d-panel d-sticky" style="padding: 22px" aria-label="選取的工作項目">
        <LabDOps :item="sel" />
        <NuxtLink :to="`/lab/d/w/${sel.id}`" class="d-btn" style="margin-top: 16px; width: 100%">開啟完整追溯</NuxtLink>
      </aside>
    </div>
  </div>
</template>
