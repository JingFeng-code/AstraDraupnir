---
name: astra-design
description: 草案。在 AstraDraupnir V2.1 專案內新增或修改介面時使用：套用已認可的 Direction D 設計語言（token、ui-* 元件、五站追溯、資料誠實規則）。只適用本專案；不修改全域設定。
---

# astra-design（專案級 Skill 草案 v0.1）

狀態：**草案**，由 Claude（Design Lead）撰寫，未經擁有者確認，不屬於其他 CLI 的全域 Skills。規範來源：`AstraBookOfThoth/06-參考/Astra-Design-System/DESIGN-SYSTEM.md`；token 唯一來源：`v2/web/app/assets/css/main.css`。

## 何時使用
新增或修改 `v2/web/app` 下任何頁面、元件、樣式。不用於：後端、測試邏輯、其他 Astra 產品。

## 步驟
1. 先讀 `main.css` 的 token 與 `ui-*` 類別，再讀 `pages/index.vue`、`pages/review.vue`、`pages/trace.vue` 當成品範例。
2. 只用 `var(--c-*)` 與既有 Tailwind 對應名稱（`bg-surface`、`text-ink`、`text-ink-muted`、`border-line`…）。**不新增色碼、不新增字體、不加陰影或漸層。**
3. 版面放進殼層的 `.ui-content`；不要自帶外層背景或 `site-container`。
4. 版面元素：`ui-panel`（標題列右側寫來源與時間）、`ui-row`、`ui-table`、`ui-tabs`、`ui-callout`、`UiPill`、`UiMark`、`UiThread`、`UiChain`。主要按鈕每區一個（`btn btn-primary`）。
5. 狀態一律「形狀＋文字」，不只用顏色。
6. 每個資料畫面做齊四態：Loading（`AppLoading`）、Empty（`AppEmpty`＋下一步）、Error（`AppNotice`＋重試）、Success（toast）。
7. 資料誠實：匯入快照寫來源與絕對時間；沒有即時連線不寫「正在…」；未知執行者寫「未確認」；本機決定寫「只記錄在 Draupnir，未同步 Supervisor」；標題含「測試資料」要顯示徽章；Human Gate 缺必要證據時不得提供核准捷徑（見 `app/lib/evidenceRules.ts`）。
8. 字級：內文 16px，次要 14px 為下限；列高 ≥ 60px、觸控目標 ≥ 42px。
9. 完成後跑 `nuxt typecheck`、`vitest`，用 `tools/product-shots.cjs` 截圖四種組合（深/淺 × 桌面/行動）並自行檢視，再交給 `astra-design-review`。

## 禁則（Anti-AI-Slop）
星空、粒子、霓虹、發光、大面積漸層、相同圓角卡片陣列、emoji 圖示、全大寫寬字距小標、KPI 英雄區當唯一首頁內容、米黃暖色後台、無功能的裝飾或按鈕、假資料填版面。
