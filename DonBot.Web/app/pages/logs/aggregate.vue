<template>
  <div>
    <div style="display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem;">
      <Button icon="pi pi-arrow-left" severity="secondary" text @click="navigateTo('/logs')" />
      <h1 class="page-title" style="margin: 0;">Aggregated Results</h1>
      <Button
        v-if="result && result.type !== 'wvw'"
        icon="pi pi-upload"
        :label="wingmanQueued ? 'Queued!' : 'Upload to Wingman'"
        severity="secondary"
        :disabled="wingmanQueued"
        style="margin-left: auto;"
        @click="uploadToWingman"
      />
    </div>

    <ProgressSpinner v-if="pending" />

    <template v-else-if="result">
      <div v-if="displayResult" style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem; align-items: stretch;">
        <Card class="stat-card">
          <template #content><div class="stat-label">Logs</div><div v-fit-text class="stat-value">{{ displayResult.totalLogs }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Fight Time</div><div v-fit-text class="stat-value">{{ formatDuration(displayResult.totalDurationMs) }}</div></template>
        </Card>
        <Card v-if="displayResult.sessionDurationMs" class="stat-card">
          <template #content><div class="stat-label">Total Time</div><div v-fit-text class="stat-value">{{ formatDuration(displayResult.sessionDurationMs) }}</div></template>
        </Card>
        <Card v-if="displayResult.sessionDurationMs && displayResult.sessionDurationMs > displayResult.totalDurationMs" class="stat-card">
          <template #content><div class="stat-label">Downtime</div><div v-fit-text class="stat-value">{{ formatDuration(displayResult.sessionDurationMs - displayResult.totalDurationMs) }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Type</div><div v-fit-text class="stat-value">{{ displayResult.type === 'wvw' ? 'WvW' : 'PvE' }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Players</div><div v-fit-text class="stat-value">{{ displayResult.players.length }}</div></template>
        </Card>
        <ProgressSpinner v-if="filterPending" style="width: 2rem; height: 2rem;" />
      </div>

      <div v-if="result.type !== 'wvw'" style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.75rem; align-items: center;">
        <div style="display: flex; gap: 0.4rem;">
          <Button size="small" label="All" :severity="aggSuccessFilter === 'all' ? 'primary' : 'secondary'" @click="aggSuccessFilter = 'all'" />
          <Button size="small" label="Kills" :severity="aggSuccessFilter === 'kills' ? 'success' : 'secondary'" @click="aggSuccessFilter = 'kills'" />
          <Button size="small" label="Wipes" :severity="aggSuccessFilter === 'wipes' ? 'danger' : 'secondary'" @click="aggSuccessFilter = 'wipes'" />
        </div>
        <div style="display: flex; gap: 0.4rem;">
          <Button size="small" label="All modes" :severity="aggDifficultyFilter === null ? 'primary' : 'secondary'" @click="aggDifficultyFilter = null" />
          <Button size="small" label="NM" :severity="aggDifficultyFilter === 0 ? 'primary' : 'secondary'" @click="aggDifficultyFilter = 0" />
          <Button size="small" label="CM" :severity="aggDifficultyFilter === 1 ? 'primary' : 'secondary'" @click="aggDifficultyFilter = 1" />
          <Button size="small" label="LCM" :severity="aggDifficultyFilter === 2 ? 'primary' : 'secondary'" @click="aggDifficultyFilter = 2" />
        </div>
      </div>

      <Tabs value="logs">
        <TabList>
          <Tab value="logs">Logs</Tab>
          <Tab value="damage">Damage & Combat</Tab>
          <Tab v-if="displayResult?.type === 'wvw'" value="support">Support</Tab>
          <Tab value="survivability">Survivability</Tab>
          <Tab v-if="displayResult?.type !== 'wvw'" value="mechanics">Mechanics</Tab>
        </TabList>
        <TabPanels>

          <!-- Logs -->
          <TabPanel value="logs">
            <DataTable :value="filteredAggLogs" striped-rows class="mb-section" size="small">
              <Column header="Fight">
                <template #body="{ data }">{{ fightName(data.fightType) }}</template>
              </Column>
              <Column header="Date">
                <template #body="{ data }">{{ new Date(data.fightStart)?.toLocaleString() ?? '0' }}</template>
              </Column>
              <Column header="Duration">
                <template #body="{ data }">{{ formatDuration(data.fightDurationInMs) }}</template>
              </Column>
              <Column header="Result">
                <template #body="{ data }">
                  <Tag v-if="data.fightType !== 0" :severity="data.isSuccess ? 'success' : 'danger'" :value="data.isSuccess ? 'Kill' : `${data.fightPercent}%`" />
                  <Tag v-else severity="secondary" value="WvW" />
                </template>
              </Column>
              <Column header="Links" style="width: 6rem;">
                <template #body="{ data }">
                  <div style="display: flex; gap: 0.5rem; align-items: center;">
                    <Button icon="pi pi-eye" severity="secondary" text size="small" v-tooltip.top="'View log details'" @click="navigateTo(`/logs/${data.fightLogId}`)" />
                    <a v-if="data.url" :href="data.url" target="_blank" rel="noopener" v-tooltip.top="'Open on dps.report'" style="color: var(--p-text-muted-color); display: flex; align-items: center;">
                      <i class="pi pi-external-link" style="font-size: 0.875rem;" />
                    </a>
                  </div>
                </template>
              </Column>
            </DataTable>
            <Message v-if="filteredAggLogs.length === 0" severity="info" :closable="false">
              No logs match the current filter.
            </Message>
          </TabPanel>

          <!-- Damage & Combat -->
          <TabPanel value="damage">
            <template v-if="displayResult && filteredAggLogs.length > 0 && displayResult.type === 'wvw'">
              <div class="charts-row mb-section">
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Avg Damage per Fight</div>
                  <Chart type="line" :data="wvwDamageChartData" :options="clickableChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Avg DDC per Fight</div>
                  <Chart type="line" :data="wvwDdcChartData" :options="clickableChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Kills per Fight</div>
                  <Chart type="line" :data="killsChartData" :options="clickableIntChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Downs per Fight</div>
                  <Chart type="line" :data="downsChartData" :options="clickableIntChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Boons Ripped per Fight</div>
                  <Chart type="line" :data="wvwBoonsRippedChartData" :options="clickableIntChartOptions" />
                </div>
              </div>
              <DataTable :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="damage" :sort-order="-1">
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column field="fightCount" header="Fights" :sortable="true" style="min-width: 65px;" />
                <Column header="Avg Damage" :sortable="true" sort-field="damage" style="min-width: 110px;">
                  <template #body="{ data }">{{ data.damage?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Avg DDC" :sortable="true" sort-field="damageDownContribution" style="min-width: 100px;">
                  <template #body="{ data }">{{ data.damageDownContribution?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column field="kills" header="Kills" :sortable="true" style="min-width: 60px;" />
                <Column field="downs" header="Downs" :sortable="true" style="min-width: 65px;" />
                <Column field="interrupts" header="Interrupts" :sortable="true" style="min-width: 90px;" />
                <Column field="numberOfBoonsRipped" header="Boons Ripped" :sortable="true" style="min-width: 110px;" />
              </DataTable>
            </template>
            <template v-else-if="displayResult && filteredAggLogs.length > 0">
              <div class="charts-row mb-section">
                <div class="chart-container clickable-chart">
                  <div class="chart-label">DPS per Fight</div>
                  <Chart type="line" :data="pveDpsChartData" :options="clickableChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Cleave DPS per Fight</div>
                  <Chart type="line" :data="pveCleaveDpsChartData" :options="clickableChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Alacrity % per Fight</div>
                  <Chart type="line" :data="pveAlacChartData" :options="clickableChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Quickness % per Fight</div>
                  <Chart type="line" :data="pveQuickChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="dps" :sort-order="-1">
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column field="fightCount" header="Fights" :sortable="true" style="min-width: 65px;" />
                <Column header="DPS" :sortable="true" sort-field="dps" style="min-width: 90px;">
                  <template #body="{ data }">{{ data.dps?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Cleave DPS" :sortable="true" sort-field="cleaveDps" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.cleaveDps?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Avg Barrier Gen" :sortable="true" sort-field="barrierGenerated" style="min-width: 120px;">
                  <template #body="{ data }">{{ data.barrierGenerated?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Quick %" :sortable="true" sort-field="quicknessDuration" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.quicknessDuration }}%</template>
                </Column>
                <Column header="Alac %" :sortable="true" sort-field="alacDuration" style="min-width: 75px;">
                  <template #body="{ data }">{{ data.alacDuration }}%</template>
                </Column>
              </DataTable>
            </template>
          </TabPanel>

          <!-- Support (WvW only) -->
          <TabPanel value="support">
            <template v-if="displayResult && filteredAggLogs.length > 0 && displayResult.type === 'wvw'">
              <div class="charts-row mb-section">
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Avg Healing per Fight</div>
                  <Chart type="line" :data="healingChartData" :options="clickableChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Quickness % per Fight</div>
                  <Chart type="line" :data="wvwQuickChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="healing" :sort-order="-1">
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column header="Avg Healing" :sortable="true" sort-field="healing" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.healing?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Avg Cleanses" :sortable="true" sort-field="cleanses" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.cleanses }}</template>
                </Column>
                <Column header="Avg Strips" :sortable="true" sort-field="strips" style="min-width: 95px;">
                  <template #body="{ data }">{{ data.strips }}</template>
                </Column>
                <Column header="Avg Barrier Gen" :sortable="true" sort-field="barrierGenerated" style="min-width: 120px;">
                  <template #body="{ data }">{{ data.barrierGenerated?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Stab On" :sortable="true" sort-field="stabOnGroup" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.stabOnGroup }}</template>
                </Column>
                <Column header="Stab Off" :sortable="true" sort-field="stabOffGroup" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.stabOffGroup }}</template>
                </Column>
                <Column header="Quick %" :sortable="true" sort-field="quicknessDuration" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.quicknessDuration }}%</template>
                </Column>
              </DataTable>
            </template>
          </TabPanel>

          <!-- Survivability -->
          <TabPanel value="survivability">
            <template v-if="displayResult && filteredAggLogs.length > 0 && displayResult.type === 'wvw'">
              <div class="charts-row mb-section">
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Deaths per Fight</div>
                  <Chart type="line" :data="deathsChartData" :options="clickableIntChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Downed per Fight</div>
                  <Chart type="line" :data="downedChartData" :options="clickableIntChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Damage Taken per Fight</div>
                  <Chart type="line" :data="damageTakenChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="deaths" :sort-order="-1">
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column field="deaths" header="Deaths" :sortable="true" style="min-width: 70px;" />
                <Column field="timesDowned" header="Downed" :sortable="true" style="min-width: 70px;" />
                <Column field="firstToDie" header="Died 1st" :sortable="true" style="min-width: 75px;" />
                <Column header="Dmg Taken" :sortable="true" sort-field="damageTaken" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.damageTaken?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Barrier Mit" :sortable="true" sort-field="barrierMitigation" style="min-width: 100px;">
                  <template #body="{ data }">{{ data.barrierMitigation?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Res Time (s)" :sortable="true" sort-field="resurrectionTime" style="min-width: 105px;">
                  <template #body="{ data }">{{ (data.resurrectionTime / 1000).toFixed(1) }}</template>
                </Column>
                <Column field="timesInterrupted" header="Interrupted" :sortable="true" style="min-width: 95px;" />
                <Column header="Dist Tag" :sortable="true" sort-field="distanceFromTag" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.distanceFromTag > 0 ? data.distanceFromTag : '-' }}</template>
                </Column>
              </DataTable>
            </template>

            <template v-else-if="displayResult && filteredAggLogs.length > 0">
              <div class="charts-row mb-section">
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Deaths per Fight</div>
                  <Chart type="line" :data="deathsChartData" :options="clickableIntChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Downed per Fight</div>
                  <Chart type="line" :data="downedChartData" :options="clickableIntChartOptions" />
                </div>
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Damage Taken per Fight</div>
                  <Chart type="line" :data="damageTakenChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="deaths" :sort-order="-1">
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column field="deaths" header="Deaths" :sortable="true" style="min-width: 70px;" />
                <Column field="timesDowned" header="Downed" :sortable="true" style="min-width: 70px;" />
                <Column field="firstToDie" header="Died 1st" :sortable="true" style="min-width: 75px;" />
                <Column header="Dmg Taken" :sortable="true" sort-field="damageTaken" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.damageTaken?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Res Time (s)" :sortable="true" sort-field="resurrectionTime" style="min-width: 105px;">
                  <template #body="{ data }">{{ (data.resurrectionTime / 1000).toFixed(1) }}</template>
                </Column>
              </DataTable>
            </template>
          </TabPanel>

          <!-- Mechanics (PvE only) -->
          <TabPanel value="mechanics">
            <div v-if="mechanicsByGroup.length > 0">
              <div v-for="group in mechanicsByGroup" :key="group.label" class="mechanic-group">
                <CollapsibleSection :title="group.label" :collapsed="true">
                  <div v-for="item in group.items" :key="item.fightType" class="mechanic-fight-wrap">
                    <button class="mechanic-fight-toggle" @click="toggleFight(`${group.label}:${item.fightType}`)">
                      <span>{{ fightName(item.fightType) }}</span>
                      <i :class="openFights.has(`${group.label}:${item.fightType}`) ? 'pi pi-chevron-up' : 'pi pi-chevron-down'" class="mechanic-fight-icon" />
                    </button>
                    <div v-show="openFights.has(`${group.label}:${item.fightType}`)">
                      <DataTable :value="item.players" striped-rows size="small" scrollable class="mechanic-table">
                        <Column field="accountName" header="Account" frozen style="min-width: 150px;" />
                        <Column v-for="mech in item.mechanicNames" :key="mech" :header="mech" style="min-width: 80px;">
                          <template #body="{ data }">
                            <span :class="{ 'mech-zero': !data.counts[mech] }">{{ data.counts[mech] ?? 0 }}</span>
                          </template>
                        </Column>
                      </DataTable>
                    </div>
                  </div>
                </CollapsibleSection>
              </div>
            </div>
            <Message v-else severity="info" :closable="false">No mechanics data available.</Message>
          </TabPanel>

        </TabPanels>
      </Tabs>
    </template>

    <Message v-else-if="!pending" severity="secondary" :closable="false">
      No data found for the selected logs.
    </Message>
  </div>
