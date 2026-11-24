import { describe, expect, it } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import StatusBadge from '~/components/StatusBadge.vue'
import { workStatusLabels } from '~/utils/labels'

describe('StatusBadge', () => {
  it('顯示中文標籤與對應顏色 class', async () => {
    const wrapper = await mountSuspended(StatusBadge, { props: { value: workStatusLabels.Done } })
    expect(wrapper.text()).toBe('完成')
    expect(wrapper.classes()).toContain('bg-[#e6f0e7]')
  })
})
