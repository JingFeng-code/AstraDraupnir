<script setup lang="ts">
import { STOP_STATE_LABEL } from '~/lib/engineering'

const ws = useWorkspace()
const scope = ref<'live' | 'all'>('live')
const all = computed(() => ws.data.value.items.filter(i => scope.value === 'all' || i.status !== 'Done')
  .sort((a, b) => Number(b.status === 'Blocked') - Number(a.status === 'Blocked') || a.code.localeCompare(b.code)))
const showAll = ref(false)
const items = computed(() => (showAll.value ? all.value : all.value.slice(0, 12)))
const selId = ref('')
const sel = computed(() => items.value.find(i => i.id === selId.value))
const doneCount = computed(() => ws.data.value.items.filter(i => i.status === 'Done').length)
</script>

<template>
  <div>
    <h1 class="ui-h1">追溯與品質</h1>
    <p class="ui-lead">每一列是一件工作，每一欄是它在需求、工作、執行、證據、決定各站能證明什麼。實心代表站得住，虛線代表還沒有東西可以依靠。</p>
    <div style="display: flex; flex-wrap: wrap; gap: 8px 22px; margin-top: 16px" aria-label="圖例">
      <span v-for="k in (['proven', 'moving', 'needs', 'fault', 'missing'] as const)" :key="k" style="display: inline-flex; gap: 8px; align-items: center; color: var(--c-ink-2)"><UiMark :tone="k === 'missing' ? 'idle' : k" :dashed="k === 'missing'" :size="16" />{{ STOP_STATE_LABEL[k] }}</span>
    </div>
    <div style="display: flex; gap: 8px; margin-top: 18px" role="group" aria-label="顯示範圍">
      <button type="button" class="ui-btn" :class="{ 'is-primary': scope === 'live' }" :aria-pressed="scope === 'live'" @click="scope = 'live'">未完成 {{ ws.data.value.items.length - doneCount }}</button>
      <button type="button" class="ui-btn" :class="{ 'is-primary': scope === 'all' }" :aria-pressed="scope === 'all'" @click="scope = 'all'">全部 {{ ws.data.value.items.length }}</button>
    </div>
    <div class="ui-split" :class="sel ? 'is-2r' : ''">
      <section class="ui-panel" style="overflow-x: auto"><UiMap v-model="selId" :items="items" show-project />
        <div v-if="all.length > items.length" style="padding: 14px 18px"><button type="button" class="ui-btn" @click="showAll = true">顯示其餘 {{ all.length - items.length }} 項</button></div></section>
      <aside v-if="sel" class="ui-panel ui-sticky" style="padding: 22px" aria-label="選取的工作項目">
        <UiOps :item="sel" />
        <NuxtLink :to="`/work/${sel.id}`" class="ui-btn" style="margin-top: 16px; width: 100%">開啟完整追溯</NuxtLink>
      </aside>
    </div>
  </div>
</template>
