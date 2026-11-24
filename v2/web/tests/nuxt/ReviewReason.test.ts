import { describe, expect, it, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { computed, ref } from 'vue'
import type { HumanGate } from '~/types/api'

const gate: HumanGate = {
  id: 'gate-1', projectId: 'project-1', workItemId: null, externalRef: null,
  title: 'Manual decision', status: 'Pending', decidedBy: null, decidedAt: null, note: null,
}
const decide = vi.fn().mockResolvedValue(undefined)
const workspace = {
  pendingGates: computed(() => [gate]),
  attention: computed(() => []),
  data: ref({ projects: [] }),
  byId: ref({ items: new Map() }),
  busy: ref(false),
  decide,
  traceOf: vi.fn(),
  dependentsOf: vi.fn(() => []),
}
mockNuxtImport('useWorkspace', () => () => workspace)
mockNuxtImport('useRoute', () => () => ({ query: {} }))

const ReviewPage = (await import('~/pages/review.vue')).default

describe('Review Desk no-work-item gate', () => {
  it('requires at least 20 characters before approving', async () => {
    const wrapper = await mountSuspended(ReviewPage)
    const approve = wrapper.findAll('button').find(button => button.text() === '核准')
    expect(approve).toBeTruthy()
    await approve!.trigger('click')

    const note = wrapper.get('#ui-note')
    const confirm = wrapper.findAll('button').find(button => button.text() === '確認核准')
    expect(confirm).toBeTruthy()
    expect(confirm!.attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="review-reason-required"]').text()).toContain('至少 20 字')

    await note.setValue('太短')
    await nextTick()
    expect(confirm!.attributes('disabled')).toBeDefined()
    expect(decide).not.toHaveBeenCalled()

    const reason = '這是沒有工作項目的人工判斷理由，至少二十字。'
    await note.setValue(reason)
    await nextTick()
    expect(confirm!.attributes('disabled')).toBeUndefined()
    await confirm!.trigger('click')
    expect(decide).toHaveBeenCalledWith(gate, true, reason)
    wrapper.unmount()
  })
})
