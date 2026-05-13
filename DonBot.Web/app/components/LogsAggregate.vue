<template>
  <div>
    <ProgressSpinner v-if="pending" />

    <template v-else-if="result">
      <div v-if="displayResult" style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem; align-items: stretch;">
        <StatCard label="Logs" :value="displayResult.totalLogs" />
        <StatCard label="Fight Time" :value="formatDuration(displayResult.totalDurationMs, true)" />
        <StatCard v-if="displayResult.sessionDurationMs" label="Total Time" :value="formatDuration(displayResult.sessionDurationMs, true)" />
        <StatCard v-if="displayResult.sessionDurationMs && displayResult.sessionDurationMs > displayResult.totalDurationMs" label="Downtime" :value="formatDuration(displayResult.sessionDurationMs - displayResult.totalDurationMs, true)" />
        <StatCard label="Type" :value="displayResult.type === 'wvw' ? 'WvW' : 'PvE'" />
        <StatCard label="Players" :value="displayResult.players.length" />
        <ProgressSpinner v-if="filterPending" style="width: 2rem; height: 2rem;" />
      </div>

      <div v-if="result.type !== 'wvw'" style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.75rem; align-items: center;">
        <FilterButtonGroup
          :options="successFilterOptions"
          :model-value="aggSuccessFilter"
          @update:model-value="aggSuccessFilter = $event"
        />
        <FilterButtonGroup
          :options="difficultyFilterOptions"
          :model-value="aggDifficultyFilter"
          @update:model-value="aggDifficultyFilter = $event"
        />
      </div>

      <Tabs value="logs">
        <TabList>
          <Tab value="logs">Logs</Tab>
          <Tab value="damage">Damage & Combat</Tab>
          <Tab value="support">Support</Tab>
          <Tab value="survivability">Survivability</Tab>
          <Tab v-if="displayResult?.type !== 'wvw'" value="mechanics">Mechanics</Tab>
        </TabList>
        <TabPanels>

          <TabPanel value="logs">
            <DataTable :value="filteredAggLogs" striped-rows class="mb-section" size="small" :row-style="logRowStyle" @row-click="onLogRowClick">
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
                    <Button icon="pi pi-eye" severity="secondary" text size="small" v-tooltip.top="rowActionTooltip" @click.stop="onRowAction(data)" />
                    <a v-if="data.url" :href="data.url" target="_blank" rel="noopener" v-tooltip.top="'Open on dps.report'" style="color: var(--p-text-muted-color); display: flex; align-items: center;" @click.stop>
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
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Quickness % per Fight</div>
                  <Chart type="line" :data="wvwQuickChartData" :options="clickableChartOptions" />
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
                <Column header="Quick %" :sortable="true" sort-field="quicknessDuration" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.quicknessDuration }}%</template>
                </Column>
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
                <Column header="Quick %" :sortable="true" sort-field="quicknessDuration" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.quicknessDuration }}%</template>
                </Column>
                <Column header="Alac %" :sortable="true" sort-field="alacDuration" style="min-width: 75px;">
                  <template #body="{ data }">{{ data.alacDuration }}%</template>
                </Column>
              </DataTable>
            </template>
          </TabPanel>

          <TabPanel value="support">
            <template v-if="displayResult && filteredAggLogs.length > 0 && displayResult.type === 'wvw'">
              <div class="charts-row mb-section">
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Avg Healing per Fight</div>
                  <Chart type="line" :data="healingChartData" :options="clickableChartOptions" />
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
              </DataTable>
            </template>
            <template v-else-if="displayResult && filteredAggLogs.length > 0">
              <div class="charts-row mb-section">
                <div class="chart-container clickable-chart">
                  <div class="chart-label">Avg Healing per Fight</div>
                  <Chart type="line" :data="healingChartData" :options="clickableChartOptions" />
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
                <Column header="Avg Barrier Gen" :sortable="true" sort-field="barrierGenerated" style="min-width: 120px;">
                  <template #body="{ data }">{{ data.barrierGenerated?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Avg Strips" :sortable="true" sort-field="strips" style="min-width: 95px;">
                  <template #body="{ data }">{{ data.strips }}</template>
                </Column>
                <Column header="Stab On" :sortable="true" sort-field="stabOnGroup" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.stabOnGroup }}</template>
                </Column>
                <Column header="Stab Off" :sortable="true" sort-field="stabOffGroup" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.stabOffGroup }}</template>
                </Column>
              </DataTable>
            </template>
          </TabPanel>

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
                      <DataTable :value="flattenMechanicPlayers(item.players)" striped-rows size="small" scrollable class="mechanic-table">
                        <Column field="accountName" header="Account" frozen style="min-width: 150px;" />
                        <Column v-for="mech in item.mechanicNames" :key="mech" :field="mech" :header="mech" :sortable="true" style="min-width: 80px;" header-style="white-space: nowrap">
                          <template #body="{ data }">
                            <span :class="{ 'mech-zero': !data[mech] }">{{ data[mech] ?? 0 }}</span>
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
import { fightName, groupByFightType, formatDuration } from '~/composables/useFightTypes'
import { successFilterOptions, difficultyFilterOptions, type SuccessFilter, type DifficultyFilter } from '~/composables/useLogFilters'
import CollapsibleSection from '~/components/CollapsibleSection.vue'

