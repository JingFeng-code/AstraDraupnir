/**
 * V2.1 acceptance E2E。**這份 spec 會真的寫資料**（專案、使用者、Gate、報告…）。
 *
 * 規則：
 * 1. 每個建立的標題／顯示名稱都以「測試資料」開頭，UI 才會出現測試資料徽章（A-23）。
 * 2. 請只在可拋棄的資料庫上跑（例如 CI 或 `draupnir_v21_accept`）；它不會清除自己建立的資料，
 *    因為 API 的 DELETE 只會 archive，也沒有刪使用者的端點。
 * 3. 專案 key 與需求代碼都帶隨機值，重跑不會撞號。
 */
import { expect, request as requestFactory, test } from '@playwright/test'

const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@draupnir.local'
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? 'Draupnir!2026'
const unique = () => Array.from({ length: 6 }, () => String.fromCharCode(65 + Math.floor(Math.random() * 26))).join('')

async function adminApi(baseURL: string | undefined) {
  const context = await requestFactory.newContext({ baseURL })
  const login = await context.post('/api/auth/login', { data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD } })
  expect(login.status()).toBe(200)
  return context
}

test('acceptance: users, roles, members, audit, logout', async ({ baseURL }) => {
  const key = `A${unique()}`
  const email = `member-${key.toLowerCase()}@example.test`
  const password = 'Member!2026'
  const admin = await adminApi(baseURL)
  const projectResponse = await admin.post('/api/projects', { data: { key, name: `測試資料 Acceptance ${key}` } })
  expect(projectResponse.status()).toBe(201)
  const projectId = (await projectResponse.json()).id
  const foreign = await admin.post('/api/projects', { data: { key: `B${key.slice(1)}`, name: '測試資料 Foreign project' } })
  expect(foreign.status()).toBe(201)
  const foreignId = (await foreign.json()).id
  const userResponse = await admin.post('/api/users', { data: { email, displayName: '測試資料 Acceptance Member', password, role: 'Member' } })
  expect(userResponse.status()).toBe(201)
  const userId = (await userResponse.json()).id
  const add = await admin.post(`/api/projects/${projectId}/members`, { data: { userId } })
  expect(add.status()).toBe(200)

  const member = await requestFactory.newContext({ baseURL })
  expect((await member.post('/api/auth/login', { data: { email, password } })).status()).toBe(200)
  const listed = await (await member.get('/api/projects')).json()
  expect(listed.some((p: { id: string }) => p.id === projectId)).toBe(true)
  expect((await member.get(`/api/projects/${foreignId}`)).status()).toBe(403)
  expect((await member.post('/api/projects', { data: { key: `C${key.slice(1)}`, name: 'No' } })).status()).toBe(403)

  expect((await admin.patch(`/api/users/${userId}`, { data: { role: 'Admin' } })).status()).toBe(200)
  expect((await (await member.get('/api/auth/me')).json()).role).toBe('Admin')
  expect((await admin.patch(`/api/users/${userId}`, { data: { role: 'Member' } })).status()).toBe(200)
  expect((await admin.delete(`/api/projects/${projectId}/members/${userId}`)).status()).toBe(204)
  expect((await member.get(`/api/projects/${projectId}`)).status()).toBe(403)
  const afterRemove = await (await member.get('/api/projects')).json()
  expect(afterRemove.some((p: { id: string }) => p.id === projectId)).toBe(false)

  const audit = await (await admin.get('/api/audit')).json()
  expect(audit.some((row: { action: string }) => row.action === 'auth.login')).toBe(true)
  expect(audit.some((row: { action: string }) => row.action === 'membership.add')).toBe(true)
  expect(audit.some((row: { action: string }) => row.action === 'membership.remove')).toBe(true)

  const spool = { schema: 'spool-result/v1', command: 'check', status: 'pass', exitCode: 0, findings: [], counts: {} }
  expect((await member.post(`/api/projects/${projectId}/spool-results`, { data: spool })).status()).toBe(403)
  expect((await member.post(`/api/import/supervisor?projectId=${projectId}`, { data: { tasks: [] } })).status()).toBe(403)
  expect((await admin.post(`/api/projects/${projectId}/spool-results`, { data: spool })).status()).toBe(201)
  expect((await admin.post(`/api/import/supervisor?projectId=${projectId}`, { data: { tasks: [] } })).status()).toBe(200)
  const auditAfter = await (await admin.get('/api/audit')).json()
  expect(auditAfter.some((row: { action: string }) => row.action === 'spool.import')).toBe(true)
  expect(auditAfter.some((row: { action: string }) => row.action === 'supervisor.import')).toBe(true)
  for (const row of auditAfter) expect(row.detail).not.toContain('schema')
  for (const row of auditAfter) expect(row.detail).not.toContain('"tasks"')

  expect((await admin.patch(`/api/users/${userId}`, { data: { isActive: false } })).status()).toBe(200)
  expect((await member.get('/api/projects')).status()).toBe(401)
  expect((await admin.post('/api/auth/logout')).status()).toBe(204)
  expect((await admin.get('/api/projects')).status()).toBe(401)
  await admin.dispose(); await member.dispose()
})

