import type { WorkItemSeverity, WorkType } from '~/types/api'

/** Only a Bug keeps a severity; the API accepts S1–S4. */
export function severityPayload(type: WorkType, severity: string | null | undefined): WorkItemSeverity | null {
  if (type !== 'Bug') return null
  return severity === 'S1' || severity === 'S2' || severity === 'S3' || severity === 'S4'
    ? severity
    : null
}