</template>

<script setup lang="ts">
import { fightName, groupByFightType } from '~/composables/useFightTypes'
import CollapsibleSection from '~/components/CollapsibleSection.vue'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const api = useApi()

const ids = computed(() => {
  const raw = route.query.ids as string
  return raw ? raw.split(',').map(Number).filter(Boolean) : []
})

const { data: result, pending } = await useAsyncData(
  'aggregate-logs',
  () => api('/api/logs/aggregate', {
    method: 'POST',
    body: { logIds: ids.value },
  }) as Promise<any>
)

const wingmanQueued = ref(false)
const aggSuccessFilter = ref<'all' | 'kills' | 'wipes'>('all')
const aggDifficultyFilter = ref<number | null>(null)
const filterPending = ref(false)
const displayResult = ref<any>(null)

watch(result, (r) => { if (r) displayResult.value = r }, { immediate: true })

const filteredAggLogs = computed(() => {
  let logs = result.value?.logs ?? []
  if (aggSuccessFilter.value === 'kills') logs = logs.filter((l: any) => l.isSuccess)
  else if (aggSuccessFilter.value === 'wipes') logs = logs.filter((l: any) => !l.isSuccess)
  if (aggDifficultyFilter.value !== null) logs = logs.filter((l: any) => l.fightMode === aggDifficultyFilter.value)
  return logs
})

