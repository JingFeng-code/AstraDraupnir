import type {
  AssigneeKind,
  EvidenceKind,
  EvidenceResult,
  GateStatus,
  Priority,
  ProjectStatus,
  RequirementSource,
  RequirementStatus,
  RunSource,
  SprintStatus,
  Verdict,
  WorkStatus,
  WorkType,
} from '~/types/api'

export interface Labeled {
  label: string
  /** Tailwind 背景／文字 class（已確認 AA 對比） */
  className: string
}

export const projectStatusLabels: Record<ProjectStatus, Labeled> = {
  Active: { label: '進行中', className: 'bg-[#e6f0e7] text-[#2f6b3a]' },
  Paused: { label: '暫停', className: 'bg-[#f7ecd9] text-[#8a5300]' },
  Archived: { label: '已封存', className: 'bg-[#ece8e1] text-[#5f6879]' },
}

export const workStatusLabels: Record<WorkStatus, Labeled> = {
  Backlog: { label: '待辦', className: 'bg-[#ece8e1] text-[#5f6879]' },
  Ready: { label: '待開始', className: 'bg-[#e5eaf3] text-[#1f3a68]' },
  InProgress: { label: '進行中', className: 'bg-[#f7ecd9] text-[#8a5300]' },
  InReview: { label: '審查中', className: 'bg-[#ece6f5] text-[#5b3f8c]' },
  Blocked: { label: '阻塞', className: 'bg-[#fbe4e4] text-[#8a1c1c]' },
  Done: { label: '完成', className: 'bg-[#e6f0e7] text-[#2f6b3a]' },
}

export const workTypeLabels: Record<WorkType, string> = {
  Task: '任務',
  Bug: '缺陷',
  Feature: '功能',
}

export const priorityLabels: Record<Priority, string> = {
  P0: 'P0 最高',
  P1: 'P1 高',
  P2: 'P2 中',
  P3: 'P3 低',
}

export const requirementStatusLabels: Record<RequirementStatus, Labeled> = {
  Draft: { label: '草稿', className: 'bg-[#ece8e1] text-[#5f6879]' },
  Approved: { label: '已核准', className: 'bg-[#e5eaf3] text-[#1f3a68]' },
  Implemented: { label: '已實作', className: 'bg-[#f7ecd9] text-[#8a5300]' },
  Verified: { label: '已驗證', className: 'bg-[#e6f0e7] text-[#2f6b3a]' },
}

export const requirementSourceLabels: Record<RequirementSource, string> = {
  UserAuthorized: '使用者授權',
  Repository: '程式庫',
  ClaudeDecision: 'Claude 決策',
  External: '外部來源',
}

export const sprintStatusLabels: Record<SprintStatus, Labeled> = {
  Planned: { label: '規劃中', className: 'bg-[#ece8e1] text-[#5f6879]' },
  Active: { label: '進行中', className: 'bg-[#e6f0e7] text-[#2f6b3a]' },
  Closed: { label: '已結束', className: 'bg-[#e5eaf3] text-[#1f3a68]' },
}

export const assigneeKindLabels: Record<AssigneeKind, string> = {
  None: '未指派',
  Human: '人類',
  Agent: 'Agent',
}

export const gateStatusLabels: Record<GateStatus, Labeled> = {
  Pending: { label: '待批准', className: 'bg-[#f7ecd9] text-[#8a5300]' },
  Approved: { label: '已批准', className: 'bg-[#e6f0e7] text-[#2f6b3a]' },
  Rejected: { label: '已駁回', className: 'bg-[#fbe4e4] text-[#8a1c1c]' },
}

export const runSourceLabels: Record<RunSource, string> = {
  Supervisor: 'Supervisor',
  Gleipnir: 'Gleipnir',
  Manual: '手動',
}

export const evidenceKindLabels: Record<EvidenceKind, string> = {
  Test: '測試',
  Build: '建置',
  Trace: 'Trace',
  Document: '文件',
  Log: '日誌',
}

export const evidenceResultLabels: Record<EvidenceResult, Labeled> = {
  Pass: { label: '通過', className: 'bg-[#e6f0e7] text-[#2f6b3a]' },
  Fail: { label: '失敗', className: 'bg-[#fbe4e4] text-[#8a1c1c]' },
  NotTested: { label: '未測試', className: 'bg-[#ece8e1] text-[#5f6879]' },
  Blocked: { label: '受阻', className: 'bg-[#f7ecd9] text-[#8a5300]' },
}

export const verdictLabels: Record<Verdict, Labeled> = {
  Pass: { label: '通過', className: 'bg-[#e6f0e7] text-[#2f6b3a]' },
  ChangesRequested: { label: '要求修改', className: 'bg-[#fbe4e4] text-[#8a1c1c]' },
}

/** 看板欄位順序 */
export const boardColumns: WorkStatus[] = ['Backlog', 'Ready', 'InProgress', 'InReview', 'Blocked', 'Done']

/** 狀態需要填寫原因（Blocked） */
export function requiresBlockedReason(status: WorkStatus): boolean {
  return status === 'Blocked'
}

/** 從看板 select 的值解析狀態（非法值回 null） */
export function parseWorkStatus(value: unknown): WorkStatus | null {
  return typeof value === 'string' && (boardColumns as string[]).includes(value)
    ? (value as WorkStatus)
    : null
}

/** 看板卡片拖曳時使用的 dataTransfer key */
export const dragDataType = 'application/x-draupnir-work-item'