type AggregateResult = any

const props = defineProps<{
  fetchAggregate: (logIds?: number[]) => Promise<AggregateResult>
  reloadKey?: number | string
  selectedLogId?: number | null
  rowAction?: 'navigate' | 'select'
}>()

const emit = defineEmits<{
  (e: 'select-log', fightLogId: number): void
}>()

const result = ref<AggregateResult | null>(null)
const displayResult = ref<AggregateResult | null>(null)
const pending = ref(true)
const filterPending = ref(false)
const aggSuccessFilter = ref<SuccessFilter>('all')
const aggDifficultyFilter = ref<DifficultyFilter>(null)

const loadInitial = async () => {
  pending.value = true
  try {
    const r = await props.fetchAggregate()
    result.value = r
    displayResult.value = r
  } catch {
    result.value = null
    displayResult.value = null
  } finally {
    pending.value = false
  }
}

await loadInitial()

watch(() => props.reloadKey, () => {
  loadInitial()
})

const filteredAggLogs = computed(() => {
  let logs = result.value?.logs ?? []
  if (aggSuccessFilter.value === 'kills') {
    logs = logs.filter((l: any) => l.isSuccess)
  } else if (aggSuccessFilter.value === 'wipes') {
    logs = logs.filter((l: any) => !l.isSuccess)
  }
  if (aggDifficultyFilter.value !== null) {
    logs = logs.filter((l: any) => l.fightMode === aggDifficultyFilter.value)
  }
  return logs
})

watch(filteredAggLogs, async (filtered) => {
  if (!result.value) {
    return
  }
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
    displayResult.value = await props.fetchAggregate(filteredIds)
  } catch {
    displayResult.value = null
  } finally {
    filterPending.value = false
  }
})

const rowActionTooltip = computed(() => props.rowAction === 'select' ? 'Show this log above' : 'View log details')

const onRowAction = (row: any) => {
  if (props.rowAction === 'select') {
    emit('select-log', row.fightLogId)
  } else {
    navigateTo(`/logs/${row.fightLogId}`)
  }
}

const onLogRowClick = (event: any) => {
  if (props.rowAction === 'select') {
    emit('select-log', event.data.fightLogId)
  }
}

const logRowStyle = (row: any) => {
  if (props.selectedLogId && row.fightLogId === props.selectedLogId) {
    return { background: 'color-mix(in srgb, var(--p-primary-color) 12%, transparent)' }
  }
  return undefined
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
  for (const fight of (displayResult.value?.timeline ?? [])) {
    for (const p of fight.players) {
      seen.add(p.accountName)
    }
  }
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

const wvwDamageChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.damage)) }))
const killsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.kills)) }))
const downsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.downs)) }))
const wvwDdcChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.damageDownContribution)) }))
const wvwBoonsRippedChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.numberOfBoonsRipped)) }))
const healingChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.healing)) }))
const wvwQuickChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => ({ ...makeDataset(a, i, p => p.quicknessDuration, true), fill: false })) }))

const pveDpsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.dps)) }))
const pveCleaveDpsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.cleaveDps)) }))
const pveAlacChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => ({ ...makeDataset(a, i, p => p.alacDuration, true), fill: false })) }))
const pveQuickChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => ({ ...makeDataset(a, i, p => p.quicknessDuration, true), fill: false })) }))

const deathsChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.deaths)) }))
const downedChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.timesDowned)) }))
const damageTakenChartData = computed(() => ({ labels: chartLabels.value, datasets: allAccounts.value.map((a, i) => makeDataset(a, i, p => p.damageTaken)) }))

const tooltipOpts: any = {
  callbacks: {
    label: (ctx: any) => `${ctx.dataset.label}: ${ctx.parsed.y?.toLocaleString() ?? 'n/a'}`,
    footer: () => [props.rowAction === 'select' ? 'Click to show above' : 'Click to open log'],
  },
}
tooltipOpts['itemSort'] = (a: any, b: any) => (b.parsed.y ?? 0) - (a.parsed.y ?? 0)

const handleChartClick = (_event: any, elements: any[]) => {
  if (!elements.length) {
    return
  }
  const timeline = displayResult.value?.timeline ?? []
  const fight = timeline[elements[0].index]
  if (!fight?.fightLogId) {
    return
  }
  if (props.rowAction === 'select') {
    emit('select-log', fight.fightLogId)
  } else {
    navigateTo(`/logs/${fight.fightLogId}`)
  }
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
  if (openFights.value.has(key)) {
    openFights.value.delete(key)
  } else {
    openFights.value.add(key)
  }
  openFights.value = new Set(openFights.value)
}

const mechanicsByGroup = computed(() =>
  groupByFightType<{
    fightType: number
    mechanicNames: string[]
    players: { accountName: string; counts: Record<string, number> }[]
  }>(displayResult.value?.mechanics ?? [])
)

const flattenMechanicPlayers = (players: { accountName: string; counts: Record<string, number> }[]) =>
  players.map(p => ({ accountName: p.accountName, ...p.counts }))

defineExpose({ reload: loadInitial })
</script>

<style scoped>
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