watch(filteredAggLogs, async (filtered) => {
  if (!result.value) return
  const allIds = (result.value.logs ?? []).map((l: any) => l.fightLogId)
  const filteredIds = filtered.map((l: any) => l.fightLogId)
  if (filteredIds.length === allIds.length) {
    displayResult.value = result.value
    return
  }
  if (filteredIds.length === 0) {
    displayResult.value = null
    return
  }
  filterPending.value = true
  try {
    displayResult.value = await api('/api/logs/aggregate', {
      method: 'POST',
      body: { logIds: filteredIds },
    })
  } finally {
    filterPending.value = false
  }
})

const uploadToWingman = () => {
  if (wingmanQueued.value) return
  wingmanQueued.value = true
  api('/api/logs/wingman', {
    method: 'POST',
    body: { logIds: ids.value },
  }).catch(() => {})
}

const formatDuration = (ms: number) => {
  const s = Math.floor(ms / 1000)
  const m = Math.floor(s / 60)
  const h = Math.floor(m / 60)
  if (h > 0) return `${h}h ${m % 60}m ${s % 60}s`
  return `${m}m ${s % 60}s`
}

const PALETTE = [
  '#6366f1', '#22d3ee', '#f59e0b', '#10b981', '#ef4444',
  '#a855f7', '#f97316', '#14b8a6', '#ec4899', '#84cc16',
  '#3b82f6', '#e11d48', '#8b5cf6', '#06b6d4', '#f43f5e',
  '#0ea5e9', '#d946ef', '#65a30d', '#fb923c', '#2dd4bf',
]
const playerColor = (i: number) => PALETTE[i % PALETTE.length]

