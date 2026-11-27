# AstraDraupnir V2.1 — deferred items (after the UAT freeze)

V2.1 is feature-frozen for UAT. Nothing below is a blocker for the Draft PR; each item records
what it is, when it triggers, and what it can affect, so a later release decision can weigh it.

## Review findings kept as known-low

### L-01 — a crafted custom gate ref could adopt another source's *Pending* gate
- **What**: `SupervisorImport` namespaces custom `gate.externalRef` as `supervisor:{instance}:{custom}` and, when no gate with that ref exists, adopts a legacy gate whose ref is exactly `supervisor:{id}` or exactly the raw custom ref — but only while that gate is **Pending**. A custom ref crafted to equal another instance's already-namespaced ref (e.g. `supervisor:beta:release-42` imported by instance `alpha`) can still claim beta's *pending* gate and rename it to alpha's namespace.
- **Trigger**: only if an importer deliberately sends such a ref; the Draupnir UI never produces custom refs, and normal Supervisor queues use the default.
- **Impact**: a *pending* gate can be re-labelled to another source instance. A gate that already carries a human decision is never moved (`Status != Pending` is skipped) and the decision/note/snapshot stay with the original row. No data loss, no privilege change.
- **Fix sketch for a later version**: only adopt when the legacy ref is the old default `supervisor:{id}`, or store the source instance on the gate at creation and compare it before adoption.

### L-02 — API tests use fixed e-mail addresses
- **Trigger**: running the API integration suite twice against the *same* database (each run normally creates its own `draupnir_test_<guid>`).
- **Impact**: the second run fails on the unique e-mail constraint. CI isolates the database, so this is a local-only annoyance.

## Deferred by the owner (not defects)

- **A-11 PARTIAL** — Milestone create/edit is exercised through the API and displayed in the UI, but the UI form was not separately automated.
- **A-14 PARTIAL** — the evidence → work-item link renders and its target is checked; the click itself is not automated.
- **UI polish** — the circular / half-circle / diamond / square / dashed-circle status icons, and the dedicated Astra icon system and logo.
- **Integration** — live Supervisor polling (imports are snapshots by design), Supervisor write-back of gate decisions, SSO/MFA, login rate limiting at the edge.
