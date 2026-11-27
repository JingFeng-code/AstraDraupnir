import { describe, expect, it } from 'vitest'
import { formatDate, formatDateTime, relativeTime, todayIso } from '~/utils/format'

describe('日期格式', () => {
  it('formatDate 只取日期部分，不因時區偏移', () => {
    expect(formatDate('2026-11-24')).toBe('2026/11/24')
    expect(formatDate('2026-11-24T23:30:00+00:00')).toBe('2026/11/24')
    expect(formatDate(null)).toBe('—')
  })

  it('formatDateTime 產生可讀時間', () => {
    expect(formatDateTime('2026-11-24T01:05:00Z')).toMatch(/^2026\/11\/24 \d{2}:\d{2}$/)
    expect(formatDateTime(undefined)).toBe('—')
  })

  it('relativeTime 以中文相對時間表示', () => {
    const now = Date.parse('2026-11-24T12:00:00Z')
    expect(relativeTime('2026-11-24T11:58:00Z', now)).toContain('分鐘')
    expect(relativeTime('2026-11-23T12:00:00Z', now)).toContain('天')
  })

  it('todayIso 是 YYYY-MM-DD', () => {
    expect(todayIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })
})