const chartLabels = computed(() =>
  (displayResult.value?.timeline ?? []).map((t: any) => {
    const time = new Date(t.fightStart).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    return `${fightName(t.fightType)} ${time}`
  })
)

const allAccounts = computed(() => {
  const seen = new Set<string>()
  for (const fight of (displayResult.value?.timeline ?? []))
    for (const p of fight.players) seen.add(p.accountName)
  return [...seen]
})

const makeDataset = (account: string, i: number, getValue: (p: any) => number | null, dashed = false) => ({
  label: account,
  data: (displayResult.value?.timeline ?? []).map((fight: any) => {
    const p = fight.players.find((pl: any) => pl.accountName === account)
    return p ? getValue(p) : null
  }),
  borderColor: playerColor(i),
  backgroundColor: dashed ? 'transparent' : playerColor(i) + '22',
  tension: 0.3,
  spanGaps: true,
  pointRadius: 3,
  pointHoverRadius: 5,
  borderDash: dashed ? [5, 4] : [],
  fill: !dashed,
})

// WvW charts
const wvwDamageChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.damage)) }))
const killsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.kills)) }))
const downsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.downs)) }))
const wvwDdcChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.damageDownContribution)) }))
const wvwBoonsRippedChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.numberOfBoonsRipped)) }))
const healingChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.healing)) }))
const wvwQuickChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => ({ ...makeDataset(a, i, p => p.quicknessDuration, true), fill: false })) }))

