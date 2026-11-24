import { describe, expect, it } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import WorkItemBoard from '~/components/WorkItemBoard.vue'
import type { WorkItem } from '~/types/api'

function item(overrides: Partial<WorkItem>): WorkItem {
  return {
    id: 'id-1',
    projectId: 'project-1',
    code: 'DEMO-1',
    type: 'Task',
    title: '示範工作',
    description: '',
    status: 'Ready',
    priority: 'P2',
    assigneeKind: 'None',
    assignee: null,
    requirementId: null,
    sprintId: null,
    milestoneId: null,
    estimate: null,
    startDate: null,
    dueDate: null,
    severity: null,
    blockedReason: null,
    ...overrides,
  }
}

describe('WorkItemBoard', () => {
  it('依狀態分欄並顯示卡片', async () => {
    const wrapper = await mountSuspended(WorkItemBoard, {
      props: { items: [item({ id: 'a', code: 'DEMO-1', title: '第一個' }), item({ id: 'b', code: 'DEMO-2', status: 'Done', title: '第二個' })] },
    })
    expect(wrapper.findAll('[data-testid^="board-column-"]')).toHaveLength(6)
    expect(wrapper.get('[data-testid="board-card-DEMO-1"]').text()).toContain('第一個')
    expect(wrapper.get('[data-testid="board-column-Done"]').text()).toContain('第二個')
  })

  it('在 Bug 卡片顯示嚴重度', async () => {
    const target = item({ id: 'a', code: 'BUG-1', type: 'Bug', severity: 'S2', title: '嚴重缺陷' })
    const wrapper = await mountSuspended(WorkItemBoard, { props: { items: [target] } })
    expect(wrapper.get('[data-testid="board-severity-BUG-1"]').text()).toContain('S2')
  })

  it('用鍵盤變更 select 會發出 move 事件', async () => {
    const target = item({ id: 'a', code: 'DEMO-1', status: 'Ready' })
    const wrapper = await mountSuspended(WorkItemBoard, { props: { items: [target] } })
    const select = wrapper.get('[data-testid="board-card-DEMO-1"] select')
    expect(select.attributes('aria-label')).toBe('變更 DEMO-1 狀態')
    await select.setValue('Done')
    await nextTick()
    expect(wrapper.emitted('move')?.[0]).toEqual([target, 'Done'])
  })

  it('拖曳放到其他欄位會發出 move；同欄位不發事件', async () => {
    const target = item({ id: 'a', code: 'DEMO-1', status: 'Ready' })
    const wrapper = await mountSuspended(WorkItemBoard, { props: { items: [target] } })
    const dataTransfer = { getData: () => 'a', setData: () => {}, effectAllowed: 'move' }

    await wrapper.get('[data-testid="board-column-Done"]').trigger('drop', { dataTransfer })
    expect(wrapper.emitted('move')?.[0]).toEqual([target, 'Done'])

    await wrapper.get('[data-testid="board-column-Ready"]').trigger('drop', { dataTransfer })
    expect(wrapper.emitted('move')).toHaveLength(1)
  })
  it('停用正在更新的卡片，保留詳情連結', async () => {
    const target = item({ id: 'a', code: 'DEMO-1', status: 'Ready' })
    const wrapper = await mountSuspended(WorkItemBoard, { props: { items: [target], busyId: 'a' } })
    const card = wrapper.get('[data-testid="board-card-DEMO-1"]')
    expect(card.attributes('draggable')).toBe('false')
    expect(card.get('a').attributes('href')).toBe('/work/a')
    expect(card.get('select').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="board-column-Done"]').trigger('drop', { dataTransfer: { getData: () => 'a' } })
    expect(wrapper.emitted('move')).toBeUndefined()
  })

})
