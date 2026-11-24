# AstraDraupnir V2.1 — independent review log

每一次修正都在獨立、唯讀的審查下確認：先重現原問題，再看測試是否真的會在修正前失敗。
本檔只摘要結論；完整審查報告與逐項證據留在交付工作區的 `evidence/`（不進 repo，避免把內部工具紀錄公開）。

## Round 1 — acceptance claims (PR #5 initial)

| Review | Scope | Verdict |
|---|---|---|
| T74 | Security, Human Gate, data consistency, regression, secret scan of the V2.1 diff | 0 critical / 2 high / 3 medium / 2 low → must-fix 2 (gate evidence rule server-side; import write authorisation) |
| T74b | Delta review of the gate-rule commit | OK — rule matches the UI, no bypass; 3 low notes |
| T74c | Delta review of the merged acceptance branch | 1 must-fix (E2E wrote unbadged data / fixed requirement code) → fixed |
| T74d | Confirm the badge/uniqueness fix | OK (1 low: spool evidence title) → fixed |

## Round 2 — ChatGPT review (F-01…F-05)

| Finding | Fixed | Test that reproduces the original problem |
|---|---|---|
| F-01 Member could archive a project | `ProjectUpdateInput`; only Admin may change Status | `ProjectStatusCanOnlyBeChangedByAnAdmin` (before: expected 403, actual 200) |
| F-02 last-admin race | count + update in one SERIALIZABLE transaction | `ConcurrentDemotionsCannotRemoveTheLastActiveAdministrator` (before: both requests 200) |
| F-03 import identity collision | `AgentRun.SourceInstance` in the unique index and every lookup, additive migration | `F03SameTaskIdInTwoSourceInstancesStaysSeparate` (before: second import 409) |
| F-04 gate evidence integrity | latest attempt per source task, fresh evidence only, manual-exception rules unchanged, decision snapshot | `GateEvidenceIntegrity*`, `GateOnAWorkItem*` |
| F-05 E2E write isolation | fail-closed guard: opt-in token, loopback, disposable DB, instance match | `e2eGuard.test.ts` + a real refusal before the first write |

| Review | Scope | Verdict |
|---|---|---|
| T76 | Delta review of the F-01…F-05 fixes | 0 must-fix; 4 residuals (1 medium, 3 low) |
| T76b | Confirm the medium residual fix | OK — legacy rows are matched by the frozen namespace, no regression |

## Round 3 — ChatGPT round 2 residuals (R2-01…R2-04)

| Finding | Fixed | Test that reproduces the original problem |
|---|---|---|
| R2-01 gate identity collision | `Project.SourceInstance` frozen on first import; default ref `supervisor:{instance}:{id}`; legacy gates adopted, not duplicated | 4 integration tests (separate gates, A approved ≠ B, idempotent re-import, rename keeps one namespace, legacy adoption keeps the decision) |
| R2-02 latest attempt grouped without the task id | grouping by `Source + SourceInstance + ExternalTaskId` on both sides | `ApiEvidenceRuleTests` (TASK-A Fail + TASK-B Pass → 400; TASK-A retried Pass → 200) + front-end mirror test |
| R2-03 decision snapshot incomplete | one SERIALIZABLE transaction; snapshot keeps the used evidence (result time source attempt content hash) and reviews | `DecisionSnapshotDoesNotChangeWhenTheEvidenceIsReimported` |
| R2-04 E2E could write to another target | no server reuse; API reports `E2E_INSTANCE_TOKEN`, E2E must match `E2E_EXPECTED_INSTANCE` | `e2eGuard.test.ts` + real refusal (exit 1 before any write) |

| Review | Scope | Verdict |
|---|---|---|
| T77 | Implementer report for R2-02 / R2-04 / Review Desk reason (Pi-2) | all three done; API 42, web 52, e2e 26 |
| T78 | Independent delta review of `d58c8c7..960f876` | **0 must-fix**; 3 low residuals (docs counts, legacy-decision assertion, explicit-instance freezing) all closed in `960f876` |
