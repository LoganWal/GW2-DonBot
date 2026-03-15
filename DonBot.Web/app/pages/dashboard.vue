<template>
  <div>
    <h1 class="page-title">Dashboard</h1>
    <ProgressSpinner v-if="pending" />
    <div v-else-if="dashboard">
      <div class="stat-grid">
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Available Points</div>
            <div class="stat-value">{{ dashboard.account?.availablePoints ?? 0 }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Points Earned</div>
            <div class="stat-value">{{ dashboard.account?.points ?? 0 }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Last Fight</div>
            <div class="stat-value" style="font-size: 1rem;">
              {{ dashboard.lastFightDate ? new Date(dashboard.lastFightDate).toLocaleDateString() : '—' }}
            </div>
          </template>
        </Card>
      </div>
      <Card v-if="dashboard.gw2Accounts?.length">
        <template #title>GW2 Accounts</template>
        <template #content>
          <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
            <Tag
              v-for="account in dashboard.gw2Accounts"
              :key="account.guildWarsAccountName"
              :value="account.guildWarsAccountName"
              severity="secondary"
            />
          </div>
        </template>
      </Card>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const { data: dashboard, pending } = await useAsyncData('dashboard', () => api('/api/dashboard'))
</script>
