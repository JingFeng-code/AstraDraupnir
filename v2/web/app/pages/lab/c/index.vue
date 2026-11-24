<script setup lang="ts">
import { STOP_STATE_LABEL } from '~/utils/lab'

definePageMeta({ layout: 'lab-c' })
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
    <h1 class="c-h1">交付結構圖</h1>
    <p class="c-sub">每一列是一件工作，每一欄是它在需求、工作、執行、證據、決定各站能證明什麼。實心代表站得住，虛線代表還沒有東西可以依靠。點一列看細節與操作。</p>
    <div class="c-legend" aria-label="圖例">
      <span v-for="k in (['proven', 'moving', 'needs', 'fault', 'missing'] as const)" :key="k"><LabMark :tone="k === 'missing' ? 'idle' : k" :dashed="k === 'missing'" :size="13" />{{ STOP_STATE_LABEL[k] }}</span>
    </div>
    <div style="margin-top: 16px; display: flex; gap: 8px" role="group" aria-label="顯示範圍">
      <button type="button" class="lab-btn" :class="{ 'is-primary': scope === 'live' }" :aria-pressed="scope === 'live'" @click="scope = 'live'">進行中與有問題 {{ lab.data.value.items.length - doneCount }}</button>
      <button type="button" class="lab-btn" :class="{ 'is-primary': scope === 'all' }" :aria-pressed="scope === 'all'" @click="scope = 'all'">全部含已完成 {{ lab.data.value.items.length }}</button>
    </div>
    <div class="c-layout" :class="{ 'no-drawer': !sel }">
      <LabCMap v-model="selId" :items="items" show-project />
      <LabCDrawer v-if="sel" :item="sel" />
    </div>
  </div>
</template>
