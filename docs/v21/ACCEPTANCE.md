# AstraDraupnir V2.1 — 驗收交付（Direction D）

> 交付日期：2026-11-24　　分支：`auto/p7-draupnir-v21`　　**程式最終 commit：`0c11428`；其後只有文件與 E2E 測試資料標記的修正（清單見 `git log 0c11428..HEAD`）**
> T75 修正分支：`auto/p7-v21-fix2` (HEAD)，尚未合併；F-03／F-04／F-05 的語意見 §5.1。
> 本文件是給產品驗收（ChatGPT 獨立審查）的入口。所有數字與狀態都對應上面的最終 commit；過期結果不會拿來充數。

## 0. 一句話結論

Draupnir V2.1 已在本機完成可實際使用的產品交付：Direction D 工作空間（總覽／審查與決策／追溯與品質／Agent／專案＋八個專案子頁／使用者與權限／稽核紀錄／系統設定），含 Admin/Member 權限、Human Gate 證據規則（**前端與後端都強制**）、Supervisor 與 Spool 匯入、Stylus 交付報告、深淺主題與行動版（E2E 以 360px 執行，嚴格於要求的 390px）。**Draft PR #5 已建立**（未合併、未部署；本輪修正後需要一次明確 push 授權才能更新遠端分支，見 §8）。本機預覽維持在 `http://127.0.0.1:3201`。

## 1. 本機啟動（可重現）

```powershell
# 1) PostgreSQL（可攜版，127.0.0.1:55432）
pwsh -NoProfile -File E:\Astra\.astra-runs\AUTONOMOUS-DELIVERY\tools\pg-local.ps1 start

# 2) API（.NET 10；資料庫 draupnir_v21 或自建）
cd v2\api
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__Draupnir='Host=127.0.0.1;Port=55432;Database=draupnir_v21;Username=postgres'
$env:DRAUPNIR_ADMIN_EMAIL='admin@draupnir.local'; $env:DRAUPNIR_ADMIN_PASSWORD='<自訂>'
$env:DRAUPNIR_REPORT_DIR='E:\Astra\.cache\v21-reports'
dotnet restore; dotnet build
dotnet ef database update --project src/Draupnir.Api --no-build     # 第一次或 schema 變更時
dotnet run --project src/Draupnir.Api --no-build --no-restore -- --urls http://127.0.0.1:5082

# 3) Web（第二個視窗）
cd v2\web
$env:NUXT_TELEMETRY_DISABLED='1'; $env:NUXT_API_BASE='http://127.0.0.1:5082'
npm ci; npm run dev -- --host 127.0.0.1 --port 3201
```

登入帳號由 `DRAUPNIR_ADMIN_EMAIL` / `DRAUPNIR_ADMIN_PASSWORD` 決定（第一次啟動時建立，密碼用 ASP.NET Identity 雜湊）。本機預覽機的帳號是 `admin@draupnir.local`，密碼由 `DRAUPNIR_ADMIN_PASSWORD` 設定（交付時已輪替；repo 與 PR 不含任何密碼）。E2E 也需要 `E2E_ADMIN_PASSWORD`。

## 2. 角色與權限

| 能力 | Admin | Member |
|---|---|---|
| 看/編輯自己所屬的專案 | ✅ | ✅ |
| 看別人的專案 | ✅ | ❌ 403（清單也不列出） |
| 建立／封存專案 | ✅ | ❌ 403 |
| 使用者管理（建立、改角色、停用、改密碼） | ✅ | ❌ 403 |
| 專案成員管理 | ✅ | ❌ 403 |
| Human Gate 決定 | ✅ | ❌ 403（UI 也隱藏） |
| Supervisor 匯入／Spool 證據匯入 | ✅ | ✅ 僅 Admin（本次修正：成員 403，匯入頁對成員隱藏） |
| 稽核紀錄 | ✅ | ❌ 403 |

