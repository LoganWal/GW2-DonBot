<template>
  <div>
    <h1 class="page-title">My Stats</h1>
    <ProgressSpinner v-if="pending" />
    <div v-else-if="stats">
      <Message v-if="!stats.wvw && !stats.pve" severity="info" :closable="false">No fight data found.</Message>
      <Tabs v-else :value="defaultTab">
        <TabList>
          <Tab v-if="stats.pve" value="pve">PvE</Tab>
          <Tab v-if="stats.wvw" value="wvw">WvW</Tab>
        </TabList>

        <!-- WvW Tab -->
        <TabPanel v-if="stats.wvw" value="wvw">
          <h2 class="section-title">Overview</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Fights</div><div class="stat-value">{{ stats.wvw.totalFights?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Kills</div><div class="stat-value">{{ stats.wvw.totalKills?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Downs</div><div class="stat-value">{{ stats.wvw.totalDowns?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Deaths</div><div class="stat-value">{{ stats.wvw.totalDeaths?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Times Downed</div><div class="stat-value">{{ stats.wvw.totalTimesDowned?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Avg Distance from Tag</div><div class="stat-value">{{ stats.wvw.avgDistanceFromTag?.toFixed(0) }}</div></template>
            </Card>
          </div>

          <h2 class="section-title">Damage</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Damage</div><div class="stat-value">{{ stats.wvw.totalDamage?.toLocaleString() }}</div><div class="stat-sub">{{ stats.wvw.avgDps?.toLocaleString() }}/s avg</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Cleave</div><div class="stat-value">{{ stats.wvw.totalCleave?.toLocaleString() }}</div><div class="stat-sub">{{ stats.wvw.avgCleaveDps?.toLocaleString() }}/s avg</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Down Contribution</div><div class="stat-value">{{ stats.wvw.totalDamageDownContribution?.toLocaleString() }}</div></template>
            </Card>
          </div>

          <h2 class="section-title">Support</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Healing</div><div class="stat-value">{{ stats.wvw.totalHealing?.toLocaleString() }}</div><div class="stat-sub">{{ stats.wvw.avgHealingPerSecond?.toLocaleString() }}/s avg</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Barrier Generated</div><div class="stat-value">{{ stats.wvw.totalBarrierGenerated?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Cleanses</div><div class="stat-value">{{ stats.wvw.totalCleanses?.toLocaleString() }}</div><div class="stat-sub">{{ stats.wvw.avgCleansesPerSecond }}/s avg</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Strips</div><div class="stat-value">{{ stats.wvw.totalStrips?.toLocaleString() }}</div><div class="stat-sub">{{ stats.wvw.avgStripsPerSecond }}/s avg</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Avg Quickness</div><div class="stat-value">{{ stats.wvw.avgQuickness?.toFixed(1) }}%</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Avg Alacrity</div><div class="stat-value">{{ stats.wvw.avgAlac?.toFixed(1) }}%</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Avg Stab (On Group)</div><div class="stat-value">{{ stats.wvw.avgStabOnGroup?.toFixed(2) }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Avg Stab (Off Group)</div><div class="stat-value">{{ stats.wvw.avgStabOffGroup?.toFixed(2) }}</div></template>
            </Card>
          </div>

          <h2 class="section-title">Aggregations</h2>
          <div class="stat-grid">
            <Card class="stat-card agg-card">
              <template #content>
                <div class="stat-label">Attacks Missed</div>
                <div class="agg-row"><span class="agg-side">Ours</span><span class="stat-value">{{ stats.wvw.totalHitsWhileBlinded?.toLocaleString() }}</span></div>
                <div class="agg-row"><span class="agg-side">Theirs</span><span class="stat-value">{{ stats.wvw.totalMissesAgainst?.toLocaleString() }}</span></div>
              </template>
            </Card>
            <Card class="stat-card agg-card">
              <template #content>
                <div class="stat-label">Attacks Blocked</div>
                <div class="agg-row"><span class="agg-side">Ours</span><span class="stat-value">{{ stats.wvw.totalBlockedAttacks?.toLocaleString() }}</span></div>
                <div class="agg-row"><span class="agg-side">Theirs</span><span class="stat-value">{{ stats.wvw.totalEnemyBlockedAttacks?.toLocaleString() }}</span></div>
              </template>
            </Card>
            <Card class="stat-card agg-card">
              <template #content>
                <div class="stat-label">Boons Stripped</div>
                <div class="agg-row"><span class="agg-side">Ours</span><span class="stat-value">{{ stats.wvw.totalStrips?.toLocaleString() }}</span></div>
                <div class="agg-row"><span class="agg-side">Theirs</span><span class="stat-value">{{ stats.wvw.totalBoonsRipped?.toLocaleString() }}</span></div>
              </template>
            </Card>
            <Card class="stat-card agg-card">
              <template #content>
                <div class="stat-label">Damage Taken vs Barrier</div>
                <div class="agg-row"><span class="agg-side">Dmg Taken</span><span class="stat-value">{{ stats.wvw.totalDamageTaken?.toLocaleString() }}</span></div>
                <div class="agg-row"><span class="agg-side">Barrier Mit.</span><span class="stat-value">{{ stats.wvw.totalBarrierMitigation?.toLocaleString() }}</span></div>
                <div class="agg-row"><span class="agg-side">Mit. %</span><span class="stat-value">{{ stats.wvw.barrierMitigationPercent?.toFixed(1) }}%</span></div>
              </template>
            </Card>
          </div>
        </TabPanel>

        <!-- PvE Tab -->
        <TabPanel v-if="stats.pve" value="pve">
          <h2 class="section-title">Player Overview</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Fights</div><div class="stat-value">{{ stats.pve.totalFights?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Damage</div><div class="stat-value">{{ stats.pve.totalDamage?.toLocaleString() }}</div><div class="stat-sub">{{ stats.pve.avgDps?.toLocaleString() }}/s avg</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Cleave</div><div class="stat-value">{{ stats.pve.totalCleave?.toLocaleString() }}</div><div class="stat-sub">{{ stats.pve.avgCleaveDps?.toLocaleString() }}/s avg</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Avg Alacrity</div><div class="stat-value">{{ stats.pve.avgAlac?.toFixed(1) }}%</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Avg Quickness</div><div class="stat-value">{{ stats.pve.avgQuickness?.toFixed(1) }}%</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Healing</div><div class="stat-value">{{ stats.pve.totalHealing?.toLocaleString() }}</div><div class="stat-sub">{{ stats.pve.avgHealingPerSecond?.toLocaleString() }}/s avg</div></template>
            </Card>
          </div>

          <h2 class="section-title">Survivability</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Res Time (ms)</div><div class="stat-value">{{ stats.pve.totalResurrectionTime?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Damage Taken</div><div class="stat-value">{{ stats.pve.totalDamageTaken?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Times Downed</div><div class="stat-value">{{ stats.pve.totalTimesDowned?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Total Deaths</div><div class="stat-value">{{ stats.pve.totalDeaths?.toLocaleString() }}</div></template>
            </Card>
          </div>

          <h2 class="section-title">Cerus Mechanics</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Phase 1 Damage</div><div class="stat-value">{{ stats.pve.totalCerusPhaseOneDamage?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Orbs Collected</div><div class="stat-value">{{ stats.pve.totalCerusOrbsCollected?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Spread Hits</div><div class="stat-value">{{ stats.pve.totalCerusSpreadHitCount?.toLocaleString() }}</div></template>
            </Card>
          </div>

          <h2 class="section-title">Deimos Mechanics</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Oils Triggered</div><div class="stat-value">{{ stats.pve.totalDeimosOilsTriggered?.toLocaleString() }}</div></template>
            </Card>
          </div>

          <h2 class="section-title">Ura Mechanics</h2>
          <div class="stat-grid">
            <Card class="stat-card">
              <template #content><div class="stat-label">Shards Picked Up</div><div class="stat-value">{{ stats.pve.totalShardPickUp?.toLocaleString() }}</div></template>
            </Card>
            <Card class="stat-card">
              <template #content><div class="stat-label">Shards Used</div><div class="stat-value">{{ stats.pve.totalShardUsed?.toLocaleString() }}</div></template>
            </Card>
          </div>
        </TabPanel>
      </Tabs>
      <template v-if="stats.characters?.length">
        <h2 class="section-title" style="margin-top: 1.5rem;">Characters</h2>
        <DataTable :value="stats.characters" striped-rows style="max-width: 600px;">
          <Column field="characterName" header="Character" />
          <Column field="wvwLogs" header="WvW Logs" />
          <Column field="pveLogs" header="PvE Logs" />
        </DataTable>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const { data: stats, pending } = await useAsyncData('stats', () => api('/api/stats/me'))

const defaultTab = computed(() => stats.value?.pve ? 'pve' : 'wvw')
</script>

<style scoped>
.section-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 1.5rem 0 0.75rem;
}

.stat-sub {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
  margin-top: 0.1rem;
}

.agg-card .p-card-content {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.agg-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
}

.agg-side {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
}
</style>
