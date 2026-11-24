<script setup lang="ts">
const props = withDefaults(defineProps<{
  open: boolean
  title: string
  description?: string
  confirmLabel?: string
  cancelLabel?: string
  tone?: 'primary' | 'danger'
  busy?: boolean
}>(), {
  description: undefined,
  confirmLabel: '確認',
  cancelLabel: '取消',
  tone: 'primary',
  busy: false,
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  'confirm': []
  'cancel': []
}>()

const dialog = ref<HTMLDialogElement | null>(null)
const titleId = useId()
let previousFocus: HTMLElement | null = null

watch(
  () => props.open,
  async (open) => {
    await nextTick()
    if (!dialog.value) return
    if (open && !dialog.value.open) {
      previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
      dialog.value.showModal()
      dialog.value.querySelector<HTMLButtonElement>('button')?.focus()
    }
    if (!open && dialog.value.open) dialog.value.close()
  },
  { immediate: true },
)

function cancel() {
  emit('update:open', false)
  emit('cancel')
}

function confirm() {
  if (!props.busy) emit('confirm')
}

function onClose() {
  emit('update:open', false)
  if (previousFocus?.isConnected) previousFocus.focus()
  previousFocus = null
}

/** Keep Tab inside the modal even in environments where native dialog focus wrapping is unreliable. */
function onKeydown(event: KeyboardEvent) {
  if (event.key !== 'Tab' || !dialog.value) return
  const focusable = [...dialog.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )]
  if (!focusable.length) return
  const first = focusable[0]!
  const last = focusable[focusable.length - 1]!
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

onBeforeUnmount(() => {
  if (dialog.value?.open) dialog.value.close()
})
</script>

<template>
  <dialog
    ref="dialog"
    class="ui-confirm"
    :aria-labelledby="titleId"
    @keydown="onKeydown"
    @cancel.prevent="cancel"
    @close="onClose"
  >
    <div class="ui-panel ui-confirm-body">
      <h2 :id="titleId" class="ui-h2">{{ title }}</h2>
      <p v-if="description" class="ui-meta ui-confirm-desc">{{ description }}</p>
      <slot />
      <div class="ui-confirm-actions">
        <button type="button" class="btn btn-secondary" :disabled="busy" @click="cancel">{{ cancelLabel }}</button>
        <button
          type="button"
          class="btn"
          :class="tone === 'danger' ? 'btn-danger' : 'btn-primary'"
          :disabled="busy"
          @click="confirm"
        >
          {{ busy ? '處理中…' : confirmLabel }}
        </button>
      </div>
    </div>
  </dialog>
</template>

<style scoped>
.ui-confirm { width: min(34rem, calc(100vw - 2rem)); border: 0; padding: 0; background: transparent; color: var(--c-ink); }
.ui-confirm::backdrop { background: rgb(8 8 10 / 60%); }
.ui-confirm-body { padding: 26px 28px; display: grid; gap: 12px; box-shadow: 0 24px 70px rgb(0 0 0 / 32%); }
.ui-confirm-desc { font-size: 15px; }
.ui-confirm-actions { margin-top: 12px; display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px; }
</style>