- 只有兩種角色（`Admin`、`Member`），沒有 Viewer；SSO/OIDC 明確不在本輪（ADR-006）。
- 伺服器端每次都查 DB 的 `IsActive` 與角色，停用或降階後舊 cookie 立即失效；最後一個 active admin 不能被停用或降級。
- Cookie：`HttpOnly`、`SameSite=Strict`、`SecurePolicy=SameAsRequest`（本機 HTTP；正式環境改 `Always`，見 §5.1 安全政策）。**Session**：8 小時絕對期限、`SlidingExpiration=false`；`SecurityStamp` 是 cookie claim，改密碼／角色／啟用狀態會立即撤銷舊 session（本輪新增，見 §8）。登入失敗 5 次／10 分鐘 → 429。

## 3. 驗收矩陣（A：產品功能）

PASS = 我（Tech Lead）在最終 commit 上實際跑過並看到結果；測試名稱／指令與截圖都附在後面。

| ID | 項目 | 結果 | 證據 |
|---|---|---|---|
| A-01 | 登入並進入工作空間 | PASS | Playwright 登入 → `/`；`GET /api/auth/me` 200；`overview-dark.png` |
| A-02 | 登出後受保護頁面回登入 | PASS | E2E `delivery.spec.ts`（登出／再進 401）；API 401 |
| A-03 | 使用者：建立、改角色、停用、改密碼 | PASS | `ApiIntegrationTests.…`（角色、停用後 401、密碼重設後舊密碼 401）；本輪 API 實測：create 201 |
| A-04 | 專案建立／編輯／封存 | PASS | API create 201、PUT 200、DELETE → Archived；Member 建立 403 |
| A-05 | 專案成員：加入／移除；非成員 403 | PASS | 非成員 GET 專案 403、列舉 0 筆；加入後 200；移除後 403（E2E + API） |
| A-06 | 需求建立／編輯／篩選 | PASS | API：`REQ-900` 建立 201（需求代碼格式 `REQ-n`，全庫唯一） |
| A-07 | 工作項目：表格與看板一致 | PASS | API 建立 201；`WorkItemBoard.test.ts`、`board.test.ts`；截圖 `work-items-*` |
| A-08 | Bug 類型＋S1–S4 嚴重度（表單、表格、看板、篩選、排序） | PASS | API：`type=Bug, severity=S2` 建立、`?type=Bug` 篩選回 1 筆；`workItemForm.test.ts` |
| A-09 | 相依性：加入／移除；相依未完成時不能 Done | PASS | API：加相依 201、B→Done 409、A Done 後 B→Done 200；`board.test.ts` |
| A-10 | Blocked 必須有原因 | PASS | API：無 reason 400（`errors.reason`）、有 reason 200 |
| A-11 | Sprint／Milestone：建立、編輯、單一 Active Sprint | PARTIAL | API：Sprint 201、第二個 Active 409、Milestone 201；E2E 驗 Sprint 建立／指派／進度與編輯。**Milestone 的 UI 建立／編輯未逐一操作**（只驗 API 與顯示） |
| A-12 | 搜尋、篩選、側邊欄收合、Ctrl+K | PASS | `a11y-keyboard.spec.ts`（跳至主要內容、Ctrl+K、側邊欄記憶） |
| A-13 | Agent 執行紀錄：Supervisor 匯入，標示快照與記錄時間 | PASS | API 匯入 `{agentRuns:3, evidence:0, reviews:0, gates:1}`；UI「匯入快照，不是即時連線」＋「記錄時間」 |
| A-14 | 證據與審查：列表、篩選、連到工作項目 | PARTIAL | Spool 證據寫入後 `GET …/spool-results` 1 筆、篩選與畫面渲染已驗；**「證據→工作項目」的連結點擊沒有自動化斷言**（只檢查了目標 href/渲染） |
| A-15 | 五階段追溯鏈 | PASS | `/trace` 頁；`UiChain`；截圖 `trace-*` |
| A-16 | Human Gate：必要證據、人工例外、Admin only、不同步 Supervisor | PASS | 後端 `GateRules`（本次新增，31 API 測試）；UI 實測：缺證據時「缺少必要證據」、**沒有直接核准鈕**、只有人工例外、且顯示「只記錄在 Draupnir」；`evidenceRules.test.ts` |
| A-17 | Gate 駁回記錄 | PASS | API：`approved:false` → `Rejected` + note；稽核 `gate.decision` |
| A-18 | 稽核紀錄：登入、建立/更新、Gate 決定 | PASS | `auth.login` / `auth.login.failed`（未知信箱用 nullable actor，不記密碼）/ `auth.logout` / `project.create|update|archive` / `supervisor.import` / `spool.import` / `report.generate` / `gate.create|decision` / `user.create|update` / `membership.add|remove` / `dependency.add|remove`；`ApiIntegrationTests.AuditRecordsAuthAndCrudWithoutSecrets`；UI 只回最近 200 筆（明示） |
| A-19 | 交付報告：產生、看 Markdown、下載 Stylus YAML、實際開啟 | PASS | API markdown 200（291 bytes）、stylus.yaml 200（1,269 bytes）；Stylus CLI 渲染 DOCX exit 0、`quality verify-content` exit 0（`ok:true`） |
| A-20 | 重啟後資料仍在 | PASS | 重啟 API 後同一個專案／Spool 證據仍可讀 |
| A-21 | 深淺主題、1440 與 360（要求 390，測試更嚴格）、無水平捲動 | PASS | 8 頁 × 2 主題：overflow 0；36 組整合截圖＋T73 56×2 views 0 axe/overflow；E2E viewport 1280 與 360 |
| A-22 | V2 回歸：原交付 E2E 仍通過 | PASS | `npm run test:e2e` → **22 passed**（我獨立重跑，隔離 DB） |
| A-23 | 測試資料標記 | PASS | `isTestData` + `ui-tag is-test`（agents/index/UiChain/WorkItemBoard）；**驗收 E2E 建立的資料全部以「測試資料」開頭**（T74c 發現後修正，`64eca5c`），且只在可拋棄的 DB 上執行 |

