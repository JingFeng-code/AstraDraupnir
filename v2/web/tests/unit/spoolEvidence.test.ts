import { describe, expect, it } from 'vitest'
import { parseSpoolEvidence } from '~/utils/spoolEvidence'

describe('Spool trace evidence', () => {
  it('parses command, status, counts and affected traces', () => {
    const trace = parseSpoolEvidence(JSON.stringify({
      schema: 'spool-result/v1',
      command: 'impact',
      status: 'pass',
      counts: { affectedTraces: 1 },
      affectedTraces: [{ trace: 'a.yaml', featureId: 'a', featureName: 'A', matchedFiles: ['src/a.ts'] }],
    }))
    expect(trace?.command).toBe('impact')
    expect(trace?.status).toBe('pass')
    expect(trace?.counts.affectedTraces).toBe(1)
    expect(trace?.affectedTraces[0]?.featureId).toBe('a')
  })

  it('returns null for missing or malformed data', () => {
    expect(parseSpoolEvidence(null)).toBeNull()
    expect(parseSpoolEvidence('{')).toBeNull()
    expect(parseSpoolEvidence('{"command":"check"}')).toBeNull()
  })
})
