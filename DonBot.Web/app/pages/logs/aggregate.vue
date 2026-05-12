<template>
  <div>
    <div style="display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem;">
      <Button icon="pi pi-arrow-left" severity="secondary" text @click="navigateTo('/logs')" />
      <h1 class="page-title" style="margin: 0;">Aggregated Results</h1>
      <Button
        icon="pi pi-upload"
        :label="wingmanQueued ? 'Queued!' : 'Upload to Wingman'"
        severity="secondary"
        :disabled="wingmanQueued"
        style="margin-left: auto;"
        @click="uploadToWingman"
      />
    </div>

    <LogsAggregate :fetch-aggregate="fetchAggregate" />
  </div>
</template>

<script setup lang="ts">
import LogsAggregate from '~/components/LogsAggregate.vue'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const api = useApi()

const ids = computed(() => {
  const raw = route.query.ids as string
  return raw ? raw.split(',').map(Number).filter(Boolean) : []
})

const fetchAggregate = async (logIds?: number[]) =>
  api('/api/logs/aggregate', {
    method: 'POST',
    body: { logIds: logIds ?? ids.value },
  })

const wingmanQueued = ref(false)
const uploadToWingman = () => {
  if (wingmanQueued.value) {
    return
  }
  wingmanQueued.value = true
  api('/api/logs/wingman', {
    method: 'POST',
    body: { logIds: ids.value },
  }).catch(() => {})
}
</script>
