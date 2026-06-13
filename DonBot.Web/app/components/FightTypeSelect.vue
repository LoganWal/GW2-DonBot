<template>
  <Select
    :model-value="modelValue"
    :options="fightTypeGroupedOptions"
    option-group-label="label"
    option-group-children="items"
    option-label="label"
    option-value="value"
    :placeholder="placeholder"
    filter
    :filter-fields="['label', 'group']"
    :scroll-height="scrollHeight"
    style="min-width: 240px;"
    @update:model-value="updateModel"
    @change="emit('change', $event)"
  />
</template>

<script setup lang="ts">
import { fightTypeGroupedOptions } from '~/composables/useFightTypes'

withDefaults(defineProps<{
  modelValue: number | null
  placeholder?: string
  scrollHeight?: string
}>(), {
  placeholder: 'Select fight type',
  scrollHeight: '400px',
})

const emit = defineEmits<{
  'update:modelValue': [value: number | null]
  change: [event: unknown]
}>()

function updateModel(value: unknown) {
  emit('update:modelValue', value == null ? null : Number(value))
}
</script>
