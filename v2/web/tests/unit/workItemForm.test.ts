import { describe, expect, it } from 'vitest'
import { requirementProgress, severityPayload, severityRank } from '~/utils/workItemForm'
import type { WorkItemSeverity } from '~/types/api'
import { severityOptions } from '~/utils/labels'

describe('work item severity form mapping', () => {
  it('keeps S1-S4 only for a Bug', () => {
    for (const severity of severityOptions) expect(severityPayload('Bug', severity)).toBe(severity)
    expect(severityPayload('Bug', 'S9')).toBeNull()
    expect(severityPayload('Bug', '')).toBeNull()
    expect(severityPayload('Task', 'S2')).toBeNull()
    expect(severityPayload('Feature', 'S1')).toBeNull()
  })
})

describe('work item filters and requirement progress', () => {
  it('orders S1 through S4 ahead of unspecified severity', () => {
    const levels: (WorkItemSeverity | null)[] = [null, 'S3', 'S1', 'S4', 'S2']
    expect(levels.sort((a, b) => severityRank(a) - severityRank(b))).toEqual(['S1', 'S2', 'S3', 'S4', null])
  })

  it('derives partial and blocked delivery without rewriting requirement status', () => {
    const progress = requirementProgress([
      { requirementId: 'a', status: 'Done' }, { requirementId: 'a', status: 'Ready' },
      { requirementId: 'b', status: 'Done' }, { requirementId: 'b', status: 'Blocked' },
      { requirementId: 'c', status: 'Done' }, { requirementId: null, status: 'Blocked' },
    ])
    expect(progress.get('a')).toMatchObject({ total: 2, done: 1, status: 'Partial' })
    expect(progress.get('b')).toMatchObject({ total: 2, status: 'Blocked' })
    expect(progress.get('c')?.status).toBeUndefined()
    expect(progress.size).toBe(3)
  })
})
