<script setup lang="ts">
// Set the theme before first paint so there is no flash. Dark is the approved default.
useHead({ script: [{ innerHTML: "try{var t=localStorage.getItem('astra-theme');document.documentElement.dataset.theme=(t==='light'||t==='dark')?t:'dark'}catch(e){document.documentElement.dataset.theme='dark'}" }] })
</script>

<template>
  <div>
    <a href="#main-content" class="skip-link">跳至主要內容</a>
    <NuxtRouteAnnouncer />
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </div>
</template>
