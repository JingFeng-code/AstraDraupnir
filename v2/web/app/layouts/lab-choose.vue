<script setup lang="ts">
import '~/assets/lab/tokens.css'
useHead({ htmlAttrs: { 'data-lab': 'choose' }, title: 'Draupnir V2.1 設計方向' })
</script>

<template>
  <div class="ch-shell"><slot /></div>
</template>

<style>
html[data-lab='choose'] .ch-shell { max-width: 1040px; margin: 0 auto; padding: 56px 32px 96px; }
</style>
