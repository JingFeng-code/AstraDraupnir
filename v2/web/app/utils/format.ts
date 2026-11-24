/** 日期／時間顯示（固定使用 UTC 解析，避免時區造成日期偏移）。 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '—'
  const match = value.match(/^(\d{4})-(\d{2})-(\d{2})/)
  if (!match) return value
  return `${Number(match[1])}/${Number(match[2])}/${Number(match[3])}`
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const pad = (number: number) => String(number).padStart(2, '0')
  return `${date.getFullYear()}/${pad(date.getMonth() + 1)}/${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

/** 相對時間（例如「3 分鐘前」）；無法解析時回原字串。 */
export function relativeTime(value: string | null | undefined, now: number = Date.now()): string {
  if (!value) return '—'
  const time = new Date(value).getTime()
  if (Number.isNaN(time)) return value
  const seconds = Math.round((time - now) / 1000)
  const formatter = new Intl.RelativeTimeFormat('zh-Hant', { numeric: 'auto' })
  const thresholds: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 365 * 24 * 3600],
    ['month', 30 * 24 * 3600],
    ['day', 24 * 3600],
    ['hour', 3600],
    ['minute', 60],
    ['second', 1],
  ]
  for (const [unit, size] of thresholds) {
    if (Math.abs(seconds) >= size || unit === 'second') {
      return formatter.format(Math.round(seconds / size), unit)
    }
  }
  return value
}

/** 取今天的 YYYY-MM-DD（本地時區） */
export function todayIso(): string {
  const date = new Date()
  const pad = (number: number) => String(number).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}
