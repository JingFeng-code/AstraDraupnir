/** ProblemDetails（RFC 7807）與使用者可讀訊息的對應。 */

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  status: number
  title: string
  detail: string
  fields: Record<string, string[]>
  traceId?: string

  constructor(status: number, title: string, detail: string, fields: Record<string, string[]> = {}, traceId?: string) {
    super(detail)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
    this.fields = fields
    this.traceId = traceId
  }
}

export interface FriendlyProblem {
  message: string
  /** 逐欄位的訊息（已翻成中文） */
  fieldMessages: string[]
  detail: string
}

const detailMessages: Record<string, string> = {
  'All dependencies must be Done.': '相依項目未完成，不能設為 Done。',
  'Reopen completed dependents before reopening this work item.': '此工作項目已有完成的相依項目；請先重新開啟那些項目。',
  'The dependency would create a cycle.': '加入這個相依關係會造成循環，已阻擋。',
  'The dependency already exists.': '這個相依關係已經存在。',
  'A completed item cannot depend on incomplete work.': '已完成的工作項目不能再相依於未完成的項目。',
  'This gate already has a human decision.': '這個 Human Gate 已經有決定了，不能重複批准或駁回。',
  'The project key cannot change after work item codes exist.': '已建立工作項目後，不能修改專案 Key。',
  'A Supervisor run is already associated with another project.': '這筆 Agent 執行已屬於其他專案，不能改掛到這個專案。',
  'The dependency must belong to the same project.': '相依的工作項目必須屬於同一個專案。',
  'Blocked work requires a reason.': '設為「阻塞」時必須填寫原因。',
  'Provide a Supervisor object with a tasks array.': '匯入資料必須是 Supervisor 格式，且包含 tasks 陣列。',
  'Each task requires an id and status.': '每一筆任務都需要 id 與 status。',
  'Invalid or missing request value.': '請求內容格式不正確，請檢查欄位。',
  'Resource not found.': '找不到資料，可能已被刪除。',
  'Invalid credentials.': 'Email 或密碼不正確。',
  'Email and password are required.': '請輸入 Email 與密碼。',
}

const fieldLabels: Record<string, string> = {
  code: '代碼',
  status: '狀態',
  reason: '原因',
  endDate: '結束日期',
  dueDate: '截止日期',
  startDate: '開始日期',
  title: '標題',
  key: '專案 Key',
  name: '名稱',
  email: 'Email',
  password: '密碼',
  page: '頁碼',
  pageSize: '每頁筆數',
  tasks: '任務資料',
  workItemId: '工作項目',
  dependsOnId: '相依項目',
  requirementId: '需求',
  sprintId: 'Sprint',
  milestoneId: 'Milestone',
  body: '內容',
  filter: '篩選條件',
  credentials: '登入資訊',
}

const knownFieldMessages: Record<string, string> = {
  'A status is required.': '請選擇狀態。',
  'A dependency is required.': '請選擇相依的工作項目。',
  'Use {key}-n.': '工作項目代碼必須是「專案 Key-數字」格式。',
  'A due date is required.': '請填寫截止日期。',
  'A title is required.': '請填寫標題。',
  'Blocked work requires a reason.': '設為「阻塞」時必須填寫原因。',
}

function translateFieldMessage(field: string, message: string): string {
  const known = knownFieldMessages[message]
  if (known) return known.replaceAll('{key}', '專案 Key')
  return `${fieldLabels[field] ?? field}：${message}`
}

/** 將 HTTP 狀態與 ProblemDetails 轉成繁體中文訊息。 */
export function friendlyProblem(status: number, body?: Partial<ProblemDetails> | null): FriendlyProblem {
  const detail = typeof body?.detail === 'string' ? body.detail : ''
  const errors = body?.errors ?? {}
  const fieldMessages = Object.entries(errors).flatMap(([field, messages]) =>
    (messages ?? []).map(message => translateFieldMessage(field, message)),
  )

  if (detail && detailMessages[detail]) {
    return { message: detailMessages[detail], fieldMessages, detail }
  }
  if (fieldMessages.length > 0) {
    return { message: fieldMessages[0]!, fieldMessages, detail }
  }
  if (detail) {
    return { message: detail, fieldMessages, detail }
  }

  const fallback: Record<number, string> = {
    400: '請求內容有誤，請檢查輸入後再試一次。',
    401: '登入已過期，請重新登入。',
    403: '沒有權限執行這個操作。',
    404: '找不到資料，可能已被刪除。',
    409: '資料衝突，請重新整理後再試一次。',
    422: '輸入內容無法處理，請檢查欄位。',
    500: '伺服器發生錯誤，請稍後再試。',
    502: '後端服務暫時無法使用，請稍後再試。',
    503: '服務暫時無法使用，請稍後再試。',
  }
  return { message: fallback[status] ?? `操作失敗（HTTP ${status}）。`, fieldMessages, detail }
}

interface FetchErrorLike {
  status?: number
  statusCode?: number
  data?: Partial<ProblemDetails> | null
  message?: string
}

/** ProblemDetails 有時會以未解析的字串送達（例如 application/problem+json），這裡補一次解析。 */
function normalizeBody(data: unknown): Partial<ProblemDetails> | null {
  if (typeof data === 'string') {
    try {
      const parsed = JSON.parse(data) as unknown
      return parsed && typeof parsed === 'object' ? (parsed as Partial<ProblemDetails>) : null
    } catch {
      return null
    }
  }
  return data && typeof data === 'object' ? (data as Partial<ProblemDetails>) : null
}

/** 把 $fetch 的錯誤轉成 ApiError（含中文訊息）。已是 ApiError 時直接回傳，避免二次轉換掉 detail。 */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error
  const source = (error ?? {}) as FetchErrorLike
  const status = source.status ?? source.statusCode ?? 0
  const body = normalizeBody(source.data)
  const friendly = friendlyProblem(status || 500, body)
  return new ApiError(
    status || 500,
    body?.title ?? 'Request failed',
    friendly.message,
    body?.errors ?? {},
    body?.traceId,
  )
}
