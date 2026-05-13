<template>
  <h2 class="section-title">
    <slot />
  </h2>
</template>

<script setup lang="ts">
</script>

<style scoped>
.section-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 1.5rem 0 0.75rem;
}
</style>
