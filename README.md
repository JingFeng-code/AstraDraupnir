# AstraDraupnir

> **目前版本是 V2.1**（`v2/`：Nuxt 4 工作空間 + ASP.NET Core 10 API + PostgreSQL 18），品牌與視覺方向為 Direction D。
> 安裝、啟動、migration、測試與角色權限請看 **[`v2/README.md`](v2/README.md)**；驗收與已知限制請看
> **[`docs/v21/ACCEPTANCE.md`](docs/v21/ACCEPTANCE.md)**。
> 根目錄的 Angular 20 + Supabase 版本是 V1 的歷史版本，仍保留可使用，不再是主要交付。

## 名稱由來｜Astra Mythos 神器系列

**品牌與 GitHub repository 都是 AstraDraupnir**（`JingFeng-code/AstraDraupnir`；舊名 AstraCadence 已重新命名）。

- **神器原型**：北歐神話中的金環 Draupnir。
- **傳說中的能力與考證界線**：傳說每九夜產生八枚相同重量的新環；是週期象徵而非軟體已支援自動週期排程。
- **軟體對應能力（現代品牌比喻）**：週期、團隊節奏與任務推進。
- **實際產品定位**：團隊任務與進度管理。
- **技術相容性**：V1 的 Angular、Supabase、甘特圖及部署設定維持不變；V2.1 是 `v2/` 下的獨立交付，不影響 V1。

**全系列命名規範的唯一維護來源**：[Astra Mythos｜神器系列品牌規範](https://github.com/JingFeng-code/AstraLore/blob/main/06-%E5%8F%83%E8%80%83/Astra-Mythos/ASTRA_MYTHOS.md)，位於工程筆記庫 AstraLore；本專案不另存品牌規範副本。

---

> 一個以 Angular 20 為前端、Supabase 為後端 BaaS 架構的任務管理工具，支援 RWD 響應式介面，提供登入、任務清單、任務建立與修改等功能。

---

## 功能 Features
- 使用者註冊 / 登入 / 登出  
- 任務清單瀏覽與管理  
- 任務建立、修改與刪除  
- 任務進度管理，使用甘特圖顯示 (ngx-gantt)  
- 響應式網頁設計 (RWD)

---

## 技術棧 Tech Stack
| 技術/模組 | 說明 |
|-----------|------|
| Angular 20 | 建構前端 SPA，使用元件化架構與 RxJS 資料流管理 |
| Supabase | 後端即時資料庫、驗證與 RESTful API |
| Supabase Auth | 註冊/登入/登出與使用者狀態控制 |
| ngx-gantt | 任務進度管理甘特圖呈現 |
| HTML5 / CSS3 / Bootstrap | 響應式版面設計 |

---
