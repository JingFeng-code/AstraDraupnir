<script setup lang="ts">
const props = defineProps<{ open: boolean, title: string, busy?: boolean }>()
const emit = defineEmits<{ 'update:open': [boolean] }>()
const dialog = ref<HTMLDialogElement>()
const heading = useId()
watchEffect(() => {
  if (!dialog.value) return
  if (dialog.value.open && !props.open) dialog.value.close()
  else if (!dialog.value.open && props.open) dialog.value.showModal()
}, { flush: 'post' })

</script>

<template>
  <dialog ref="dialog" class="project-dialog ui-panel" :aria-labelledby="heading" @close="emit('update:open', false)" @cancel="busy && $event.preventDefault()">
    <header class="ui-panel-h"><h2 :id="heading" class="ui-h2">{{ title }}</h2></header>
    <div class="ui-panel-b"><slot /></div>
  </dialog>
</template>

<style scoped>
.project-dialog { margin: auto; width: min(46rem, calc(100vw - 2rem)); max-height: 90vh; overflow: auto; color: var(--c-ink); }
.project-dialog::backdrop { background: color-mix(in srgb, var(--c-bg) 72%, transparent); }
</style>
