<template>
  <Card class="stat-card agg-card">
    <template #content>
      <div class="stat-label">{{ label }}</div>
      <div v-for="row in rows" :key="row.side" class="agg-row">
        <span class="agg-side">{{ row.side }}</span>
        <span class="stat-value">{{ formatted(row.value) }}</span>
      </div>
    </template>
  </Card>
</template>

<script setup lang="ts">
export interface AggRow {
  side: string
  value: string | number
}

defineProps<{
  label: string
  rows: AggRow[]
}>()

const formatted = (v: string | number) =>
  typeof v === 'number' ? formatCompact(v) : v
</script>

<style scoped>
.agg-card :deep(.p-card-content) {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.agg-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
}
.agg-side {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
}
</style>
