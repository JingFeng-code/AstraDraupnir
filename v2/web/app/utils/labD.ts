import type { HumanGate } from '~/types/api'
import type { Trace } from '~/utils/lab'

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
}

export function gateEvidence(gate: HumanGate, trace: Trace | null): GateEvidence {
  if (!trace) {
    return {
      state: 'manual',
      checks: [{ label: '沒有對應的工作項目', ok: true, detail: '這是人工判斷的決定，沒有可自動驗證的必要證據。' }],
      headline: '人工判斷，沒有必要證據可驗證',
      canApprove: true,
      needsException: false,
    }
  }
  const passes = trace.evidence.filter(e => e.result === 'Pass')
  const fails = trace.evidence.filter(e => e.result === 'Fail')
  const changes = trace.reviews.filter(r => r.verdict === 'ChangesRequested')
  const checks = [
    { label: '至少一筆通過的證據', ok: passes.length > 0, detail: passes.length ? `${passes.length} 筆通過` : '還沒有任何通過的證據' },
    { label: '沒有失敗的證據', ok: fails.length === 0, detail: fails.length ? `${fails.length} 筆失敗` : '沒有失敗' },
    { label: '審查沒有要求修改', ok: changes.length === 0, detail: changes.length ? `${changes.length} 次要求修改` : '沒有未解決的修改要求' },
  ]
  const failed = fails.length > 0 || changes.length > 0
  const missing = passes.length === 0
  if (!failed && !missing) return { state: 'satisfied', checks, headline: '必要證據齊全', canApprove: true, needsException: false }
  return {
    state: failed ? 'failed' : 'missing',
    checks,
    headline: failed ? '有失敗的證據或未解決的修改要求' : '缺少必要證據',
    canApprove: false,
    needsException: true,
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
