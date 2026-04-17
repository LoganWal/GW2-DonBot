<template>
  <div class="collapsible-section">
    <button class="collapsible-header" @click="open = !open">
      <span class="collapsible-title">{{ title }}</span>
      <i :class="open ? 'pi pi-chevron-up' : 'pi pi-chevron-down'" class="collapsible-icon" />
    </button>
    <div v-show="open" class="collapsible-body">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{ title: string; collapsed?: boolean }>(), { collapsed: false })
const open = ref(!props.collapsed)
</script>

<style scoped>
.collapsible-section {
  margin-top: 1.5rem;
}

.collapsible-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 0.4rem 0;
  margin-bottom: 0.5rem;
  background: none;
  border: none;
  border-bottom: 1px solid var(--p-surface-border);
  cursor: pointer;
  gap: 0.5rem;
}

.collapsible-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.collapsible-icon {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
  flex-shrink: 0;
}
</style>
