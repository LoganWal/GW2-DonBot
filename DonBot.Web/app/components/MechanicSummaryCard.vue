<template>
  <div class="mechanic-summary-card">
    <div class="mechanic-summary-name" :title="name">{{ name }}</div>
    <div class="mechanic-summary-row">
      <span class="mechanic-summary-label">Max</span>
      <a
        v-if="maxLink && maxValue > 0"
        :href="maxLink"
        class="mechanic-max-link"
      >{{ maxValue }}</a>
      <span v-else>{{ maxValue }}</span>
    </div>
    <div class="mechanic-summary-row">
      <span class="mechanic-summary-label">Avg</span>
      <span>{{ avg }}</span>
    </div>
    <div class="mechanic-summary-row">
      <span class="mechanic-summary-label">Median</span>
      <span>{{ median }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  name: string
  maxValue?: number
  maxLink?: string | null
  avg: string
  median?: number
}>(), {
  maxValue: 0,
  maxLink: null,
  median: 0,
})
</script>

<style scoped>
.mechanic-summary-card {
  background: var(--p-surface-ground);
  border: 1px solid var(--p-surface-border);
  border-radius: 0.5rem;
  padding: 0.5rem 0.75rem;
  font-size: 0.8rem;
}
.mechanic-summary-name {
  font-weight: 600;
  color: var(--p-text-muted-color);
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  margin-bottom: 0.35rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.mechanic-summary-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.25rem;
  line-height: 1.6;
}
.mechanic-summary-label {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
}
.mechanic-max-link {
  color: var(--p-primary-color);
  text-decoration: none;
  font-weight: 600;
}
.mechanic-max-link:hover {
  text-decoration: underline;
}
</style>
