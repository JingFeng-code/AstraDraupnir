import type { AgentRun, Evidence, HumanGate, Project, Requirement, Review, WorkItem, WorkStatus } from '~/types/api'

/** Words the interface uses for work states; one vocabulary in every direction. */
export const STATUS_LABEL: Record<WorkStatus, string> = {
  Backlog: '待排',
  Ready: '可開始',
  InProgress: '進行中',
  InReview: '審查中',
  Blocked: '被擋住',
  Done: '已完成',
}
export const STATUS_ORDER: WorkStatus[] = ['Backlog', 'Ready', 'InProgress', 'InReview', 'Done']

/** Visual kind of a status: drives shape and color together (never color alone). */
export type Tone = 'proven' | 'moving' | 'needs' | 'fault' | 'idle'
export const STATUS_TONE: Record<WorkStatus, Tone> = {
  Backlog: 'idle', Ready: 'idle', InProgress: 'moving', InReview: 'moving', Blocked: 'fault', Done: 'proven',
}
export const RUN_LABEL: Record<string, string> = {
  DONE: '已完成', IMPLEMENTING: '實作中', REVIEWING: '審查中', VERIFYING: '驗證中', REWORKING: '修正中',
  CHANGES_REQUESTED: '要求修改', WAITING_HUMAN: '等你決定', AGENT_LOST: '連線中斷', READY: '待派工', BLOCKED_CONFIG: '設定錯誤',
}
export function runTone(status: string): Tone {
  if (status === 'DONE') return 'proven'
  if (status === 'WAITING_HUMAN') return 'needs'
  if (['AGENT_LOST', 'BLOCKED_CONFIG', 'CHANGES_REQUESTED', 'NEEDS_ARBITRATION'].includes(status)) return 'fault'
  if (['IMPLEMENTING', 'REVIEWING', 'VERIFYING', 'REWORKING'].includes(status)) return 'moving'
  return 'idle'
}
export const TYPE_LABEL = { Task: '任務', Bug: '缺陷', Feature: '功能' } as const
export const SEVERITY_LABEL: Record<string, string> = { S1: '嚴重度 1', S2: '嚴重度 2', S3: '嚴重度 3', S4: '嚴重度 4' }

export const TEST_MARK = '測試資料'
export const isTestData = (text?: string | null) => !!text && text.includes(TEST_MARK)

/** Stops of the trace thread, in order. */
export const STOPS = ['需求', '工作', '執行', '證據', '決定'] as const
export type StopState = 'proven' | 'moving' | 'needs' | 'fault' | 'missing' | 'na'
export interface Stop { name: (typeof STOPS)[number], state: StopState, note: string }
export interface Trace {
  stops: Stop[]
  requirement: Requirement | null
  item: WorkItem
  runs: AgentRun[]
  evidence: Evidence[]
  reviews: Review[]
  gate: HumanGate | null
}
export const STOP_STATE_LABEL: Record<StopState, string> = {
  proven: '已確認', moving: '進行中', needs: '等你決定', fault: '有問題', missing: '還沒有', na: '不需要',
}

