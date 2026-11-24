# AstraDraupnir V2.1 — test summary（對應程式最終 commit `0c11428`）

| 項目 | 指令 | 結果 |
|---|---|---|
| API build | `dotnet build --no-restore` | 0 warnings / 0 errors |
| API tests | `dotnet test --no-restore` | 32 passed / 0 failed（每個測試自建、自刪 `draupnir_test_<guid>` 資料庫） |
| Web typecheck | `npx nuxt typecheck` | exit 0 |
| Web unit / component | `npx vitest run` | 40 passed / 0 failed（11 檔案） |
| E2E | `NUXT_API_BASE=… npm run test:e2e` | 26 passed（delivery + acceptance + a11y-keyboard；桌面 1280 與 390px，40.5 秒） |
| 產品截圖 / a11y / 溢出 | `tools/product-shots.cjs` | T73 兩輪各 56 views、整合輪 36 views：0 serious/critical axe、0 水平溢出、0 頁面錯誤 |
| Stylus 交付報告 | `astra-stylus render … --format docx`、`quality verify-content` | 兩者 exit 0（`ok: true`） |

隔離方式：E2E 與 API 測試都使用專用資料庫（`draupnir_test_<guid>` / `draupnir_v21_accept` / 驗收用 `draupnir_v21_lead`），不寫入 `draupnir_v21` 的既有資料；測試完成後資料庫刪除、`pg_database` 查得 0 筆。
