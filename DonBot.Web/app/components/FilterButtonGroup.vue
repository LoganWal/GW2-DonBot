<template>
  <div class="filter-button-group">
    <Button
      v-for="opt in options"
      :key="String(opt.value)"
      size="small"
      :label="opt.label"
      :severity="modelValue === opt.value ? (opt.severity ?? 'primary') : 'secondary'"
      @click="$emit('update:modelValue', opt.value)"
    />
  </div>
</template>

<script setup lang="ts" generic="T">
export interface FilterOption<T> {
  label: string
  value: T
  severity?: string
}

defineProps<{
  options: FilterOption<T>[]
  modelValue: T
}>()

defineEmits<{
  'update:modelValue': [value: T]
}>()
</script>

<style scoped>
.filter-button-group {
  display: flex;
  gap: 0.4rem;
}
</style>
