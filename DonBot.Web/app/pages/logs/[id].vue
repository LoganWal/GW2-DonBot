<template>
  <div>
    <ProgressSpinner v-if="pending" />
    <LogDetail v-else-if="fight" :data="fight" />
    <p v-else-if="!pending">Log not found.</p>
  </div>
</template>

<script setup lang="ts">
import LogDetail from '~/components/LogDetail.vue'

definePageMeta({ middleware: 'auth' })

const api = useApi()
const route = useRoute()

const { data: fight, pending } = await useAsyncData(
  `log-${route.params.id}`,
  () => api(`/api/logs/${route.params.id}`) as Promise<{ log: any; players: any[]; mechanics: any[] }>
)
</script>
