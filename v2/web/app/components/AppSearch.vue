<script setup lang="ts">
import { clean } from '~/lib/engineering'

/** Quick jump (Ctrl+K): projects, work items, pages. Real navigation, no placeholders. */
const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()
const ws = useWorkspace()
const q = ref('')
const idx = ref(0)
const input = ref<HTMLInputElement | null>(null)
const dlg = ref<HTMLDialogElement | null>(null)

const pages = [
  { label: '總覽', to: '/' }, { label: '審查與決策', to: '/review' }, { label: '追溯與品質', to: '/trace' }, { label: 'Agent', to: '/agents' },
  { label: '專案', to: '/projects' }, { label: '使用者與權限', to: '/users' }, { label: '稽核紀錄', to: '/audit' }, { label: '系統設定', to: '/settings' },
]
const results = computed(() => {
  const s = q.value.trim().toLowerCase()
  const match = (t: string) => !s || t.toLowerCase().includes(s)
  const out: { kind: string, label: string, hint: string, to: string }[] = []
  for (const p of pages) if (match(p.label)) out.push({ kind: '頁面', label: p.label, hint: '', to: p.to })
  for (const p of ws.data.value.projects) if (match(p.name) || match(p.key)) out.push({ kind: '專案', label: p.name, hint: p.key, to: `/projects/${p.id}` })
  if (s) for (const i of ws.data.value.items) if (match(i.title) || match(i.code)) out.push({ kind: '工作項目', label: clean(i.title), hint: i.code, to: `/work/${i.id}` })
  return out.slice(0, 30)
})
watch(() => props.open, async (v) => {
  if (v) { q.value = ''; idx.value = 0; dlg.value?.showModal(); await nextTick(); input.value?.focus() }
  else dlg.value?.close()
})
watch(q, () => { idx.value = 0 })
function go(r: { to: string } | undefined) { if (r) { emit('close'); navigateTo(r.to) } }
function onKey(e: KeyboardEvent) {
  if (e.key === 'ArrowDown') { e.preventDefault(); idx.value = Math.min(idx.value + 1, results.value.length - 1) }
  else if (e.key === 'ArrowUp') { e.preventDefault(); idx.value = Math.max(idx.value - 1, 0) }
  else if (e.key === 'Enter') { e.preventDefault(); go(results.value[idx.value]) }
}
</script>

<template>
  <dialog ref="dlg" class="ui-dialog" aria-label="快速跳轉" @close="emit('close')" @keydown="onKey">
    <div class="ui-dlg">
      <label class="sr-only" for="app-search-input">搜尋專案、工作項目或頁面</label>
      <input id="app-search-input" ref="input" v-model="q" class="ui-input" style="width: 100%" placeholder="搜尋專案、工作項目編號或標題，或輸入頁面名稱" autocomplete="off" role="combobox" aria-expanded="true" aria-controls="app-search-list">
      <ul id="app-search-list" role="listbox" class="ui-results">
        <li v-for="(r, i) in results" :key="r.kind + r.to" role="option" :aria-selected="i === idx">
          <button type="button" class="ui-row" :class="{ 'is-on': i === idx }" @click="go(r)" @mousemove="idx = i">
            <span class="ui-tag">{{ r.kind }}</span><span class="ui-row-t" style="flex: 1">{{ r.label }}</span><span class="ui-meta">{{ r.hint }}</span>
          </button>
        </li>
        <li v-if="!results.length" class="ui-empty">找不到符合的項目。</li>
      </ul>
      <p class="ui-meta">↑↓ 選擇，Enter 開啟，Esc 關閉</p>
    </div>
  </dialog>
</template>

<style>
dialog.ui-dialog { margin: 10vh auto auto; border: 0; border-radius: var(--radius-2); background: var(--c-surface); color: var(--c-ink); padding: 0; width: min(640px, calc(100vw - 32px)); box-shadow: 0 0 0 1px var(--c-line-strong), 0 24px 60px rgb(0 0 0 / 35%); }
dialog.ui-dialog::backdrop { background: rgb(8 8 10 / 55%); }
.ui-dlg { padding: 18px; display: grid; gap: 12px; }
.ui-results { max-height: 50vh; overflow-y: auto; }
.ui-results .ui-row { padding: 10px 12px; min-height: 48px; align-items: center; border-radius: var(--radius-1); border-top: 0; }
</style>
