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
          <SectionTitle>Overview</SectionTitle>
          <div class="stat-grid">
            <StatCard label="Total Fights" :value="stats.wvw.totalFights" />
            <StatCard label="Total Kills" :value="stats.wvw.totalKills" />
            <StatCard label="Total Downs" :value="stats.wvw.totalDowns" />
            <StatCard label="Total Deaths" :value="stats.wvw.totalDeaths" />
            <StatCard label="Times Downed" :value="stats.wvw.totalTimesDowned" />
            <StatCard label="Avg Distance from Tag" :value="stats.wvw.avgDistanceFromTag?.toFixed(0) ?? '0'" />
          </div>

          <SectionTitle>Damage</SectionTitle>
          <div class="stat-grid">
            <StatCard label="Total Damage" :value="stats.wvw.totalDamage" :sub="(stats.wvw.avgDps?.toLocaleString() ?? '0') + '/s avg'" />
            <StatCard label="Total Cleave" :value="stats.wvw.totalCleave" :sub="(stats.wvw.avgCleaveDps?.toLocaleString() ?? '0') + '/s avg'" />
            <StatCard label="Down Contribution" :value="stats.wvw.totalDamageDownContribution" />
          </div>

          <SectionTitle>Support</SectionTitle>
          <div class="stat-grid">
            <StatCard label="Total Healing" :value="stats.wvw.totalHealing" :sub="(stats.wvw.avgHealingPerSecond?.toLocaleString() ?? '0') + '/s avg'" />
            <StatCard label="Barrier Generated" :value="stats.wvw.totalBarrierGenerated" />
            <StatCard label="Total Cleanses" :value="stats.wvw.totalCleanses" :sub="(stats.wvw.avgCleansesPerSecond ?? '0') + '/s avg'" />
            <StatCard label="Total Strips" :value="stats.wvw.totalStrips" :sub="(stats.wvw.avgStripsPerSecond ?? '0') + '/s avg'" />
            <StatCard label="Avg Quickness" :value="(stats.wvw.avgQuickness?.toFixed(1) ?? '0') + '%'" />
            <StatCard label="Avg Alacrity" :value="(stats.wvw.avgAlac?.toFixed(1) ?? '0') + '%'" />
            <StatCard label="Avg Stab (On Group)" :value="stats.wvw.avgStabOnGroup?.toFixed(2) ?? '0'" />
            <StatCard label="Avg Stab (Off Group)" :value="stats.wvw.avgStabOffGroup?.toFixed(2) ?? '0'" />
          </div>

          <SectionTitle>Aggregations</SectionTitle>
          <div class="stat-grid">
            <AggCard
              label="Attacks Missed"
              :rows="[
                { side: 'Ours', value: stats.wvw.totalHitsWhileBlinded ?? 0 },
                { side: 'Theirs', value: stats.wvw.totalMissesAgainst ?? 0 },
              ]"
            />
            <AggCard
              label="Attacks Blocked"
              :rows="[
                { side: 'Ours', value: stats.wvw.totalBlockedAttacks ?? 0 },
                { side: 'Theirs', value: stats.wvw.totalEnemyBlockedAttacks ?? 0 },
              ]"
            />
            <AggCard
              label="Boons Stripped"
              :rows="[
                { side: 'Ours', value: stats.wvw.totalStrips ?? 0 },
                { side: 'Theirs', value: stats.wvw.totalBoonsRipped ?? 0 },
              ]"
            />
            <AggCard
              label="Damage vs Barrier"
              :rows="[
                { side: 'Dmg Taken', value: stats.wvw.totalDamageTaken ?? 0 },
                { side: 'Barrier Mit.', value: stats.wvw.totalBarrierMitigation ?? 0 },
                { side: 'Mit. %', value: (stats.wvw.barrierMitigationPercent?.toFixed(1) ?? '0.0') + '%' },
              ]"
            />
          </div>
        </TabPanel>

        <!-- PvE Tab -->
        <TabPanel v-if="stats.pve" value="pve">
          <SectionTitle>Player Overview</SectionTitle>
          <div class="stat-grid">
            <StatCard label="Total Fights" :value="stats.pve.totalFights" />
            <StatCard label="Total Damage" :value="stats.pve.totalDamage" :sub="(stats.pve.avgDps?.toLocaleString() ?? '0') + '/s avg'" />
            <StatCard label="Total Cleave" :value="stats.pve.totalCleave" :sub="(stats.pve.avgCleaveDps?.toLocaleString() ?? '0') + '/s avg'" />
            <StatCard label="Avg Alacrity" :value="(stats.pve.avgAlac?.toFixed(1) ?? '0') + '%'" />
            <StatCard label="Avg Quickness" :value="(stats.pve.avgQuickness?.toFixed(1) ?? '0') + '%'" />
            <StatCard label="Total Healing" :value="stats.pve.totalHealing" :sub="(stats.pve.avgHealingPerSecond?.toLocaleString() ?? '0') + '/s avg'" />
          </div>

          <SectionTitle>Survivability</SectionTitle>
          <div class="stat-grid">
            <StatCard label="Res Time (ms)" :value="stats.pve.totalResurrectionTime" />
            <StatCard label="Damage Taken" :value="stats.pve.totalDamageTaken" />
            <StatCard label="Times Downed" :value="stats.pve.totalTimesDowned" />
            <StatCard label="Total Deaths" :value="stats.pve.totalDeaths" />
          </div>
        </TabPanel>
      </Tabs>
      <template v-if="stats.characters?.length">
        <SectionTitle style="margin-top: 1.5rem;">Characters</SectionTitle>
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
const { data: stats, pending } = await useAsyncData('stats', () => api('/api/stats/me') as Promise<any>)

const defaultTab = computed(() => stats.value?.pve ? 'pve' : 'wvw')
</script>