## 4. Astra 生態系整合（B）

| 工具 | 現況 | 判定 |
|---|---|---|
| Herdr / hc | Supervisor 用真實 `hc` session 派遣；Draupnir 只匯入結果，不控制 hc | **已驗證（T17 證據）**，Draupnir 端為匯入快照 |
| AstraGleipnir / Supervisor | `POST /api/import/supervisor` 接受 queue.json／tasks.json，冪等更新、保留人類決定；UI 標示快照與時間 | **手動／API 匯入，非即時連線**（實測 3 agent runs + 1 gate） |
| AstraSpool | `POST /api/projects/{id}/spool-results` 收 `spool-result/v1`，存成 Trace 證據（Pass/Fail、原始 JSON、SHA-256 去重） | **已自動整合（本機）**：實測匯入 1 筆、列表可見 |
| AstraStylus | 報告產生 Markdown＋DocumentSpec；`DRAUPNIR_STYLUS_EXECUTABLE` 時可自動渲染 DOCX | **已整合**：YAML 由 Stylus 渲染 DOCX exit 0、verify-content exit 0 |
| 反向同步 | Draupnir 的 Gate 決定**不會**回寫 Supervisor | **未實作**（UI 明說）；屬設計邊界 |
| 即時狀態 | 沒有任何輪詢／WebSocket；所有 Supervisor/Spool 資料都是匯入快照 | **未實作**（刻意，避免把歷史當即時） |

## 5. 安全與人工政策

