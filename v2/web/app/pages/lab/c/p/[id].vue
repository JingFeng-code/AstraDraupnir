<script setup lang="ts">
import { STATUS_LABEL, STATUS_ORDER } from '~/utils/lab'

definePageMeta({ layout: 'lab-c' })
const lab = useLab()
const route = useRoute()
const project = computed(() => lab.byId.value.projects.get(route.params.id as string))
const items = computed(() => lab.data.value.items.filter(i => i.projectId === route.params.id))
const reqs = computed(() => lab.data.value.requirements.filter(r => r.projectId === route.params.id))
const selId = ref('')
const sel = computed(() => items.value.find(i => i.id === selId.value))
const covered = computed(() => reqs.value.filter(r => items.value.some(i => i.requirementId === r.id)).length)
const by = computed(() => [...STATUS_ORDER, 'Blocked' as const].map(s => ({ s, n: items.value.filter(i => i.status === s).length })).filter(x => x.n))
</script>

<template>
  <div v-if="project">
    <h1 class="c-h1">{{ project.name }}</h1>
    <p class="c-sub">{{ items.length }} 個工作項目，{{ reqs.length }} 條需求，其中 {{ covered }} 條有工作對應。{{ by.map(x => `${STATUS_LABEL[x.s]} ${x.n}`).join('，') }}。</p>
    <div class="c-layout" :class="{ 'no-drawer': !sel }">
      <LabCMap v-model="selId" :items="items" />
      <LabCDrawer v-if="sel" :item="sel" />
    </div>
  </div>
  <p v-else class="c-sub">{{ lab.data.value.ready ? '找不到這個專案。' : '讀取中…' }}</p>
</template>
