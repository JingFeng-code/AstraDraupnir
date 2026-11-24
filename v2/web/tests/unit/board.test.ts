import { describe, expect, it } from 'vitest'
import {
  boardColumns,
  dragDataType,
  parseWorkStatus,
  priorityLabels,
  requiresBlockedReason,
  workStatusLabels,
  workTypeLabels,
} from '~/utils/labels'

describe('看板狀態轉換', () => {
  it('看板欄位順序固定為六個狀態', () => {
    expect(boardColumns).toEqual(['Backlog', 'Ready', 'InProgress', 'InReview', 'Blocked', 'Done'])
  })

  it('parseWorkStatus 只接受合法狀態', () => {
    expect(parseWorkStatus('Done')).toBe('Done')
    expect(parseWorkStatus('Blocked')).toBe('Blocked')
    expect(parseWorkStatus('done')).toBeNull()
    expect(parseWorkStatus('')).toBeNull()
    expect(parseWorkStatus(null)).toBeNull()
    expect(parseWorkStatus(42)).toBeNull()
  })

  it('只有 Blocked 需要填寫原因', () => {
    expect(boardColumns.filter(status => requiresBlockedReason(status))).toEqual(['Blocked'])
  })

  it('拖曳資料使用固定 MIME key', () => {
    expect(dragDataType).toBe('application/x-draupnir-work-item')
  })

  it('狀態、類型、優先級都有中文標籤', () => {
    for (const status of boardColumns) expect(workStatusLabels[status].label.length).toBeGreaterThan(0)
    expect(workTypeLabels.Task).toBe('任務')
    expect(priorityLabels.P0).toContain('P0')
  })
})