- 已修（本次）：**Gate 證據規則在後端強制**（`GateRules`，缺少通過證據／有失敗證據／審查要求修改時，核准必須是帶固定前綴且 ≥20 字理由的人工例外）。獨立審查的 delta review（T74b）判定 **OK**：前後端條件、19 字前綴、20 字規則逐字一致，主要繞過路徑（無證據直接核准、偽造備註、已決定重送、非 admin、只有 NotTested、證據挂別的項目）都被擋下，新測試確實會在移除規則時失敗。
- 第二次獨立審查（T74c，只看合併後的 delta `6c0602c..0c11428`）：F-02（匯入 admin-only）、合併解析（Gate 規則未被削弱）、稽核覆蓋（A-18 三類都有，detail 不含密碼/token/payload）、migration（僅 `DROP NOT NULL`，additive）全部 **OK**；唯一 must-fix 是驗收 E2E 沒清理且未標測試資料，已在 `64eca5c` 修正並重跑 26 passed。
- 已知語意（T75 起）：沒有 `workItemId` 的 Gate 仍是「人工判斷」，但核准必須寫明至少 20 字理由，理由會寫入 `gate.decision` 稽核列；前端對話框同步要求。
- - 已修（本次）：**Supervisor 匯入與 Spool 證據匯入改為 Admin-only**（成員 403、UI 隱藏入口、寫入都留稽核，且只記 counts／command/status/hash，不記內容），避免成員自行製造 Pass 證據再讓 admin 核准。
- 已驗證：IDOR 全面以專案成員資格檢查（非成員 403、清單不列出）；child 路由用 route 的 projectId 覆寫 body；`/users`、`/audit`、成員管理、Gate 決定 admin-only；已決定的 Gate 重送 409（Status 是 concurrency token）。
- 已知限制：OpenAPI 匿名可讀（本機工具）；沒有登入 rate limit；cookie 無 security stamp／絕對到期（§10）。
- 證據關聯語意：API 以 `WorkItemId` 為準；前端的追溯頁另外接受「只挂 agent run」的證據（`buildTrace`）。目前的寫入端都會帶 `WorkItemId`，所以兩者一致；若未來新增只挂 run 的寫入，後端會要求例外（T74b B-01，列入 §10）。

## 5.1 T75 修正（F-03／F-04／F-05）

T75 在 `auto/p7-v21-fix2` 修正三個獨立審查 finding；本節是語意與重跑入口，不是新的產品功能。

### F-03 Supervisor 匯入身分

`AgentRun` 新增 `SourceInstance`。匯入身分 = `Source` + `SourceInstance` + `ExternalTaskId` + `Attempt`；查詢與唯一索引都包含它。`?sourceInstance=` 優先，否則使用 payload 的 `sourceInstance`、第一個 task 的 `repo`／`worktree`／`state`，最後才是 `supervisor`；**不使用 project id**，所以專案重建後仍可冪等重匯。同一個 task id 在兩個 source instance 是兩筆獨立 run；同一 source instance 重匯同一 attempt 仍更新原 run。`AddAgentRunSourceInstance` 先把既有列以所屬 project key 回填（孤兒列 `legacy`），再建新唯一索引、移除舊索引。

### F-04 Human Gate 證據完整性

- 必要證據只取每個來源（`Source`+`SourceInstance`）的**最新 attempt**；舊 attempt 的 Fail 不會永久擋住後來通過的重試。
- 證據 `RecordedAt` 早於工作項目 `UpdatedAt` 視為 **stale**，不能單獨作為核准依據；stale／舊 attempt 的 ID 仍留在 snapshot 供稽核。
- `Pass` 必要；`Fail` 與 `ChangesRequested` 直接擋核准；`Blocked`、`NotTested` 不是通過，只有它們時仍須人工例外。
- 工作項目 Gate 的人工例外維持固定前綴＋≥20 字理由＋Admin-only，沒有放寬。
- 沒有工作項目的 Gate：核准需 ≥20 字理由，寫入稽核；駁回不強制理由。
- 每次決定都寫 `EvidenceSnapshot`（評估時間、工作項目更新時間、最新 attempts、各 result counts、使用／stale／忽略的 evidence 與 review IDs、是否使用例外、人工理由），migration additive。
- 後端仍是唯一強制點；`app/lib/evidenceRules.ts` 同步顯示最新 attempt／stale 規則。

