<script setup lang="ts">
definePageMeta({ layout: 'lab-choose' })
const lab = useLab()
onMounted(async () => { lab.initTheme(); await lab.load() })
const d = computed(() => lab.data.value)
const DIRS = [
  { to: '/lab/a', name: '日誌', letter: 'A', idea: '以時間為主。一條交付日誌，最上面是需要你的事，往下是依日期排列的發生過的事。', best: '想讀完整個故事、一天結束時回顧。', risk: '一次掃過很多專案會比較慢。' },
  { to: '/lab/b', name: '審查桌', letter: 'B', idea: '以決定為主。左邊是等你處理的佇列，中間是證據與追溯，右邊是決定與它的後果。', best: '快速清掉待決定事項。', risk: '不容易看到整體全貌。' },
  { to: '/lab/c', name: '結構圖', letter: 'C', idea: '以追溯為主。需求、工作、執行、證據、決定排成五條軌，選一個項目就亮出它的整條鏈。', best: '弄清楚某件事為什麼卡住。', risk: '小螢幕上資訊較密。' },
]
</script>

<template>
  <main id="main">
    <header style="max-width: 640px">
      <h1 style="font-size: var(--s-28); font-weight: 700; letter-spacing: -.015em; line-height: 1.2">三種介面方向，用同一份資料比較</h1>
      <p style="margin-top: 10px; color: var(--ink-2)">
        每個方向都能實際操作：首頁、專案、工作項目、Agent 狀態、Human Gate 決策。資料是真實的 Astra 交付
        （{{ d.projects.length }} 個專案、{{ d.items.length }} 個工作項目、{{ d.requirements.length }} 條需求、{{ d.runs.length }} 筆 Agent 執行）。
        標示「測試資料」的是為了讓每種狀態都出現而安排的進行中工作。
      </p>
      <div style="margin-top: 16px; display: flex; gap: 12px; align-items: center"><LabTheme /><span class="lab-faint" style="font-size: 13px">三個方向都有深色與淺色主題。</span></div>
    </header>

    <ul class="ch-list">
      <li v-for="x in DIRS" :key="x.to" class="ch-item">
        <div class="ch-letter" aria-hidden="true">{{ x.letter }}</div>
        <div>
          <h2 style="font-size: var(--s-21); font-weight: 700">方向 {{ x.letter }}：{{ x.name }}</h2>
          <p style="margin-top: 6px; max-width: 54ch">{{ x.idea }}</p>
          <p class="lab-muted" style="margin-top: 8px; font-size: 14px">適合：{{ x.best }} 取捨：{{ x.risk }}</p>
        </div>
        <NuxtLink :to="x.to" class="lab-btn is-primary ch-go">打開方向 {{ x.letter }}</NuxtLink>
      </li>
    </ul>

    <p class="lab-faint" style="margin-top: 28px; font-size: 13px; max-width: 70ch">
      這是設計原型，不會取代現有的 V2。你在這裡核准的決定、改變的狀態，只會寫進獨立的設計資料庫，不影響驗收環境。
    </p>
  </main>
</template>

<style>
.ch-list { margin-top: 36px; border-top: 1px solid var(--line); }
.ch-item { display: grid; grid-template-columns: 56px minmax(0, 1fr) auto; gap: 24px; align-items: center; padding: 26px 0; border-bottom: 1px solid var(--line); }
.ch-letter { font-size: 40px; font-weight: 700; color: var(--ink-3); line-height: 1; }
@media (max-width: 720px) { .ch-item { grid-template-columns: 1fr; gap: 12px; } .ch-go { justify-self: start; } }
</style>
