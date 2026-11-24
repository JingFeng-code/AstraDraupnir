import { describe, expect, it } from 'vitest'
import { agentName, gateEvidence } from '~/lib/evidenceRules'
import type { Trace } from '~/lib/engineering'
import type { Evidence, HumanGate, Review, WorkItem } from '~/types/api'

const gate: HumanGate = {
  id: 'g1', projectId: 'p1', workItemId: 'w1', externalRef: 'supervisor:1', title: 'Gate',
  status: 'Pending', decidedBy: null, decidedAt: null, note: null,
}
const item: WorkItem = {
  id: 'w1', projectId: 'p1', code: 'P-1', type: 'Task', title: 'Work', description: '',
  status: 'InReview', priority: 'P2', assigneeKind: 'None', assignee: null, requirementId: null,
  sprintId: null, milestoneId: null, estimate: null, startDate: null, dueDate: null, severity: null,
  blockedReason: null,
}
const evidence = (result: Evidence['result']): Evidence => ({
  id: `e-${result}`, projectId: 'p1', workItemId: 'w1', agentRunId: null, kind: 'Test', title: 'test',
  path: null, url: null, result, recordedAt: '2026-11-24T00:00:00Z', spoolResultHash: null, data: null,
})
const review = (verdict: Review['verdict']): Review => ({
  id: `r-${verdict}`, workItemId: 'w1', agentRunId: null, reviewer: 'Pi', verdict,
  findings: [], createdAt: '2026-11-24T00:00:00Z',
})
function trace(ev: Evidence[], rv: Review[] = []): Trace {
  return { stops: [], requirement: null, item, runs: [], evidence: ev, reviews: rv, gate }
}

describe('gate evidence rules', () => {
  it('manual when no work item trace exists', () => {
    const result = gateEvidence(gate, null)
    expect(result.state).toBe('manual')
    expect(result.canApprove).toBe(true)
    expect(result.needsException).toBe(false)
  })
  it('satisfied only with a pass and no fail/change request', () => {
    const result = gateEvidence(gate, trace([evidence('Pass')]))
    expect(result.state).toBe('satisfied')
    expect(result.canApprove).toBe(true)
  })
  it('blocks direct approval on failed evidence or changes requested', () => {
    const result = gateEvidence(gate, trace([evidence('Pass'), evidence('Fail')], [review('ChangesRequested')]))
    expect(result.state).toBe('failed')
    expect(result.canApprove).toBe(false)
    expect(result.needsException).toBe(true)
  })
  it('treats missing passes as an exception, never a silent approval', () => {
    const result = gateEvidence(gate, trace([]))
    expect(result.state).toBe('missing')
    expect(result.canApprove).toBe(false)
    expect(result.needsException).toBe(true)
  })
  it('never guesses an Unknown agent name', () => {
    expect(agentName('Unknown')).toBe('未確認')
    expect(agentName('')).toBe('未確認')
    expect(agentName('Pi')).toBe('Pi')
  })
})