### F-05 E2E 寫入隔離

Playwright `global-setup` 在任何測試寫入前 fail-closed：必須 `E2E_ALLOW_WRITE=1`、`E2E_STATE_DIR` 可寫、`NUXT_API_BASE` 是 loopback，且匿名 `GET /api/health` 回報 `status=ok`、`disposable=true`；`draupnir`／`draupnir_v21`／`draupnir_design` 一律拒絕。health 只回 DB 名稱與可拋棄標記，不含資料。安全指令：

```powershell
cd v2\web
$env:E2E_ALLOW_WRITE='1'
$env:E2E_STATE_DIR='E:\Astra\.cache\tmp\t75-e2e-state'
$env:E2E_ADMIN_PASSWORD='<被測 API 管理員密碼>'
$env:NUXT_API_BASE='http://127.0.0.1:5088'
$env:E2E_PORT='4950'
npm run test:e2e
```

`npx vitest run tests/unit/e2eGuard.test.ts` 證明缺少 opt-in、非 loopback、preview DB、讀不到 health 都會被拒絕。

## 6. 測試與原始結果

| 項目 | 指令 | 結果 |
|---|---|---|
| API build | `dotnet build --no-restore` | 0 warnings / 0 errors |
| API tests | `dotnet test --no-restore`（隔離 DB） | **49 passed / 0 failed** |
| Web typecheck | `npx nuxt typecheck` | exit 0 |
| Web unit/component | `npx vitest run` | **52 passed / 0 failed**（13 檔案） |
| E2E（桌面 1280＋行動 360，真實 API + PostgreSQL） | `E2E_ALLOW_WRITE=1 E2E_STATE_DIR=… npm run test:e2e` | **26 passed**（fail-closed 寫入保護開啟；delivery + acceptance + a11y-keyboard） |
| Stylus 報告 | `astra-stylus render … --format docx` + `quality verify-content` | exit 0 / `ok:true` |
| 截圖＋axe＋overflow | `product-shots.cjs`（T70/T71/T72/integration/T73） | T73 兩輪各 **56 views** ＋整合 36 views；所有視圖 **0 serious/critical axe、0 水平溢出、0 頁面錯誤**，0 serious/critical axe、0 水平溢出 |

## 7. 缺陷與修正

| # | 症狀 | 根因 | 修正 | 驗證 |
|---|---|---|---|---|
| D-01 | 直接 `curl` 就能在沒有證據時核准 Human Gate | 證據規則只在前端 | 後端 `GateRules`＋端點檢查（`f1806bb`） | API 測試（無證據 400、例外 200、駁回 200） |
| D-02 | 交付報告 Markdown 捲動區無法用鍵盤聚焦（首輪 E2E 抓到） | 捲動容器沒有 tabindex／名稱 | 具名可聚焦區域 | E2E 全綠（交接文件） |
| D-03 稽核覆蓋不足（登入／匯入／報告等沒寫入）→ 補齊 17 種 action 並加測試；D-04 成員可偽造證據（Supervisor/Spool 匯入只檢查成員）→ 改為 Admin-only＋稽核；D-05 `import`/`evidence` 在 390px 出現 16px 水平溢出 → 修 `.ui-stack` 子項 `min-width:0`，重跑截圖 0 溢出 | | | | |
| D-06 Supervisor 同 task id 跨來源衝突／覆寫 | run 查詢與唯一索引只有 Source+ExternalTaskId+Attempt | `SourceInstance`＋`AddAgentRunSourceInstance` 回填 | `F03SameTaskIdInTwoSourceInstancesStaysSeparate`；重匯同 source 仍 1 筆 |
| D-07 Gate 可能被舊 attempt 的 Fail 永久卡住、無法稽核決策當時證據 | 只檢查全部證據與當下結果 | `GateRules.Evaluate` 取最新 attempt、排除 stale、`EvidenceSnapshot` | 6 個 Gate API 測試：missing／Fail／Changes／NotTested／Blocked／例外／stale／舊 Fail→新 Pass |
| D-08 無工作項目 Gate 可空白核准；E2E 可誤寫 preview | 沒有理由要求；E2E 沒有寫入保護 | ≥20 字理由＋稽核；health／opt-in fail-closed guard | `GateManualException...`、`e2eGuard.test.ts`、實際 E2E 26 passed |

