<template>
  <div>
    <h1 class="page-title">Dashboard</h1>
    <ProgressSpinner v-if="pending" />
    <template v-else-if="dashboard">

      <!-- Account row -->
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Available Points</div>
            <div v-fit-text class="stat-value">{{ dashboard.account?.availablePoints ?? 0 }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Points Earned</div>
            <div v-fit-text class="stat-value">{{ dashboard.account?.points ?? 0 }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Last Active</div>
            <div v-fit-text class="stat-value" style="font-size: 1rem;">
              {{ dashboard.lastFightDate ? new Date(dashboard.lastFightDate).toLocaleDateString() : '-' }}
            </div>
          </template>
        </Card>
        <Card v-if="dashboard.characterCount" class="stat-card">
          <template #content>
            <div class="stat-label">Characters</div>
            <div v-fit-text class="stat-value">{{ dashboard.characterCount }}</div>
          </template>
        </Card>
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
        <h2 class="section-title">Fight Summary</h2>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Fights</div><div v-fit-text class="stat-value">{{ dashboard.fights.total.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">WvW Fights</div><div v-fit-text class="stat-value">{{ dashboard.fights.wvw.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">PvE Fights</div><div v-fit-text class="stat-value">{{ dashboard.fights.pve.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Deaths</div><div v-fit-text class="stat-value">{{ dashboard.fights.totalDeaths.toLocaleString() }}</div></template>
          </Card>
        </div>

        <h2 class="section-title">Career Totals</h2>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Damage</div><div v-fit-text class="stat-value">{{ dashboard.fights.totalDamage.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Kills (WvW)</div><div v-fit-text class="stat-value">{{ dashboard.fights.totalKills.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Down Contribution (WvW)</div><div v-fit-text class="stat-value">{{ dashboard.fights.totalDownContribution.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Healing</div><div v-fit-text class="stat-value">{{ dashboard.fights.totalHealing.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Cleanses</div><div v-fit-text class="stat-value">{{ dashboard.fights.totalCleanses.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Strips</div><div v-fit-text class="stat-value">{{ dashboard.fights.totalStrips.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Avg Alacrity</div><div v-fit-text class="stat-value">{{ dashboard.fights.avgAlac?.toFixed(1) }}%</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Avg Quickness</div><div v-fit-text class="stat-value">{{ dashboard.fights.avgQuickness?.toFixed(1) }}%</div></template>
          </Card>
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
.section-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 0 0 0.75rem;
}
</style>
