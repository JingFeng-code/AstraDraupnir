# Draupnir V2.1: design lab (three directions to compare)

Status: prototypes only. Nothing here replaces the V2 pages; the lab lives under `/lab`. Not final until the owner picks a direction.
Skills followed: Anthropic `frontend-design` (plan, review against the brief, build, critique) and `webapp-testing` (headless Playwright, wait for `networkidle`, screenshot then inspect, selectors by role). They are not installed in Claude Code here; their official text was read directly from `anthropics/skills`.
Note: the two reference images were not received, so the palette comes from the written brief only. All colors live in `app/assets/lab/tokens.css` and can be retuned in one place.

## Subject and job
The owner of a one-person engineering practice delegates the work to AI agents (Codex, Pi, OpenCode, Claude) and decides what to believe. The screen's job is **to show what is proven and what needs the owner**, quickly enough that a long session does not tire. The shared scenario is the real Astra delivery: 13 projects, 57 work items, 88 requirements, 56 agent runs, 34 pieces of evidence and 7 pending human decisions. Work marked 「測試資料」 is staged in-flight V2.1 work so every state exists to compare.

## The one memorable thing: the trace thread
Astra's mark is not a star. It is a **fix**: in navigation, a position is *fixed* from known reference points, and where there are none you are *dead reckoning*. Every work item carries a thread of five stops: 需求, 工作, 執行, 證據, 決定. A **filled disc** is a fixed point (that stage is proven), a **ring** is in motion or unverified, a **dashed gap** means there is nothing to stand on. The thread tells the owner at a glance how far an item can be trusted, and it appears in all three directions: as a mark in the stream (A), as the chain of custody beside a decision (B), as the layout itself (C). Everything else stays quiet.

## Tokens
- Color, light: Mineral `#EDF0F4` (page), Slate ink `#18212B`, Fix verdigris `#17695B` (proven), Signal amber `#9A5300` (needs you), Cobalt `#2B55C7` (agents moving), Brick `#AD3327` (fault).
- Color, dark: Deep slate `#0F151C` (blue-leaning, not neutral black), ink `#E4EAF1`, verdigris `#5CC5AE`, amber `#F0B35A`, cobalt `#86A8FF`, brick `#F4877B`. Both themes follow the system setting and have a toggle; the choice is remembered and applied before first paint.
- State is never color alone: each state has its own shape (filled disc, ring, half ring, square, diamond, cross).
- Type: one family for Latin (Atkinson Hyperlegible Next, built for legibility, with tabular figures for numbers) and Noto Sans TC for Chinese, both bundled. Scale 13 / 15 / 17 / 21 / 28 on a 4px rhythm, line length under 70 characters.
- Radius by role: 3px for rows and inputs, 8px for panels and drawers, no fully rounded pills.

## Review of this plan against the generic defaults (frontend-design)
| Default to avoid | How this plan avoids it |
|---|---|
| Cream background with serif and terracotta | Cool mineral grays; sans only; no warm accent as the identity |
| Near-black with one bright accent | Dark is a blue-leaning slate; four accents each with a job; none is a brand splash |
| Newspaper columns with hairline rules | A uses rules only where content is a sequence; B and C use panes and nodes |
| Identical rounded cards with the same soft shadow | No shadow cards. A has none, B has panes, C has nodes of a distinct notched shape |
| ALL-CAPS tracked eyebrows, middle-dot strings, arrows on links, mono data labels, numbered markers | None used. Stage names are words; numerals are tabular sans; the five-stage thread is a real sequence |
| Hero with a big number and a gradient | No hero. The first thing shown is what needs the owner |
| Fade-and-slide entrances, hover transitions everywhere | Motion only answers an action (status change, opening the drawer); reduced motion respected |
Changed after the review: the first draft used a monospace face for codes and task ids (a listed tell); codes now use the sans with tabular figures, and monospace is kept only for commit hashes.

## The three directions
| | A: 日誌 (the ledger) | B: 審查桌 (the review desk) | C: 結構圖 (the trace map) |
|---|---|---|---|
| Organizing idea | Time. One stream of what happened and what is waiting, grouped by day | Decision. A queue of what needs the owner, with the evidence and the consequence next to it | Structure. Five lanes from requirement to decision, items linked by what they depend on |
| Home | Stream across all projects with a "needs you" band on top | Inbox of decisions, blocks and failures; first item open | Map of every live work item against the five lanes |
| Project | The project's own stream plus requirements grouped with their threads | Filterable table with a side panel for operations and the chain | The project's lane map with a drawer |
| Work item | A page with a status path, dependencies and a log of its runs and evidence | A side panel that shows only the moves that are allowed and why the others are not | Select a node: a drawer with the thread and the actions |
| Agents | Who is doing what now, then each agent's recent runs | A table of runs with the selected run's chain beside it | One swimlane per agent, recent runs in time order |
| Gate | Decision card inline in the stream | The main workflow: evidence read, consequence shown, then decide (keyboard: j/k, a/r) | The gate as a node: its chain and what it unblocks in a dialog |
| Best for | Reading the story; low effort at the end of the day | Clearing decisions fast | Seeing why something is stuck; planning |
| Risk | Slow to scan many projects | Weak at seeing the whole | Dense on small screens |

Mature basics stay everywhere: familiar lists, filters, a status menu, confirmation before a decision, undo for status changes, keyboard operation, visible focus.

## Status of this prototype (V2.1 lab)
- Run on port 3101 with the separate `draupnir_design` database and API on 5081. The V2 acceptance environment (PostgreSQL 55432, API 5080, Web 3000) is untouched.
- Every direction has the five views, light and dark, and was checked with Playwright: screenshots, axe (WCAG 2 A/AA, serious and critical), horizontal-overflow check. Rounds 1 to 3 per direction are in `acceptance/screens/draupnir-design/{a,b,c}/round*/`.
- Fixes made across rounds: a CSS specificity bug that stripped styles from buttons and spacing, ARIA table structure in C, mobile overflow in A, duplicated text in the trace, jargon (internal ids, test-data marker repeated in titles), missing agent attribution in the source data.
- Test data: items titled with the 測試資料 marker (and shown with a badge) exist only so every state is visible; all other rows come from the real Astra delivery records.
- Not done on purpose: the existing product is not redesigned and nothing is written into AstraBookOfThoth until a direction is chosen.

## Independent review outcome
Reviewed by an agent that did not design (report: `acceptance/DESIGN-LAB-INDEPENDENT-REVIEW.md`, evidence = screenshots). Ranking: B first, C second, A third.
Fixed afterwards: B agents result badge wrapping and clipped column; C mobile map unusable; C gate shown without its decision flow (dialog now screenshotted, `c/round6/gate-dialog-light.png`); A warm beige callout (now neutral surface); A repeated "verified, 0 work items" lines; slashed zero digits.
Still open (design choices for the owner, not bugs): all three share one palette so Astra identity is modest (wordmark plus the five-station glyph); A's gate page stacks several live approve/reject pairs.
