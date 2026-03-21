<template>
  <Card class="stat-card best-card" style="cursor: pointer;" @click="navigateTo(`/logs/${entry.fightLogId}`)">
    <template #content>
      <div class="stat-label">{{ label }}</div>
      <div class="stat-value">{{ fmt(entry.value) }}</div>
      <div class="best-meta">
        <span>{{ fightName(entry.fightType) }}</span>
        <span>{{ new Date(entry.fightDate).toLocaleDateString() }}</span>
      </div>
    </template>
  </Card>
</template>

<script setup lang="ts">
defineProps<{
  label: string
  entry: { value: any; fightLogId: number; fightType: number; fightDate: string }
  fmt: (v: any) => string
}>()
</script>

<style scoped>
.best-card:hover {
  border-color: var(--p-primary-color);
}

.best-meta {
  display: flex;
  justify-content: space-between;
  font-size: 0.72rem;
  color: var(--p-text-muted-color);
  margin-top: 0.35rem;
}
</style>
