<template>
  <Card
    class="stat-card"
    :class="{ 'stat-card-clickable': to }"
    :style="colspan ? `grid-column: span ${colspan};` : undefined"
    @click="onClick"
  >
    <template #content>
      <div class="stat-label-row">
        <div class="stat-label">{{ label }}</div>
        <i v-if="to" class="pi pi-arrow-up-right stat-card-arrow" />
      </div>
      <div class="stat-value">{{ formattedValue }}</div>
      <div v-if="sub" class="stat-sub">{{ sub }}</div>
      <slot />
    </template>
  </Card>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  label: string
  value: string | number | null | undefined
  sub?: string
  colspan?: number
  to?: string
}>()

const formattedValue = computed(() => {
  const v = props.value
  if (v == null) {
    return '0'
  }
  return typeof v === 'number' ? formatCompact(v) : v
})

const onClick = () => {
  if (props.to) {
    navigateTo(props.to)
  }
}
</script>

<style scoped>
.stat-label-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.4rem;
}
.stat-sub {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
  margin-top: 0.1rem;
}

.stat-card-clickable {
  cursor: pointer;
  transition: border-color 0.15s, transform 0.15s;
}
.stat-card-clickable:hover {
  border-color: var(--p-primary-color) !important;
  transform: translateY(-1px);
}
.stat-card-arrow {
  font-size: 0.7rem;
  color: var(--p-primary-color);
  opacity: 0.7;
}
.stat-card-clickable:hover .stat-card-arrow {
  opacity: 1;
}
</style>