export function buildTrace(
  item: WorkItem,
  parts: { requirements: Map<string, Requirement>, runs: AgentRun[], evidence: Evidence[], reviews: Review[], gates: HumanGate[] },
): Trace {
  const requirement = item.requirementId ? parts.requirements.get(item.requirementId) ?? null : null
  const runs = parts.runs.filter(r => r.workItemId === item.id)
  const runIds = new Set(runs.map(r => r.id))
  const evidence = parts.evidence.filter(e => e.workItemId === item.id || (e.agentRunId && runIds.has(e.agentRunId)))
  const reviews = parts.reviews.filter(r => r.workItemId === item.id || (r.agentRunId && runIds.has(r.agentRunId)))
  const gate = parts.gates.find(g => g.workItemId === item.id) ?? null
  const latest = runs[0]

  const req: Stop = !requirement
    ? { name: '需求', state: 'missing', note: '沒有對應的需求' }
    : (requirement.status === 'Verified' || requirement.status === 'Implemented')
        ? { name: '需求', state: 'proven', note: `${requirement.code} 已${requirement.status === 'Verified' ? '驗證' : '實作'}` }
        : { name: '需求', state: 'moving', note: `${requirement.code} 尚未完成` }
  const work: Stop = item.status === 'Done'
    ? { name: '工作', state: 'proven', note: '已完成' }
    : item.status === 'Blocked'
      ? { name: '工作', state: 'fault', note: item.blockedReason ?? '被擋住' }
      : item.status === 'Backlog' || item.status === 'Ready'
        ? { name: '工作', state: 'missing', note: STATUS_LABEL[item.status] }
        : { name: '工作', state: 'moving', note: STATUS_LABEL[item.status] }
  const run: Stop = !latest
    ? { name: '執行', state: 'missing', note: '沒有 Agent 執行紀錄' }
    : { name: '執行', state: runTone(latest.status) === 'idle' ? 'missing' : (runTone(latest.status) as StopState), note: `${latest.agent}：${RUN_LABEL[latest.status] ?? latest.status}` }
  const hasFail = evidence.some(e => e.result === 'Fail') || reviews.some(r => r.verdict === 'ChangesRequested')
  const hasPass = evidence.some(e => e.result === 'Pass')
  const ev: Stop = hasFail
    ? { name: '證據', state: 'fault', note: evidence.some(e => e.result === 'Fail') ? '有失敗的證據' : '審查要求修改' }
    : hasPass
      ? { name: '證據', state: 'proven', note: `${evidence.filter(e => e.result === 'Pass').length} 筆通過` }
      : { name: '證據', state: 'missing', note: '還沒有證據' }
  const dec: Stop = !gate
    ? { name: '決定', state: 'na', note: '不需要人工決定' }
    : gate.status === 'Pending'
      ? { name: '決定', state: 'needs', note: '等你決定' }
      : { name: '決定', state: gate.status === 'Approved' ? 'proven' : 'fault', note: gate.status === 'Approved' ? '已核准' : '已駁回' }
  return { stops: [req, work, run, ev, dec], requirement, item, runs, evidence, reviews, gate }
}

/** How far the thread can be trusted: share of applicable stops that are proven. */
export function trust(trace: Trace) {
  const applicable = trace.stops.filter(s => s.state !== 'na')
  const proven = applicable.filter(s => s.state === 'proven').length
  return { proven, total: applicable.length }
}

export function ago(iso?: string | null, now = Date.now()): string {
  if (!iso) return ''
  const s = Math.max(0, (now - new Date(iso).getTime()) / 1000)
  if (s < 90) return '剛剛'
  if (s < 3600) return `${Math.round(s / 60)} 分鐘前`
  if (s < 86400) return `${Math.round(s / 3600)} 小時前`
  return `${Math.round(s / 86400)} 天前`
}

export function dayLabel(iso: string, now = new Date()): string {
  const d = new Date(iso)
  const same = (a: Date, b: Date) => a.toDateString() === b.toDateString()
  if (same(d, now)) return '今天'
  const y = new Date(now); y.setDate(y.getDate() - 1)
  return same(d, y) ? '昨天' : `${d.getMonth() + 1} 月 ${d.getDate()} 日`
}

export const projectOf = (projects: Project[], id: string | null | undefined) => projects.find(p => p.id === id)

/** Titles carry the 測試資料 marker in text; the interface shows it as a badge instead of repeating it. */
export const clean = (title: string) => title.replace(/（測試資料）/g, '').trim()
/** Internal source refs (ha:HA-07, supervisor:T06b) read as jargon; show the bare id. */
export const refLabel = (ref?: string | null) => (ref ? ref.replace(/^(ha|supervisor|design):/, '') : '')
