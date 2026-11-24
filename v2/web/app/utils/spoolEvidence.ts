export interface SpoolAffectedTrace {
  trace: string
  featureId: string
  featureName: string
  matchedFiles: string[]
  tests?: Array<{ path: string; name?: string; command?: string }>
}

export interface SpoolTraceView {
  command: string
  status: 'pass' | 'fail'
  counts: Record<string, number>
  affectedTraces: SpoolAffectedTrace[]
}

/** Parse the JSON string stored in Evidence.data; malformed data is ignored. */
export function parseSpoolEvidence(data: string | null | undefined): SpoolTraceView | null {
  if (!data) return null
  try {
    const value = JSON.parse(data) as Partial<SpoolTraceView>
    if (typeof value.command !== 'string' || (value.status !== 'pass' && value.status !== 'fail')) return null
    if (!value.counts || typeof value.counts !== 'object') return null
    return {
      command: value.command,
      status: value.status,
      counts: value.counts as Record<string, number>,
      affectedTraces: Array.isArray(value.affectedTraces) ? value.affectedTraces as SpoolAffectedTrace[] : [],
    }
  } catch {
    return null
  }
}