// PvE charts
const pveDpsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.dps)) }))
const pveCleaveDpsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.cleaveDps)) }))
const pveAlacChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => ({ ...makeDataset(a, i, p => p.alacDuration, true), fill: false })) }))
const pveQuickChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => ({ ...makeDataset(a, i, p => p.quicknessDuration, true), fill: false })) }))

// Shared charts
const deathsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.deaths)) }))
const downedChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.timesDowned)) }))
const damageTakenChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.damageTaken)) }))

const tooltipOpts: any = {
  callbacks: {
    label: (ctx: any) => `${ctx.dataset.label}: ${ctx.parsed.y?.toLocaleString() ?? 'n/a'}`,
    footer: () => ['Click to open log'],
  },
}
tooltipOpts['itemSort'] = (a: any, b: any) => (b.parsed.y ?? 0) - (a.parsed.y ?? 0)

const handleChartClick = (_event: any, elements: any[]) => {
  if (!elements.length) return
  const timeline = displayResult.value?.timeline ?? []
  const fight = timeline[elements[0].index]
  if (fight?.fightLogId) navigateTo(`/logs/${fight.fightLogId}`)
}

const baseOptions = (stepSize?: number) => ({
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  onClick: handleChartClick,
  plugins: {
    legend: {
      position: 'bottom' as const,
      labels: { color: '#a1a1aa', boxWidth: 10, padding: 8, font: { size: 10 } },
    },
    tooltip: tooltipOpts,
  },
  scales: {
    x: { ticks: { color: '#a1a1aa', maxRotation: 25, font: { size: 10 } }, grid: { color: '#27272a' } },
    y: { ticks: { color: '#a1a1aa', font: { size: 10 }, ...(stepSize ? { stepSize } : {}) }, grid: { color: '#27272a' }, beginAtZero: true },
  },
})

