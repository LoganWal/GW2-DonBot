<template>
  <div>
    <LogsAggregate :fetch-aggregate="fetchAggregate" :reload-key="logId" hide-logs-tab />
  </div>
</template>

<script setup lang="ts">
import LogsAggregate from '~/components/LogsAggregate.vue'

definePageMeta({ middleware: 'auth' })

const api = useApi()
const route = useRoute()

const logId = computed(() => Number(route.params.id))

const fetchAggregate = () =>
  api('/api/logs/aggregate', {
    method: 'POST',
    body: { logIds: [logId.value] },
  })
</script>
