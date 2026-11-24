import type { WorkItem, WorkItemSeverity, WorkType } from '~/types/api'

/** Only a Bug keeps a severity; the API accepts S1–S4. */
export function severityPayload(type: WorkType, severity: string | null | undefined): WorkItemSeverity | null {
  if (type !== 'Bug') return null
  return severity === 'S1' || severity === 'S2' || severity === 'S3' || severity === 'S4'
    ? severity
    : null
}

export function requirementProgress(items: Pick<WorkItem, 'requirementId' | 'status'>[]) {
  const counts = new Map<string, { total: number, done: number, blocked: boolean, status?: 'Partial' | 'Blocked' }>()
  for (const item of items) {
    if (!item.requirementId) continue
    const current = counts.get(item.requirementId) ?? { total: 0, done: 0, blocked: false }
    current.total++
    if (item.status === 'Done') current.done++
    if (item.status === 'Blocked') current.blocked = true
    counts.set(item.requirementId, current)
  }
  for (const current of counts.values()) {
    if (current.blocked) current.status = 'Blocked'
    else if (current.done > 0 && current.done < current.total) current.status = 'Partial'
  }
  return counts
}

export function severityRank(severity: WorkItemSeverity | null) {
  return severity ? Number(severity[1]) : 5
}

export const workTone = {
  Backlog: 'idle', Ready: 'idle', InProgress: 'moving', InReview: 'moving', Blocked: 'fault', Done: 'proven',
} as const

export const severityTone = { S1: 'fault', S2: 'needs', S3: 'moving', S4: 'idle' } as const
