import { mkdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'

const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@draupnir.local'
const password = process.env.E2E_ADMIN_PASSWORD ?? 'Draupnir!2026'
const screenshotDir = process.env.E2E_SCREENSHOT_DIR
  ?? 'E:/Astra/.astra-runs/AUTONOMOUS-DELIVERY/acceptance/screens/draupnir'
const queue = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../api/tests/Draupnir.Api.Tests/Fixtures/queue.json', import.meta.url)), 'utf8'),
) as { tasks: { id: string }[] }

mkdirSync(screenshotDir, { recursive: true })

function uniqueKey(): string {
  return Array.from({ length: 6 }, () => String.fromCharCode(65 + Math.floor(Math.random() * 26))).join('')
}

async function assertNoHorizontalScroll(page: Page, label: string, width = 360) {
  await page.setViewportSize({ width, height: 800 })
  await page.waitForLoadState('networkidle')
  const overflow = await page.evaluate(() => {
    const doc = document.documentElement
    return Math.max(doc.scrollWidth, document.body.scrollWidth) - window.innerWidth
  })
  expect(overflow, `${label} 在 ${width}px 出現水平捲動（溢出 ${overflow}px）`).toBeLessThanOrEqual(1)
}

async function assertAxeClean(page: Page, label: string) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze()
  const summary = results.violations.map(v => `${v.id}（${v.impact}）：${v.help}\n  ${v.nodes.slice(0, 3).map(n => n.target.join(' ')).join('\n  ')}`)
  expect(summary, `${label}\n${summary.join('\n')}`).toEqual([])
}

