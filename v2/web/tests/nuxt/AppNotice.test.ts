import { describe, expect, it } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import AppNotice from '~/components/AppNotice.vue'

describe('AppNotice', () => {
  it('錯誤訊息使用 role=alert 並列出細節', async () => {
    const wrapper = await mountSuspended(AppNotice, {
      props: { message: '相依項目未完成，不能設為 Done。', details: ['相依：DEMO-1'] },
    })
    expect(wrapper.attributes('role')).toBe('alert')
    expect(wrapper.text()).toContain('相依項目未完成')
    expect(wrapper.findAll('li')).toHaveLength(1)
  })

  it('成功訊息使用 role=status', async () => {
    const wrapper = await mountSuspended(AppNotice, { props: { tone: 'success', message: '完成' } })
    expect(wrapper.attributes('role')).toBe('status')
  })
})
