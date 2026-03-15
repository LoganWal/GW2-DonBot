<template>
  <div>
    <h1 class="page-title">My Stats</h1>
    <ProgressSpinner v-if="pending" />
    <div v-else-if="stats">
      <div v-if="stats.totalFights === 0">
        <Message severity="info" :closable="false">No fight data found.</Message>
      </div>
      <div v-else class="stat-grid">
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Fights</div>
            <div class="stat-value">{{ stats.totalFights }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Damage</div>
            <div class="stat-value">{{ stats.totalDamage?.toLocaleString() }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Deaths</div>
            <div class="stat-value">{{ stats.totalDeaths }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Healing</div>
            <div class="stat-value">{{ stats.totalHealing?.toLocaleString() }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Cleanses</div>
            <div class="stat-value">{{ stats.totalCleanses }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Strips</div>
            <div class="stat-value">{{ stats.totalStrips }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Avg Quickness</div>
            <div class="stat-value">{{ stats.avgQuickness?.toFixed(1) }}%</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Avg Alacrity</div>
            <div class="stat-value">{{ stats.avgAlac?.toFixed(1) }}%</div>
          </template>
        </Card>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const { data: stats, pending } = await useAsyncData('stats', () => api('/api/stats/me'))
</script>
