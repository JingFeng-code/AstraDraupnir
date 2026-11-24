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

watch(
  () => props.open,
  async (open) => {
    await nextTick()
    if (!dialog.value) return
    if (open && !dialog.value.open) dialog.value.showModal()
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

onBeforeUnmount(() => {
  if (dialog.value?.open) dialog.value.close()
})
</script>

<template>
  <dialog
    ref="dialog"
    class="w-[min(32rem,calc(100vw-2rem))] rounded-2xl border border-line bg-surface p-0 text-ink shadow-xl backdrop:bg-navy-900/60"
    :aria-labelledby="titleId"
    @cancel.prevent="cancel"
    @close="emit('update:open', false)"
  >
    <div class="p-6">
      <h2 :id="titleId" class="text-lg font-bold text-ink">{{ title }}</h2>
      <p v-if="description" class="mt-2 text-sm leading-relaxed text-ink-muted">{{ description }}</p>
      <slot />
      <div class="mt-6 flex flex-wrap justify-end gap-2">
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