## 8. ChatGPT 複審修正（第二輪）

PR #5 被 ChatGPT 判為 Conditional NO-GO；以下每一項都在本分支修正，並有「修正前失敗 → 修正後通過」的測試證據（完整紀錄見 `.astra-runs/.../evidence/T75-fix-round-pi.md` 與 `T76`）。

| Finding | 重現 | 根因 | 修正 | 測試證據 |
|---|---|---|---|---|
| **F-01** Member 可封存專案（High） | 是：Member `PUT /projects/{id}` 帶 `status:"Archived"` 回 200 | PUT 直接綁定整個 `Project` 實體（含 Status） | 專用 `ProjectUpdateInput`（key/name/description/repoPath + 可選 status），Status 變更只有 Admin；DTO 不含 id/createdAt | 修正前：`Expected Forbidden, Actual OK`；修正後 `ProjectStatusCanOnlyBeChangedByAnAdmin`（Member 改名 200、封存/解除封存 403、Admin 封存/解除 200） |
| **F-02** 最後一位 Admin 競態（High） | 是：兩個並行停用請求都回 200 → 0 位有效 Admin | 檢查與更新不在同一交易 | 同一個 `SERIALIZABLE` 交易內檢查並更新；競爭者提交時失敗 → 409 | 修正前：`Assert.Contains Filter not matched`（兩個都成功）；修正後 `ConcurrentDemotionsCannotRemoveTheLastActiveAdministrator`（至少一個非 2xx、至少一位有效 Admin） |
| **F-03** Supervisor 匯入身份碰撞（High） | 是：同一 `TASK-001` 匯入第二個來源回 409／覆蓋 | 唯一鍵與查詢只有 `Source + ExternalTaskId + Attempt`，沒有來源實例 | 新增 `AgentRun.SourceInstance`，唯一索引改為含它；匯入查詢、migration（additive＋backfill）與文件同步 | `F03SameTaskIdInTwoSourceInstancesStaysSeparate`（修正前 409 → 修正後兩個來源各自獨立、同來源重匯仍冪等） |
| **F-04** Gate 證據完整性（Medium） | 是（語意未定義） | 只認「有沒有 Pass」，沒有最新 attempt、Blocked/NotTested、過期證據的規則，也沒有決策快照 | 明確規則：只採每個來源最新 attempt、Blocked/NotTested 不算通過、過期證據不得單獨放行；無工作項目的 Gate 必須寫 ≥20 字理由；決策時把評估結果存成 `EvidenceSnapshot`；人工例外權限沒有放寬 | `GateEvidenceIntegrity*` 與 `GateOnAWorkItem*` 的 API 整合測試（缺少/失敗/要求修改/只有 NotTested/Blocked/過期 → 400；最新重試 Pass → 200；例外或無工作項目有理由 → 200 並存快照） |
| **F-05** E2E 寫入保護（Medium） | 是：原本任何 `NUXT_API_BASE` 都會寫資料 | 沒有 fail-closed 檢查 | `E2E_ALLOW_WRITE=1` ＋ loopback ＋ `E2E_STATE_DIR` ＋ `/api/health` 回報 `disposable:true` 才放行；DB 名稱若是 `draupnir`/`draupnir_v21`/`draupnir_design` 一律拒絕 | 對 preview（`draupnir_v21`）：`E2E 寫入保護：拒絕在非拋棄式資料庫 draupnir_v21 上寫入`，exit 1；未設 `E2E_ALLOW_WRITE` 也 exit 1（都在第一次寫入前） |

