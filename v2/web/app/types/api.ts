/** 與 Draupnir V2 API 對應的資料型別（JSON 使用 camelCase、enum 為字串）。 */
export type ProjectStatus = 'Active' | 'Paused' | 'Archived'
export type Priority = 'P0' | 'P1' | 'P2' | 'P3'
export type RequirementStatus = 'Draft' | 'Approved' | 'Implemented' | 'Verified'
export type RequirementSource = 'UserAuthorized' | 'Repository' | 'ClaudeDecision' | 'External'
export type WorkType = 'Task' | 'Bug' | 'Feature'
export type WorkItemSeverity = 'S1' | 'S2' | 'S3' | 'S4'
export type WorkStatus = 'Backlog' | 'Ready' | 'InProgress' | 'InReview' | 'Blocked' | 'Done'
export type AssigneeKind = 'None' | 'Human' | 'Agent'
export type SprintStatus = 'Planned' | 'Active' | 'Closed'
export type RunSource = 'Supervisor' | 'Gleipnir' | 'Manual'
export type EvidenceKind = 'Test' | 'Build' | 'Trace' | 'Document' | 'Log'
export type EvidenceResult = 'Pass' | 'Fail' | 'NotTested' | 'Blocked'
export type Verdict = 'Pass' | 'ChangesRequested'
export type GateStatus = 'Pending' | 'Approved' | 'Rejected'

export type UserRole = 'Admin' | 'Member'

export interface User {
  id: string
  email: string
  displayName: string
  role: UserRole
  isActive: boolean
}

export interface ProjectMember {
  id: string
  email: string
  displayName: string
  role: UserRole
  isActive: boolean
}

export interface Project {
  id: string
  key: string
  name: string
  description: string
  status: ProjectStatus
  repoPath: string | null
  createdAt: string
  updatedAt: string
}

export interface Requirement {
  id: string
  projectId: string
  code: string
  title: string
  body: string
  priority: Priority
  status: RequirementStatus
  source: RequirementSource
}

export interface WorkItem {
  id: string
  projectId: string
  code: string
  type: WorkType
  title: string
  description: string
  status: WorkStatus
  priority: Priority
  assigneeKind: AssigneeKind
  assignee: string | null
  requirementId: string | null
  sprintId: string | null
  milestoneId: string | null
  estimate: number | null
  startDate: string | null
  dueDate: string | null
  severity: WorkItemSeverity | null
  blockedReason: string | null
}

export interface Sprint {
  id: string
  projectId: string
  name: string
  startDate: string
  endDate: string
  goal: string
  status: SprintStatus
}

export interface Milestone {
  id: string
  projectId: string
  name: string
  dueDate: string
  status: string
}

export interface WorkItemDependency {
  workItemId: string
  dependsOnId: string
}

export interface AgentRun {
  id: string
  projectId: string | null
  workItemId: string | null
  externalTaskId: string
  agent: string
  model: string
  sessionId: string | null
  status: string
  attempt: number
  startedAt: string | null
  finishedAt: string | null
  source: RunSource
}

export interface Evidence {
  id: string
  projectId: string | null
  workItemId: string | null
  agentRunId: string | null
  kind: EvidenceKind
  title: string
  path: string | null
  url: string | null
  result: EvidenceResult
  recordedAt: string
  spoolResultHash: string | null
  data: string | null
}

export interface Review {
  id: string
  workItemId: string | null
  agentRunId: string | null
  reviewer: string
  verdict: Verdict
  findings: unknown
  createdAt: string
}

export interface HumanGate {
  id: string
  projectId: string
  workItemId: string | null
  externalRef: string | null
  title: string
  status: GateStatus
  decidedBy: string | null
  decidedAt: string | null
  note: string | null
}

export interface ReportSummary {
  id: string
  projectId: string
  title: string
  generatedAt: string
  docxPath: string | null
}

export interface Dashboard {
  statusCounts: Record<WorkStatus, number>
  activeSprintCompletionRate: number
  activeSprintDone: number
  activeSprintTotal: number
  blockedItems: WorkItem[]
  pendingGates: HumanGate[]
  recentAgentRuns: AgentRun[]
}

export interface Paged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface ImportResult {
  agentRuns: number
  evidence: number
  reviews: number
  gates: number
}