const clickableChartOptions = baseOptions()
const clickableIntChartOptions = baseOptions(1)

const openFights = ref(new Set<string>())
const toggleFight = (key: string) => {
  if (openFights.value.has(key)) openFights.value.delete(key)
  else openFights.value.add(key)
  openFights.value = new Set(openFights.value)
}

const mechanicsByGroup = computed(() =>
  groupByFightType<{
    fightType: number
    mechanicNames: string[]
    players: { accountName: string; counts: Record<string, number> }[]
  }>(displayResult.value?.mechanics ?? [])
)
</script>

<style scoped>
.stat-card {
  container-type: inline-size;
  min-width: 120px;
}
.stat-label {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-bottom: 0.25rem;
}
.stat-value {
  font-size: 1.5rem;
  font-weight: 600;
}
.mb-section {
  margin-bottom: 0.5rem;
}
.charts-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
  margin-top: 0.75rem;
  margin-bottom: 0.75rem;
}
@media (max-width: 640px) {
  .charts-row {
    grid-template-columns: 1fr;
    gap: 0.5rem;
  }
  .chart-container {
    padding: 0.5rem;
  }
  .chart-container :deep(canvas) {
    height: 200px !important;
  }
  .chart-label {
    font-size: 0.7rem;
  }
}
.chart-container {
  background: var(--p-surface-card);
  border: 1px solid var(--p-surface-border);
  border-radius: 0.5rem;
  padding: 0.75rem;
}
.clickable-chart {
  cursor: pointer;
}
.chart-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  margin-bottom: 0.5rem;
}
.chart-container :deep(canvas) {
  width: 100% !important;
  height: 300px !important;
}
.mechanic-group {
  margin-top: 0.25rem;
}
.mechanic-group :deep(.collapsible-section) {
  margin-top: 0.5rem;
}
.mechanic-group :deep(.collapsible-title) {
  font-size: 0.875rem;
}
.mechanic-fight-wrap {
  margin: 0.25rem 0 0 1rem;
}
.mechanic-fight-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 0.4rem 0;
  margin-top: 0.25rem;
  background: none;
  border: none;
  border-bottom: 1px solid var(--p-surface-border);
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--p-text-color);
  gap: 0.5rem;
}
.mechanic-fight-toggle:hover {
  color: var(--p-primary-color);
}
.mechanic-fight-icon {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
  flex-shrink: 0;
}
.mechanic-table {
  margin: 0.4rem 0 0.75rem;
  border: 1px solid var(--p-surface-border);
  border-radius: 0.375rem;
  overflow: hidden;
}
.mech-zero {
  color: var(--p-text-muted-color);
}
</style>
