import { beforeAll, describe, expect, it } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ConfirmDialog from '~/components/ConfirmDialog.vue'

beforeAll(() => {
  const proto = window.HTMLDialogElement?.prototype
  if (proto && typeof proto.showModal !== 'function') {
    proto.showModal = function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '')
    }
    proto.close = function close(this: HTMLDialogElement) {
      this.removeAttribute('open')
      this.dispatchEvent(new Event('close'))
    }
  }
})

describe('ConfirmDialog', () => {
  it('開啟時顯示對話框與標題，關閉時移除', async () => {
    const wrapper = await mountSuspended(ConfirmDialog, {
      props: { open: false, title: '批准這個 Human Gate？', description: '决定会立刻生效。' },
    })
    const dialog = wrapper.get('dialog')
    expect(dialog.attributes('open')).toBeUndefined()

    await wrapper.setProps({ open: true })
    await nextTick()
    expect(dialog.attributes('open')).toBe('')
    expect(dialog.text()).toContain('批准這個 Human Gate？')
    expect(dialog.text()).toContain('决定会立刻生效。')
  })

  it('取消與確認會發出事件，busy 時按鈕停用', async () => {
    const wrapper = await mountSuspended(ConfirmDialog, {
      props: { open: true, title: '示範', busy: true },
    })
    const buttons = wrapper.findAll('button')
    expect(buttons.every(button => button.attributes('disabled') !== undefined)).toBe(true)
    await wrapper.setProps({ busy: false })
    await buttons[0]!.trigger('click')
    expect(wrapper.emitted('update:open')?.[0]).toEqual([false])
    expect(wrapper.emitted('cancel')).toHaveLength(1)
    await buttons[1]!.trigger('click')
    expect(wrapper.emitted('confirm')).toHaveLength(1)
  })
})