### 安全補強（本輪）

- **改密碼／改角色立即撤銷 session**：`User.SecurityStamp` 是 cookie claim，任何使用者變更都輪替；`Access.Current` 與 `/api/auth/me` 不接受過期或缺少 stamp 的 cookie。測試：改密碼後舊 client 401、重新登入 200、改角色後再 401。
- **Cookie 絕對期限**：`ExpireTimeSpan = 8h`、`SlidingExpiration = false`。
- **登入節流**：同一 email 10 分鐘內失敗 5 次 → 429（以 audit 記錄計算，成功登入不受影響）。
- **本機 vs 正式政策**：`v2/README.md` 的 “Security policy” 表（TLS/Secure、secret manager、OpenAPI、SSO/MFA、IP 層限流）。
- **已公開的舊本機密碼**：`draupnir`、`draupnir_design`、`draupnir_v21` 三個環境都已驗證失效（舊密碼 401、新密碼 200）；repo 與 PR 不含任何密碼。

### 需求編號唯一性（本輪結論）

`Requirement.Code` 的格式是 `REQ-n`，唯一性範圍是**同一專案內**（DB 唯一索引 `(ProjectId, Code)`，重複回 409）；不同專案可以各自有 `REQ-1`。E2E 已改用隨機 `REQ-<6 位數>`，不會再撞號。**若你要的是全域唯一（跨專案也不能重號）**，那是一次資料模型變更（需要 migration 改寫既有編號成 `<KEY>-REQ-n`），本輪先以文件與測試明確界定現行語意，請在複審時裁決是否要改。

### 第二輪複審殘留（R2-01～R2-04，本輪）

| Finding | 重現 | 根因 | 修正 | 修正前 → 後 |
|---|---|---|---|---|
| **R2-01** Gate 來源識別碰撞（High） | 是：同一專案、兩個來源實例、同一個 `TASK-001` → 共用一個 Gate | Gate 只用 `ProjectId + ExternalRef`，預設 ref `supervisor:{id}` 不含來源實例 | `Project.SourceInstance` 在第一次匯入時凍結（migration 用當下的 Key backfill）；預設 ref 改為 `supervisor:{instance}:{id}`；升級前的 `supervisor:{id}` 會被**接手改名**而不是重建 | 修正前：第二個來源拿不到自己的 Gate；修正後 4 個測試（各自獨立、A 核准不影響 B、同來源冪等、改名後 namespace 不變、legacy 接手） |
| **R2-02** 最新 attempt 分組漏 task id（High） | 是：同來源 TASK-A Fail、TASK-B Pass 被合成一個最新桶 | 只用 `Source + SourceInstance` 分組 | 改成 `Source + SourceInstance + ExternalTaskId`，前後端一致 | `ApiEvidenceRuleTests`（T-A Fail + T-B Pass → 400；T-A 重試 Pass → 200）＋前端同情境單元測試 |
| **R2-03** 決策證據快照不完整（Medium） | 是 | 只存 id 與統計；讀取與寫入不在同一交易 | 決策在一個 SERIALIZABLE 交易內完成；快照存實際使用的證據（kind/result/時間/來源實例/task/attempt/內容雜湊）與審查結論 | 核准後重新匯入並把證據改成 Fail → 快照（canonical JSON）與狀態不變 |
| **R2-04** E2E 寫入目標不一致（Medium） | 是：`E2E_REUSE=1` 可重用別的 server | 重用伺服器略過守門 | 移除重用（永遠自己起 `.output`）；API 以 `E2E_INSTANCE_TOKEN` 回報 `instance`，E2E 必須設定相同的 `E2E_EXPECTED_INSTANCE` | 錯誤/缺少 instance → 第一次寫入前 exit 1 |

