<template>
  <div>
    <h1 class="page-title">Points & Raffles</h1>
    <ProgressSpinner v-if="pending" />
    <div v-else>
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <StatCard label="Available Points" :value="pointsData?.availablePoints ?? 0" />
        <StatCard label="Total Earned" :value="pointsData?.points ?? 0" />
      </div>
      <h2 style="margin-bottom: 1rem;">Active Raffles</h2>
      <Message v-if="!raffleData?.raffles?.length" severity="secondary" :closable="false">
        No active raffles.
      </Message>
      <div v-else style="display: flex; flex-direction: column; gap: 1rem;">
        <Card v-for="raffle in raffleData.raffles" :key="raffle.id">
          <template #title>{{ raffle.description }}</template>
          <template #content>
            <span>Your bid: </span>
            <strong>
              {{ raffleData.userBids.find((b: any) => b.raffleId === raffle.id)?.pointsSpent ?? 0 }} points
            </strong>
          </template>
        </Card>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()

const [{ data: pointsData }, { data: raffleData, pending }] = await Promise.all([
  useAsyncData('points', () => (api('/api/points/me') as Promise<any>).catch(() => null)),
  useAsyncData('raffles', () => api('/api/raffles') as Promise<any>)
])
</script>
