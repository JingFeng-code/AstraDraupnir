# AstraDraupnir V2.1 — test summary

對應 PR #5 分支 `auto/p7-draupnir-v21` 的最新程式 commit（`git log --oneline -- v2/api v2/web | head -1`）。ChatGPT 定點複審：GO for UAT（功能凍結）。

| 項目 | 指令 | 結果 |
|---|---|---|
| API build | `dotnet build --no-restore` | 0 warnings / 0 errors |
| API tests | `dotnet test --no-restore` | **49 passed / 0 failed**（每個測試自建、自刪 `draupnir_test_<guid>` 資料庫） |
| Web typecheck | `npx nuxt typecheck` | exit 0 |
| Web unit / component | `npx vitest run` | **52 passed / 0 failed**（13 檔案） |
| E2E（桌面 1280＋行動 360） | `E2E_ALLOW_WRITE=1 E2E_STATE_DIR=<tmp> E2E_ADMIN_PASSWORD=… npm run test:e2e` | **26 passed**（fail-closed 寫入保護：API 必須回報 `disposable:true`，且資料庫名稱不得是 `draupnir`/`draupnir_v21`/`draupnir_design`） |
| 產品截圖 / a11y / 溢出 | `tools/product-shots.cjs` | 整合 36 views＋T73 兩輪各 56 views：0 serious/critical axe、0 水平溢出、0 頁面錯誤 |
| Stylus 交付報告 | `astra-stylus render … --format docx`、`quality verify-content` | 兩者 exit 0（`ok: true`） |

## 這一輪（ChatGPT 複審）新增／更新的測試

| Finding | 測試 | 修正前 | 修正後 |
|---|---|---|---|
| F-01 Member 封存 | `ProjectStatusCanOnlyBeChangedByAnAdmin` | 預期 403，實際 200 | 通過（改名 200、封存/解除封存 403、Admin 200） |
| F-02 最後一位 Admin 競態 | `ConcurrentDemotionsCannotRemoveTheLastActiveAdministrator` | 兩個並行請求都 200 | 通過（至少一個非 2xx；至少一位有效 Admin；狀態還原） |
| F-03 匯入身份 | `F03SameTaskIdInTwoSourceInstancesStaysSeparate` | 第二個來源 409 | 通過（來源各自獨立、同來源冪等） |
| F-04 Gate 證據完整性 | `GateEvidenceIntegrity*`、`GateOnAWorkItem*` | 規則未定義 | 通過（缺少/失敗/要求修改/只有 NotTested/Blocked/過期 → 400；例外或無工作項目有理由 → 200 並存 `EvidenceSnapshot`） |
| F-05 E2E 寫入保護 | `guard` 的拒絕案例 | 任何 API 都會寫入 | 對 `draupnir_v21` 與未設 `E2E_ALLOW_WRITE` 都 exit 1（第一次寫入前） |
| 安全 | `PasswordResetAndRoleChangeInvalidateExistingSessions`、`RepeatedFailedLoginsAreThrottled` | 舊 session 仍可用／沒有 429 | 通過 |

隔離方式：E2E 與 API 測試都使用專用、可拋棄的資料庫（`draupnir_test_<guid>`、`draupnir_v21_e2e`），不寫入 `draupnir_v21` 的既有資料；測試完成後資料庫刪除。
