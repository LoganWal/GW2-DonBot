<template>
  <div>
    <h1 class="page-title">Dashboard</h1>
    <ProgressSpinner v-if="pending" />
    <template v-else-if="dashboard">
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <StatCard label="Available Points" :value="dashboard.account?.availablePoints ?? 0" />
        <StatCard label="Total Points Earned" :value="dashboard.account?.points ?? 0" />
        <StatCard label="Last Active" :value="dashboard.lastFightDate ? new Date(dashboard.lastFightDate).toLocaleDateString() : '-'" />
        <StatCard v-if="dashboard.characterCount" label="Characters" :value="dashboard.characterCount" />
        <Card v-if="dashboard.gw2Accounts?.length" class="stat-card" style="grid-column: span 2;">
          <template #content>
            <div class="stat-label" style="margin-bottom: 0.5rem;">GW2 Accounts</div>
            <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
              <Tag
                v-for="a in dashboard.gw2Accounts"
                :key="a.guildWarsAccountName"
                :value="a.guildWarsAccountName"
                severity="secondary"
              />
            </div>
          </template>
        </Card>
      </div>
      <template v-if="dashboard.fights">
        <SectionTitle style="margin: 0 0 0.75rem">Fight Summary</SectionTitle>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <StatCard label="Total Fights" :value="dashboard.fights.total" />
          <StatCard label="WvW Fights" :value="dashboard.fights.wvw" />
          <StatCard label="PvE Fights" :value="dashboard.fights.pve" />
          <StatCard label="Total Deaths" :value="dashboard.fights.totalDeaths" />
        </div>
        <SectionTitle style="margin: 0 0 0.75rem">Career Totals</SectionTitle>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <StatCard label="Total Damage" :value="dashboard.fights.totalDamage" />
          <StatCard label="Total Kills (WvW)" :value="dashboard.fights.totalKills" />
          <StatCard label="Down Contribution (WvW)" :value="dashboard.fights.totalDownContribution" />
          <StatCard label="Total Healing" :value="dashboard.fights.totalHealing" />
          <StatCard label="Total Cleanses" :value="dashboard.fights.totalCleanses" />
          <StatCard label="Total Strips" :value="dashboard.fights.totalStrips" />
          <StatCard label="Avg Alacrity" :value="(dashboard.fights.avgAlac?.toFixed(1) ?? '0') + '%'" />
          <StatCard label="Avg Quickness" :value="(dashboard.fights.avgQuickness?.toFixed(1) ?? '0') + '%'" />
        </div>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const { data: dashboard, pending } = await useAsyncData('dashboard', () => api('/api/dashboard') as Promise<any>)
</script>

<style scoped>
</style>