test.describe('Draupnir V2 交付流程（真實 API + PostgreSQL）', () => {
  test('登入 → 建專案／需求／工作項目 → 相依擋 Done → 匯入 → 批准 Gate → 產生報告', async ({ page, isMobile }, testInfo) => {
    const key = uniqueKey()
    const projectName = `E2E 專案 ${key}`
    const shots = (name: string) => join(screenshotDir, `${name}.png`)

    // 1. 登入
    await page.goto('/login')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('登入 Draupnir')
    await assertAxeClean(page, '登入頁')
    await assertNoHorizontalScroll(page, '登入頁')
    await page.setViewportSize(isMobile ? { width: 360, height: 740 } : { width: 1280, height: 900 })
    await page.getByTestId('login-email').fill(email)
    await page.getByTestId('login-password').fill(password)
    await page.getByTestId('login-submit').click()
    await expect(page).toHaveURL(/\/projects$/)

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('專案')
    if (!isMobile) await page.screenshot({ path: shots('01-projects'), fullPage: true })
    await assertAxeClean(page, '專案列表')
    await assertNoHorizontalScroll(page, '專案列表')
    await page.setViewportSize(isMobile ? { width: 360, height: 740 } : { width: 1280, height: 900 })

    // 2. 建立專案
    await page.getByTestId('new-project').click()
    await page.getByTestId('project-key').fill(key)
    await page.getByTestId('project-name').fill(projectName)
    await page.getByTestId('project-description').fill('Playwright E2E 建立的合成專案')
    await page.getByTestId('project-submit').click()
    await expect(page).toHaveURL(/\/projects\/[0-9a-f-]{36}$/)
    const projectId = page.url().split('/').pop()!
    await expect(page.getByRole('heading', { level: 1 })).toContainText(projectName)
    await expect(page.getByTestId('dashboard-count-Ready')).toBeVisible()

    // 3. 需求
    await page.getByRole('link', { name: '需求', exact: true }).click()
    await expect(page).toHaveURL(new RegExp(`/projects/${projectId}/requirements$`))
    await page.getByTestId('new-requirement').click()
    await page.getByTestId('requirement-title').fill('E2E 需求：建立交付流程')
    await page.getByTestId('requirement-body').fill('由 Playwright 建立，驗證需求與工作項目。')
    await page.getByTestId('requirement-submit').click()
    await expect(page.locator('table')).toContainText('E2E 需求：建立交付流程')

    // 4. 工作項目：兩筆
    await page.getByRole('link', { name: '工作項目', exact: true }).click()
    await page.getByTestId('new-work-item').click()
    await page.getByTestId('work-item-title').fill('第一個工作（相依來源）')
    await page.getByTestId('work-item-submit').click()
    await expect(page.getByTestId(`work-item-row-${key}-1`)).toBeVisible()

    await page.getByTestId('new-work-item').click()
    await page.getByTestId('work-item-title').fill('第二個工作（相依者）')
    await page.getByTestId('work-item-submit').click()
    await expect(page.getByTestId(`work-item-row-${key}-2`)).toBeVisible()

    // 4b. 建立一個 Bug 並指定嚴重度，確認表單映射與列表顯示
    await page.getByTestId('new-work-item').click()
    await page.getByTestId('work-item-type').selectOption('Bug')
    await page.getByTestId('work-item-severity').selectOption('S2')
    await page.getByTestId('work-item-title').fill('E2E 嚴重缺陷')
    await page.getByTestId('work-item-submit').click()
    await expect(page.getByTestId(`work-item-row-${key}-3`)).toBeVisible()
    await expect(page.getByTestId(`work-item-severity-${key}-3`)).toContainText('S2')

    // 5. 加入相依：第二個工作相依於第一個
    const secondRow = page.getByTestId(`work-item-row-${key}-2`)
    await secondRow.getByTestId(`dependency-select-${key}-2`).selectOption({ label: `${key}-1` })
    await expect(secondRow).toContainText(`${key}-1`)

    // 6. 嘗試把相依者移到 Done → 409，顯示中文訊息
    await page.getByTestId(`status-select-${key}-2`).selectOption('Done')
    await expect(page.getByTestId('action-error')).toContainText('相依項目未完成，不能設為 Done。')

    // 7. 看板（滑鼠拖曳與鍵盤 select 都在同一元件）
    await page.getByTestId('view-board').click()
    await expect(page.getByRole('list', { name: '工作項目看板' })).toBeVisible()
    await expect(page.getByTestId(`board-card-${key}-1`)).toBeVisible()
    if (!isMobile) await page.screenshot({ path: shots('03-board'), fullPage: true })
    await assertNoHorizontalScroll(page, '工作項目看板')
    await page.setViewportSize(isMobile ? { width: 360, height: 740 } : { width: 1280, height: 900 })

    // 8. 匯入 Supervisor queue.json
    await page.getByRole('link', { name: '匯入 Supervisor', exact: true }).click()
    // 每次執行使用唯一外部任務 id，避免 (Source, ExternalTaskId, Attempt) 唯一鍵與其他專案衝突
    const payload = { ...queue, tasks: queue.tasks.map(task => ({ ...task, id: `${task.id}-${key}` })) }
    await page.getByTestId('import-payload').fill(JSON.stringify(payload, null, 2))
    await page.getByTestId('import-submit').click()
    await expect(page.getByTestId('import-result')).toContainText('AgentRun 1')
    await expect(page.getByTestId('import-result')).toContainText('Gate 1')

    // 9. Agent 執行可見
    await page.getByRole('link', { name: 'Agent 執行', exact: true }).click()
    await expect(page.locator('table')).toContainText('EXAMPLE-01')

    // 10. 批准 Human Gate（需確認對話框）
    await page.getByRole('link', { name: 'Human Gate', exact: true }).click()
    const gateRow = page.getByRole('row').filter({ hasText: 'Update a local greeter test' })
    await expect(gateRow).toBeVisible()
    if (!isMobile) await page.screenshot({ path: shots('04-gates'), fullPage: true })
    await gateRow.getByRole('button', { name: '批准' }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    await expect(dialog).toContainText('批准這個 Human Gate？')
    await assertAxeClean(page, 'Gate 確認對話框')
    await dialog.getByTestId('gate-note').fill('E2E 批准')
    await dialog.getByRole('button', { name: '確認批准' }).click()
    await expect(dialog).toBeHidden()
    await expect(gateRow).toContainText('已批准')
    await assertNoHorizontalScroll(page, 'Human Gate')

    // 11. 產生交付報告並檢視 Markdown
    await page.getByRole('link', { name: '交付報告', exact: true }).click()
    await page.getByTestId('report-generate').click()
    const markdown = page.getByTestId('report-markdown')
    await expect(markdown).toBeVisible()
    await expect(markdown).toContainText('## Progress')
    if (!isMobile) await page.screenshot({ path: shots('05-report'), fullPage: true })
    await assertAxeClean(page, '交付報告')
    await assertNoHorizontalScroll(page, '交付報告')

    // 12. Dashboard 截圖（回到總覽）
    await page.getByRole('link', { name: '總覽', exact: true }).click()
    await expect(page.getByTestId('dashboard-count-Done')).toBeVisible()
    if (!isMobile) await page.screenshot({ path: shots('02-dashboard'), fullPage: true })
    await assertAxeClean(page, 'Dashboard')
    await assertNoHorizontalScroll(page, 'Dashboard')

    // 13. 重新登入狀態：回首頁仍能看到專案卡
    await page.getByRole('link', { name: '專案', exact: true }).click()
    await expect(page.getByTestId(`project-card-${key}`)).toBeVisible()

    await testInfo.attach('e2e-summary', {
      body: JSON.stringify({ projectId, key, projectName, tasks: queue.tasks.length }, null, 2),
      contentType: 'application/json',
    })
  })
})