test('acceptance: work items, sprint, requirements, import preview, manual gate exception, reject, report', async ({ baseURL, page }) => {
  const key = `P${unique()}`
  const requirementCode = `REQ-${Math.floor(100000 + Math.random() * 900000)}`
  const admin = await adminApi(baseURL)
  const project = await (await admin.post('/api/projects', { data: { key, name: `測試資料 Project ${key}` } })).json()
  const requirement = await (await admin.post(`/api/projects/${project.id}/requirements`, {
    data: { code: requirementCode, title: '測試資料 Initial requirement', body: 'Initial', priority: 'P2', status: 'Draft', source: 'UserAuthorized' },
  })).json()
  const bug = await (await admin.post(`/api/projects/${project.id}/work-items`, {
    data: { code: `${key}-1`, type: 'Bug', severity: 'S2', title: '測試資料 Severity bug', status: 'Ready', priority: 'P1' },
  })).json()
  const task = await (await admin.post(`/api/projects/${project.id}/work-items`, {
    data: { code: `${key}-2`, type: 'Task', title: '測試資料 Dependent task', status: 'Ready', priority: 'P2', requirementId: requirement.id },
  })).json()
  const gate = await (await admin.post(`/api/projects/${project.id}/gates`, { data: { title: '測試資料 No evidence gate', workItemId: bug.id } })).json()
  const rejectGate = await (await admin.post(`/api/projects/${project.id}/gates`, { data: { title: '測試資料 Reject gate', workItemId: task.id } })).json()
  const sprint = await (await admin.post(`/api/projects/${project.id}/sprints`, {
    data: { name: '測試資料 Acceptance sprint', startDate: '2026-11-24', endDate: '2026-11-30', goal: '測試資料 Acceptance', status: 'Active' },
  })).json()
  await admin.post(`/api/projects/${project.id}/milestones`, { data: { name: '測試資料 Acceptance milestone', dueDate: '2026-12-01', status: 'Planned' } })

  await page.goto('/login')
  await page.getByTestId('login-email').fill(ADMIN_EMAIL)
  await page.getByTestId('login-password').fill(ADMIN_PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL('/')

  await page.goto(`/projects/${project.id}/work-items`)
  await expect(page.getByTestId(`work-item-row-${key}-1`)).toContainText('S2')
  await page.getByLabel('嚴重度篩選').selectOption('S2')
  await expect(page.getByTestId(`work-item-row-${key}-1`)).toBeVisible()
  await page.getByRole('button', { name: `編輯 ${key}-1` }).click()
  await page.getByTestId('work-item-title').fill('測試資料 Severity bug edited')
  await page.getByTestId('work-item-submit').click()
  await expect(page.getByTestId(`work-item-row-${key}-1`)).toContainText('測試資料 Severity bug edited')
  await page.getByTestId('view-board').click()
  await expect(page.getByTestId(`board-card-${key}-1`)).toContainText('S2')

  await page.goto(`/projects/${project.id}/requirements`)
  await expect(page.locator('table')).toContainText('Initial requirement')
  await page.getByRole('button', { name: `編輯 ${requirementCode}` }).click()
  await page.getByTestId('requirement-title').fill('測試資料 Edited requirement')
  await page.getByTestId('requirement-submit').click()
  await expect(page.locator('table')).toContainText('測試資料 Edited requirement')
  await page.getByLabel('需求狀態篩選').selectOption('Draft')
  await expect(page.locator('table')).toContainText('測試資料 Edited requirement')

  await page.goto(`/projects/${project.id}/sprints`)
  await expect(page.getByTestId(`sprint-${sprint.id}`)).toBeVisible()
  await expect(page.getByTestId(`sprint-${sprint.id}`)).toContainText('0 / 0')
  await page.getByTestId(`sprint-assign-${sprint.id}`).selectOption(task.id)
  await expect(page.getByTestId(`sprint-${sprint.id}`)).toContainText('0 / 1')
  await page.getByTestId(`sprint-edit-${sprint.id}`).click()
  await page.getByTestId('sprint-goal').fill('測試資料 Acceptance edited')
  await page.getByTestId('sprint-submit').click()
  await expect(page.getByTestId(`sprint-${sprint.id}`)).toContainText('測試資料 Acceptance edited')

  await page.goto(`/projects/${project.id}/import`)
  const taskId = `${key}-IMPORT`
  await page.getByTestId('import-payload').fill(JSON.stringify({ tasks: [{ id: taskId, title: '測試資料 Import preview task', status: 'WAITING_HUMAN', requiresHuman: true, repo: 'sandbox' }] }))
  await expect(page.getByTestId('import-preview')).toContainText('1 個任務')
  await page.getByTestId('import-submit').click()
  await expect(page.getByTestId('import-result')).toContainText('AgentRun 1')
  await expect(page.getByTestId('import-result')).toContainText('本次匯入時間')

  await page.goto(`/projects/${project.id}/gates`)
  await expect(page.getByTestId(`gate-approve-${gate.id}`)).toHaveCount(0)
  await page.getByTestId(`gate-row-${gate.id}`).click()
  await page.getByLabel('例外原因（至少 20 字）').fill('這是驗收測試用的人工例外，已確認必要證據不足。')
  await page.getByLabel('我知道必要證據缺少或未通過').check()
  await page.getByLabel('請輸入「例外」確認').fill('例外')
  await page.getByRole('button', { name: '以人工例外放行' }).click()
  await expect(page.getByTestId(`gate-row-${gate.id}`)).toContainText('已批准')
  await page.getByTestId(`gate-row-${rejectGate.id}`).click()
  await page.getByTestId(`gate-reject-${rejectGate.id}`).click()
  await page.getByTestId('gate-note').fill('Rejected by acceptance')
  await page.getByRole('button', { name: '確認駁回' }).click()
  await expect(page.getByTestId(`gate-row-${rejectGate.id}`)).toContainText('已駁回')

  await page.goto(`/projects/${project.id}/reports`)
  await page.getByTestId('report-generate').click()
  await expect(page.getByTestId('report-markdown')).toContainText('## Progress')
  const reports = await (await admin.get(`/api/projects/${project.id}/reports`)).json()
  const yaml = await admin.get(`/api/reports/${reports[0].id}/stylus.yaml`)
  expect(yaml.status()).toBe(200)
  expect(await yaml.text()).toContain('spec_version')
  await admin.dispose()
})
