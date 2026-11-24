import { describe, expect, it } from 'vitest'
import { ApiError, friendlyProblem, toApiError } from '~/utils/errors'

describe('friendlyProblem', () => {
  it('把「相依未完成」的 409 轉成中文提示', () => {
    const result = friendlyProblem(409, { status: 409, detail: 'All dependencies must be Done.' })
    expect(result.message).toBe('相依項目未完成，不能設為 Done。')
  })

  it('涵蓋其他已知 409：重複 Gate 決定、循環相依、重複相依', () => {
    expect(friendlyProblem(409, { detail: 'This gate already has a human decision.' }).message)
      .toContain('已經有決定')
    expect(friendlyProblem(409, { detail: 'The dependency would create a cycle.' }).message)
      .toContain('循環')
    expect(friendlyProblem(409, { detail: 'The dependency already exists.' }).message)
      .toContain('已經存在')
  })

  it('400 驗證錯誤會列出中文化欄位訊息', () => {
    const result = friendlyProblem(400, {
      status: 400,
      errors: { code: ['Use DEMO-n.'], reason: ['Blocked work requires a reason.'] },
    })
    expect(result.fieldMessages).toHaveLength(2)
    expect(result.fieldMessages[0]).toContain('代碼')
    expect(result.message).toContain('代碼')
    expect(result.fieldMessages.join(' ')).toContain('阻塞')
  })

  it('401 / 404 / 未知狀態有預設中文訊息', () => {
    expect(friendlyProblem(401).message).toContain('重新登入')
    expect(friendlyProblem(404).message).toContain('找不到資料')
    expect(friendlyProblem(418).message).toContain('HTTP 418')
  })

  it('未知的英文 detail 會原樣顯示（不吞掉後端訊息）', () => {
    expect(friendlyProblem(409, { detail: 'Something unexpected.' }).message).toBe('Something unexpected.')
  })
})

describe('toApiError', () => {
  it('保留 status、fields、traceId，並把 detail 轉成中文', () => {
    const error = toApiError({
      status: 409,
      data: { title: 'Conflict', detail: 'All dependencies must be Done.', traceId: 'trace-1' },
    })
    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(409)
    expect(error.detail).toBe('相依項目未完成，不能設為 Done。')
    expect(error.traceId).toBe('trace-1')
  })

  it('ProblemDetails 以未解析字串送達時仍能轉成中文', () => {
    const error = toApiError({
      status: 409,
      data: JSON.stringify({ title: 'Conflict', detail: 'All dependencies must be Done.', traceId: 'trace-2' }),
    })
    expect(error.detail).toBe('相依項目未完成，不能設為 Done。')
    expect(error.traceId).toBe('trace-2')
  })

  it('已經是 ApiError 時會原樣回傳（避免二次轉換）', () => {
    const original = new ApiError(409, 'Conflict', '相依項目未完成，不能設為 Done。')
    expect(toApiError(original)).toBe(original)
  })

  it('$fetch 的 statusCode 也能解析', () => {
    const error = toApiError({ statusCode: 400, data: { errors: { title: ['A title is required.'] } } })
    expect(error.status).toBe(400)
    expect(error.detail).toContain('請填寫標題')
  })
})
