<template>
  <StatCard
    class="best-card"
    :label="label"
    :value="fmt(entry.value)"
    :to="`/logs/${entry.fightLogId}`"
  >
    <div class="best-meta">
      <span>{{ fightName(entry.fightType) }}</span>
      <span>{{ new Date(entry.fightDate).toLocaleDateString() }}</span>
    </div>
  </StatCard>
</template>

<script setup lang="ts">
defineProps<{
  label: string
  entry: { value: any; fightLogId: number; fightType: number; fightDate: string }
  fmt: (v: any) => string
}>()
</script>

<style scoped>
.best-meta {
  display: flex;
  justify-content: space-between;
  font-size: 0.72rem;
  color: var(--p-text-muted-color);
  margin-top: 0.35rem;
}
</style>
