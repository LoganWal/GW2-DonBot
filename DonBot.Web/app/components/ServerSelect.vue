<template>
  <Select
    v-bind="$attrs"
    :input-id="inputId"
    v-model="model"
    :options="options ?? []"
    :option-label="optionLabel"
    :option-value="optionValue"
    :placeholder="placeholder"
    :loading="loading"
    :disabled="disabled || loading"
    :style="selectStyle"
  />
</template>

<script setup lang="ts">
import type { StyleValue } from 'vue'

defineOptions({ inheritAttrs: false })

withDefaults(defineProps<{
  options?: Record<string, unknown>[] | null
  optionLabel?: string
  optionValue?: string
  placeholder?: string
  loading?: boolean
  disabled?: boolean
  inputId?: string
  selectStyle?: StyleValue
}>(), {
  optionLabel: 'guildName',
  optionValue: 'guildId',
  placeholder: 'Select a server',
  selectStyle: () => ({ minWidth: '260px' }),
})

const model = defineModel<string | null>()
</script>