**Migration 對歷史證據的影響（誠實說明）**：`GateEvidenceIntegrity` 為 `WorkItems` 加了 `UpdatedAt`；既有工作項目的 `UpdatedAt` 在 migration 時被填成當下時間，因此**該時間點之前產生的證據都會被視為過期**，不能單獨支撐直接核准——需要重新產生證據，或由 Admin 走人工例外（有前綴＋20 字理由，會留快照與稽核）。這是刻意的 fail-closed 行為，不是資料遺失。

**登入節流**：程式與文件一致（同一 email 10 分鐘內失敗 5 次 → 429；`v2/README.md` Security policy 表與 §2 都這樣寫）。**Review Desk 的純人工 Gate** 也要求 ≥20 字理由（前端驗證＋API 驗證）。**需求編號**依正式決策維持專案內唯一，沒有新增 migration。

### 獨立審查紀錄

完整審查結論與逐項對照在 **`docs/v21/REVIEW-LOG.md`**（T74/T74b/T74c/T74d、T76/T76b、T77、T78）。每一項都附「修正前失敗的測試名稱 → 修正後通過」，以及 migration 與資料保留的驗證方式。

## 8.1 GitHub 交付（Draft PR #5）

- PR：**https://github.com/JingFeng-code/AstraDraupnir/pull/5**（Draft、base `main`、head `auto/p7-draupnir-v21`）。
- 遠端分支：`https://github.com/JingFeng-code/AstraDraupnir/tree/auto/p7-draupnir-v21`。
- 本輪（ChatGPT 複審修正）尚未推送：等一次明確的 push 授權後，以 fast-forward 更新同一個分支與同一張 Draft PR（不另開 PR、不合併、不部署、不 force push）。
- 檢查結果：沒有 `.github/workflows`（無 push 觸發的 CI／部署費用）；沒有 tracked `.env` 或建置產物；diff 內沒有 token／金鑰／私鑰／私路徑。

## 9. 尚未完成／已知限制

1. **推送與 Draft PR 未執行**（等授權）。
2. Gate 決定不會同步回 Supervisor；所有 Supervisor/Spool 資料是匯入快照，不是即時狀態。
3. 沒有密碼重設自助流程、沒有 MFA、沒有 SSO（ADR-006）；cookie 無 security stamp／絕對到期。
4. 稽核紀錄只回最近 200 筆（後端既有視窗，UI 已明示）。
5. 沒有自動化效能測試；沒有多語系（全繁體中文）。
6. `/lab/*`（Direction A–D 的設計探索頁）仍存在但不在導覽中；交付後可移除或加 `noindex`。
7. 真實環境（部署、Supabase、正式 DB）完全未驗證。

## 10. 後續 UI 優化 Backlog（Direction D 之後）

- 圓形／半圓形／菱形／方形／虛線圓形狀態圖示（本輪已定案但列後續）。
- Astra 品牌 Icon System（不阻塞本輪）。
- 「待決定」與「需要處理」兩個數字的更多說明與點擊導引。
- 專案總覽的成員／權限面板在窄螢幕的呈現優化。
- `/lab` 移除或 `noindex`；把設計決策搬進 ADR。
- 深色主題下少數標籤對比再檢查（T70 已修一輪，持續監看）。
- 表格在 360–390px 的密度與「展開列」互動。
- 登入頁的錯誤訊息與 rate-limit 提示。
- ~~沒有 `workItemId` 的 Gate 要求最低理由~~（T75 已修）。
- 把證據關聯改成「WorkItemId 或該工作項目的 agent run」單一規則（T74b B-01）。
- ~~補整合測試：只有 NotTested、有 Fail、有 ChangesRequested、空白備註、別的工作項目的證據、重複決定~~（T75 已補）。

## 11. 截圖索引

- 整合驗收（36 組：overview / project / projects / requirements / work-items / reports / users / audit / settings × 深淺 × 1440/360）：`acceptance/screens/draupnir-v21/integration/roundfinal/`
- 各工作包：`T70/round4`、`T71/round2`、`T72/round2`、`T73/`（本次）
- 本文件引用的兩張代表圖：`docs/v21/screens/overview-dark.png`、`overview-light.png`
