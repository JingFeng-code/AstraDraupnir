import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'

const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@draupnir.local'
function requiredAdminPassword(): string {
  const value = process.env.E2E_ADMIN_PASSWORD
  if (!value) throw new Error('E2E_ADMIN_PASSWORD 未設定：請填入被測 API 的管理員密碼。')
  return value
}
const password = requiredAdminPassword()
const base = process.env.E2E_BASE_URL ?? ''

async function go(page: Page, path: string) {
  await page.goto(base ? new URL(path, base).toString() : path)
}

async function login(page: Page) {
  await go(page, '/login')
  await page.getByTestId('login-email').fill(email)
  await page.getByTestId('login-password').fill(password)
  await page.getByTestId('login-submit').click()
  await expect(page).not.toHaveURL(/\/login$/)
  await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible()
}

async function axeSerious(page: Page, label: string) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze()
  const serious = results.violations.filter(v => v.impact === 'serious' || v.impact === 'critical')
  const summary = serious.map(v => `${v.id}（${v.impact}）：${v.help} → ${v.nodes.slice(0, 3).map(n => n.target.join(' ')).join(' | ')}`)
  expect(summary, `${label}\n${summary.join('\n')}`).toEqual([])
}

test.describe('V2.1 系統管理：鍵盤與無障礙', () => {
  test('跳至主要內容、Ctrl+K 導覽、側邊欄記憶、Tab 可達導覽', async ({ page, isMobile }) => {
    await login(page)

    const skip = page.locator('a[href="#main-content"]')
    await expect(skip).toBeVisible()
    await skip.focus()
    await page.keyboard.press('Enter')
    await expect(page.locator('#main-content')).toBeFocused()

    await page.keyboard.press('Control+k')
    const search = page.getByRole('combobox')
    await expect(search).toBeVisible()
    await search.fill('系統設定')
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(/\/settings$/)

    // 設定頁的收合按鈕在桌面与手機都可見（側邊欄本身的按鈕在手機版會隱藏）。
    await page.getByRole('button', { name: '收合', exact: true }).click()
    await expect(page.locator('.ui-app')).toHaveClass(/is-collapsed/)
    await page.reload()
    await expect(page.locator('.ui-app')).toHaveClass(/is-collapsed/)

    // 手機版範圍內的主要導覽收在選單裡，先打開才能用 Tab 到達。
    if (isMobile) await page.getByRole('button', { name: '選單' }).click()
    let reachedNav = false
    for (let i = 0; i < 20 && !reachedNav; i++) {
      await page.keyboard.press('Tab')
      reachedNav = await page.evaluate(() => !!document.activeElement?.closest('nav[aria-label="主要"]'))
    }
    expect(reachedNav, 'Tab 順序應到達主要導覽').toBe(true)
  })

  test('ConfirmDialog 限制焦點並以 Esc 關閉、歸還焦點', async ({ page }) => {
    await login(page)
    await go(page, '/users')
    const tag = `a11y${Date.now().toString(36)}`
    const memberEmail = `${tag}@example.test`
    await page.getByTestId('user-email').fill(memberEmail)
    await page.getByTestId('user-display-name').fill('A11y Member')
    await page.getByTestId('user-password').fill('Member!2026')
    await page.getByTestId('user-submit').click()
    const row = page.getByTestId(`user-row-${memberEmail}`)
    await expect(row).toBeVisible()

    const opener = row.getByRole('button', { name: '停用' })
    await opener.click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    expect(await page.evaluate(() => document.activeElement?.tagName)).toBe('BUTTON')
    for (let i = 0; i < 6; i++) {
      await page.keyboard.press('Tab')
      const inside = await page.evaluate(() => !!document.activeElement?.closest('dialog'))
      expect(inside, `第 ${i + 1} 次 Tab 仍應停留在對話框內`).toBe(true)
    }
    await page.keyboard.press('Escape')
    await expect(dialog).toBeHidden()
    expect(await page.evaluate(() => document.activeElement?.textContent?.trim())).toContain('停用')
  })

  for (const theme of ['dark', 'light'] as const) {
    test(`${theme}：/login 的 axe serious/critical = 0`, async ({ page }) => {
      await page.addInitScript((value) => localStorage.setItem('astra-theme', value), theme)
      await go(page, '/login')
      await expect(page.getByRole('heading', { level: 1 })).toHaveText('登入 Draupnir')
      await axeSerious(page, `${theme} /login`)
    })

    for (const path of ['/users', '/audit', '/settings']) {
      test(`${theme}：${path} 的 axe serious/critical = 0`, async ({ page }) => {
        await page.addInitScript((value) => localStorage.setItem('astra-theme', value), theme)
        await login(page)
        await go(page, path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await axeSerious(page, `${theme} ${path}`)
      })
    }
  }
})
