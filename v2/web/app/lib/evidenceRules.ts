import type { HumanGate } from '~/types/api'
import type { Trace } from '~/lib/engineering'

/**
 * Direction D: honest data rules.
 * These rules are derived inside the design environment. They are NOT definitions from the Supervisor.
 */

export type GateEvidenceState = 'satisfied' | 'missing' | 'failed' | 'manual'

export interface GateEvidence {
  state: GateEvidenceState
  /** what the rule asked for, as readable lines with their outcome */
  checks: { label: string, ok: boolean, detail: string }[]
  headline: string
  canApprove: boolean
  /** approval is possible only as a recorded manual exception with a written reason */
  needsException: boolean
  /** no work item: the API requires a written human reason (at least 20 characters) */
  requiresReason: boolean
}

export function gateEvidence(gate: HumanGate, trace: Trace | null): GateEvidence {
  if (!trace) {
    return {
      state: 'manual',
      checks: [{ label: '沒有對應的工作項目', ok: true, detail: '這是人工判斷的決定；核准時必須寫明至少 20 字理由。' }],
      headline: '人工判斷，沒有必要證據可驗證',
      canApprove: true,
      needsException: false,
      requiresReason: true,
    }
  }

  // Only the latest attempt of each source task is required evidence. The work item aggregates
  // the per-task results; older attempts remain in the trace but must not block a successful retry.
  const latestBySource = new Map<string, typeof trace.runs[number]>()
  for (const run of trace.runs) {
    const key = `${run.source}|${run.sourceInstance ?? ''}|${run.externalTaskId}`
    const current = latestBySource.get(key)
    if (!current || run.attempt > current.attempt ||
      (run.attempt === current.attempt && String(run.startedAt ?? '') > String(current.startedAt ?? ''))) {
      latestBySource.set(key, run)
    }
  }
  const latestRunIds = new Set([...latestBySource.values()].map(run => run.id))
  const latestEvidence = trace.evidence.filter(e => !e.agentRunId || latestRunIds.has(e.agentRunId))
  const latestReviews = trace.reviews.filter(r => !r.agentRunId || latestRunIds.has(r.agentRunId))
  const updatedAt = trace.item.updatedAt ? Date.parse(trace.item.updatedAt) : null
  const usedEvidence = latestEvidence.filter(e => updatedAt === null || Date.parse(e.recordedAt) >= updatedAt)
  const stale = latestEvidence.length - usedEvidence.length
  const passes = usedEvidence.filter(e => e.result === 'Pass')
  const fails = usedEvidence.filter(e => e.result === 'Fail')
  const changes = latestReviews.filter(r => r.verdict === 'ChangesRequested')
  const checks = [
    { label: '至少一筆通過的證據', ok: passes.length > 0, detail: passes.length ? `${passes.length} 筆通過` : '還沒有任何通過的證據' },
    { label: '沒有失敗的證據', ok: fails.length === 0, detail: fails.length ? `${fails.length} 筆失敗` : '沒有失敗' },
    { label: '審查沒有要求修改', ok: changes.length === 0, detail: changes.length ? `${changes.length} 次要求修改` : '沒有未解決的修改要求' },
    { label: '證據未早於工作項目最後更新', ok: stale === 0, detail: stale ? `${stale} 筆過期證據不列入判斷` : '使用的證據都在最後更新之後' },
  ]
  const failed = fails.length > 0 || changes.length > 0
  const missing = passes.length === 0
  if (!failed && !missing) {
    return { state: 'satisfied', checks, headline: '必要證據齊全', canApprove: true, needsException: false, requiresReason: false }
  }
  return {
    state: failed ? 'failed' : 'missing',
    checks,
    headline: failed ? '有失敗的證據或未解決的修改要求' : (stale ? '最新嘗試的通過證據已過期' : '缺少必要證據'),
    canApprove: false,
    needsException: true,
    requiresReason: false,
  }
}

/** The raw records name an agent "Unknown" when the original record did not say who ran the task. */
export const agentName = (name: string) => (name === 'Unknown' || !name ? '未確認' : name)

/** Every run in this environment is an imported snapshot. Nothing here is a live connection. */
export const SNAPSHOT_NOTE = '匯入快照，不是即時連線'

export function newest(times: (string | null | undefined)[]): string | null {
  const t = times.filter(Boolean) as string[]
  return t.length ? t.sort().at(-1)! : null
}

export const clock = (iso?: string | null) => (iso ? new Date(iso).toLocaleString('zh-TW', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }) : '無紀錄')
