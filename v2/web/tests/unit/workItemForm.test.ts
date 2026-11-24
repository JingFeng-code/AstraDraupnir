import { describe, expect, it } from 'vitest'
import { severityPayload } from '~/utils/workItemForm'
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
