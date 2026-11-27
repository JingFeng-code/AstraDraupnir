# AstraDraupnir V2.1｜發布準備與現階段結案

- 程式基線：cdf0ee1497f135246d24f8f7029e0dcfa0f3a316；[Draft PR #5](https://github.com/JingFeng-code/AstraDraupnir/pull/5)，分支 auto/p7-draupnir-v21。
- 2026-11-24：使用者接受現階段產品方向，細項延至全部 Astra 產品完成後；功能與 UI 凍結。ChatGPT 靜態差異複審 GO for local UAT。未逐項記錄完整使用者 UAT，未合併、未發布、未部署。
- 本機回歸由 Agent 回報 API 49/49、Web 52/52、E2E 26/26；ChatGPT 未獨立重跑；PR 無 CI checks。歷史測試與審查見 [ACCEPTANCE](ACCEPTANCE.md)、[TEST-SUMMARY](TEST-SUMMARY.md)、[REVIEW-LOG](REVIEW-LOG.md)。

## 使用入口與安全界線

原交付預覽 Web http://127.0.0.1:3201、API http://127.0.0.1:5082；是否仍運行需本機確認。既有管理員帳號由本機管理，密碼不記於文件；重啟不會覆寫既有帳號。啟動與隔離測試見 [V2 README](../../v2/README.md)。不對預覽 DB 跑寫入式 E2E，不批准真實工作 Gate 作測試。

核心操作：登入 → 建測試專案與需求 → 工作項目/相依性/看板 → Sprint/Milestone → 匯入可信任的 Supervisor/Spool 快照 → 查證據與決策（隔離資料） → 產生 Markdown/DocumentSpec 報告。Gate 決定只在 Draupnir 記錄，不回寫 Supervisor。非即時 Agent 控制台。

## 發布門檻（各自獨立）

| Gate | 狀態 |
|---|---|
| 功能範圍與方向接受 | 已接受，細節延後 |
| 工程定點複審 | 完成，指定本機條件下 GO for UAT |
| 使用者逐項 UAT / 明確發布接受 | 未完整記錄 |
| 合併 main | 未授權；核對最新 PR diff、目標基線、無阻塞後另行授權 |
| Tag / Release | 未授權；先確認版本、合併 SHA、已知限制 |
| 對外部署 | 未驗證、未授權；TLS、憑證、存取控制、DB 備份/還原、migration、edge rate limit、成本及維運門檻另行確認 |

## 已知限制及重開條件

- A-11 Milestone UI 建立/編輯、A-14 證據連結點擊仍 PARTIAL。
- T79 L-01：特製自訂 ref 可能誤接手其他來源 Pending Gate。現階段僅接受可信快照、明確專案與來源；外部自動/不受信任匯入前須修正。已決策 legacy gate 不被搬移不代表所有身份碰撞已消失。
- 歷史證據 migration 可能使舊證據過期；需重新驗證或合法人工例外，不直接核准。
- 非阻塞 UI 細節、Icon/Logo、即時同步、Gate 回寫等見 [BACKLOG](BACKLOG.md)，不再開發本版本。若使用者實際發現安全、資料一致性、人工決策或核心操作阻塞，才重開最小範圍。
- 沒有正式部署驗證，也不聲稱所有安全控制適用於網際網路。

本文件不包含現行密碼、私人路徑或使用者工作資料；不新增執行程式或 CI。下一個 Astra 產品可先進入本機驗收，不要求先將本 PR 合併。
