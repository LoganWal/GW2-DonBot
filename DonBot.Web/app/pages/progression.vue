<template>
  <div>
    <h1 class="page-title">Progression</h1>

    <div style="display: flex; gap: 0.75rem; flex-wrap: wrap; align-items: center; margin-bottom: 1.5rem;">
      <Select
        v-model="selectedFightType"
        :options="fightTypeGroupedOptions"
        option-group-label="label"
        option-group-children="items"
        option-label="label"
        option-value="value"
        placeholder="Select fight type"
        filter
        :filter-fields="['label', 'group']"
        scroll-height="400px"
        style="min-width: 240px;"
        @change="load(true)"
      />
      <template v-if="selectedFightType !== null && !isWvWFight">
        <FilterButtonGroup
          :options="successFilterOptions"
          :model-value="successFilter"
          @update:model-value="successFilter = $event; load(false)"
        />
        <FilterButtonGroup
          :options="difficultyFilterOptions"
          :model-value="difficultyFilter"
          @update:model-value="difficultyFilter = $event; load(false)"
        />
      </template>
    </div>

    <template v-if="selectedFightType !== null && !loading">
      <div v-if="allCharacters.length > 1" style="margin-bottom: 1rem; max-width: 360px;">
        <MultiSelect
          v-model="selectedCharacters"
          :options="allCharacters"
          placeholder="All characters"
          show-clear
          display="chip"
          style="width: 100%;"
        />
      </div>
      <div style="display: flex; gap: 0.5rem; margin-bottom: 1rem; flex-wrap: wrap;">
        <Button
          v-for="opt in timeRangeOptions"
          :key="opt.value"
          :label="opt.label"
          size="small"
          :severity="timeRange === opt.value ? 'primary' : 'secondary'"
          @click="timeRange = opt.value; load(false)"
        />
      </div>
    </template>

    <ProgressSpinner v-if="loading" />
    <Message v-else-if="selectedFightType !== null && points.length === 0" severity="info" :closable="false">
      No data found for this fight type.
    </Message>

    <template v-else-if="points.length > 0">
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <StatCard label="Fights" :value="displayPoints.length" />
        <StatCard label="Avg DPS" :value="avgOf('dps')" />
        <StatCard label="Peak DPS" :value="maxOf('dps')" />
        <StatCard v-if="isWvW" label="Avg Kills" :value="avgOf('kills')" :sub="'per fight'" />
        <StatCard v-if="isWvW" label="Avg Cleanses" :value="avgOf('cleanses')" :sub="'per fight'" />
        <StatCard v-if="isWvW" label="Avg Strips" :value="avgOf('strips')" :sub="'per fight'" />
        <StatCard v-if="!isWvW" label="Avg Cleave DPS" :value="avgOf('cleaveDps')" />
        <StatCard v-if="!isWvW" label="Avg Alacrity" :value="avgOf('alacrity').toFixed(1) + '%'" />
        <StatCard v-if="!isWvW" label="Avg Quickness" :value="avgOf('quickness').toFixed(1) + '%'" />
      </div>

      <Card style="margin-bottom: 1.5rem;">
        <template #title>Boss Hp progress over Time</template>
        <template #content>
          <Chart type="line" :data="bossHpChartData" :options="chartOptions" style="height: 300px;" />
        </template>
      </Card>
      
      <Card style="margin-bottom: 1.5rem;">
        <template #title>DPS over Time</template>
        <template #content>
          <Chart type="line" :data="dpsChartData" :options="chartOptions" style="height: 300px;" />
        </template>
      </Card>

      <template v-if="isWvW">
        <div class="chart-grid">
          <Card>
            <template #title>Kills & Downs</template>
            <template #content>
              <Chart type="line" :data="wvwKillsChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
          <Card>
            <template #title>Cleanses & Strips</template>
            <template #content>
              <Chart type="line" :data="wvwSupportChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
          <Card>
            <template #title>Down Contribution</template>
            <template #content>
              <Chart type="line" :data="wvwDownContribChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
          <Card>
            <template #title>Healing</template>
            <template #content>
              <Chart type="line" :data="wvwHealingChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
        </div>
      </template>

      <template v-else>
        <div class="chart-grid">
          <Card>
            <template #title>Cleave DPS</template>
            <template #content>
              <Chart type="line" :data="pveCleaveChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
          <Card>
            <template #title>Alacrity & Quickness</template>
            <template #content>
              <Chart type="line" :data="pveBoonChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
          <Card>
            <template #title>Healing</template>
            <template #content>
              <Chart type="line" :data="pveHealingChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
          <Card>
            <template #title>Deaths & Downed</template>
            <template #content>
              <Chart type="bar" :data="pveSurvivabilityChartData" :options="chartOptions" style="height: 260px;" />
            </template>
          </Card>
        </div>

        <template v-if="mechanicNames.length > 0">
          <div style="margin: 1.5rem 0 1rem; font-size: 0.75rem; font-weight: 600; color: var(--p-text-muted-color); text-transform: uppercase; letter-spacing: 0.05em;">Mechanics</div>
          <div class="mechanic-summary-grid" style="margin-bottom: 1.25rem;">
            <MechanicSummaryCard
              v-for="name in mechanicNames"
              :key="'sum-'+name"
              :name="name"
              :max-value="mechanicMaxMap[name]?.value ?? 0"
              :max-link="mechanicMaxMap[name]?.logId ? `/logs/${mechanicMaxMap[name].logId}` : null"
              :avg="mechanicAvgMap[name]?.toFixed(1) ?? '0.0'"
              :median="mechanicMedianMap[name] ?? 0"
            />
          </div>
          <div class="chart-grid">
            <Card v-for="name in mechanicNames" :key="name">
              <template #title>{{ name }}</template>
              <template #content>
                <Chart type="line" :data="mechanicChartDataMap[name]" :options="chartOptions" style="height: 260px;" />
              </template>
            </Card>
          </div>
        </template>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { fightTypeGroupedOptions } from '~/composables/useFightTypes'
import { successFilterOptions, difficultyFilterOptions, type SuccessFilter, type DifficultyFilter } from '~/composables/useLogFilters'

definePageMeta({ middleware: 'auth' })

const api = useApi()
const route = useRoute()
const router = useRouter()
const selectedFightType = ref<number | null>(
  route.query.fightType != null ? Number(route.query.fightType) : null
)
const points = ref<any[]>([])
const loading = ref(false)
const selectedCharacters = ref<string[]>([])
const timeRange = ref<'all' | 'year' | 'month' | 'week'>('all')
const allCharacters = ref<string[]>([])
const successFilter = ref<SuccessFilter>('all')
const difficultyFilter = ref<DifficultyFilter>(null)

const isWvWFight = computed(() => selectedFightType.value === 0)

const timeRangeOptions = [
  { label: 'All time', value: 'all' },
  { label: 'Last year', value: 'year' },
  { label: 'Last month', value: 'month' },
  { label: 'Last week', value: 'week' },
] as const

const load = async (resetCharacters = false) => {
  if (selectedFightType.value === null)
  {
    return
  }
  await router.replace({ query: { fightType: selectedFightType.value } })
  loading.value = true
  if (resetCharacters) {
    selectedCharacters.value = []
    successFilter.value = 'all'
    difficultyFilter.value = null
  }
  try
  {
    let url = `/api/stats/progression?fightType=${selectedFightType.value}`
    if (timeRange.value !== 'all') {
      const msMap = { week: 7, month: 30, year: 365 }
      const since = new Date(Date.now() - msMap[timeRange.value] * 24 * 60 * 60 * 1000)
      url += `&startDateTime=${since.toISOString()}`
    }
    if (!isWvWFight.value) {
      if (successFilter.value === 'kills') url += '&isSuccess=true'
      else if (successFilter.value === 'wipes') url += '&isSuccess=false'
      if (difficultyFilter.value !== null) url += `&fightMode=${difficultyFilter.value}`
    }
    points.value = await api(url) as any[]
    if (resetCharacters)
      allCharacters.value = [...new Set((points.value as any[]).map((p: any) => p.characterName).filter(Boolean))]
  }
  finally
  {
    loading.value = false
  }
}

onMounted(() => {
  if (selectedFightType.value !== null)
  {
    load(true)
  }
})

const isWvW = computed(() => selectedFightType.value === 0)

const filteredPoints = computed(() =>
  selectedCharacters.value.length > 0
    ? points.value.filter(p => selectedCharacters.value.includes(p.characterName))
    : points.value
)

const displayPoints = computed(() => filteredPoints.value)

const labels = computed(() =>
  displayPoints.value.map(p => new Date(p.date).toLocaleDateString())
)

const avgOf = (field: string) => {
  const vals = displayPoints.value.map(p => Number(p[field]) || 0)
  return vals.length ? vals.reduce((a, b) => a + b, 0) / vals.length : 0
}

const maxOf = (field: string) =>
  displayPoints.value.length ? Math.max(...displayPoints.value.map(p => Number(p[field]) || 0)) : 0

const lineColor = (r: number, g: number, b: number, a = 1) => `rgba(${r},${g},${b},${a})`

const makeDataset = (label: string, field: string, r: number, g: number, b: number) => ({
  label,
  data: displayPoints.value.map(p => Number(p[field]) || 0),
  borderColor: lineColor(r, g, b),
  backgroundColor: lineColor(r, g, b, 0.15),
  tension: 0.3,
  pointRadius: 4,
  pointHoverRadius: 7,
  fill: false,
})

const bossHpChartData = computed(() => ({
  labels: labels.value,
  datasets: [makeDataset('Boss Hp', 'fightPercent', 219, 44, 67)],
}))

const dpsChartData = computed(() => ({
  labels: labels.value,
  datasets: [makeDataset('DPS', 'dps', 99, 179, 237)],
}))

const wvwKillsChartData = computed(() => ({
  labels: labels.value,
  datasets: [
    makeDataset('Kills', 'kills', 99, 179, 237),
    makeDataset('Downs', 'downs', 123, 179, 91),
  ],
}))

const wvwSupportChartData = computed(() => ({
  labels: labels.value,
  datasets: [
    makeDataset('Cleanses', 'cleanses', 99, 179, 237),
    makeDataset('Strips', 'strips', 219, 44, 67),
  ],
}))

const wvwDownContribChartData = computed(() => ({
  labels: labels.value,
  datasets: [makeDataset('Down Contribution', 'downContribution', 193, 105, 79)],
}))

const wvwHealingChartData = computed(() => ({
  labels: labels.value,
  datasets: [makeDataset('Healing', 'healing', 123, 179, 91)],
}))

const pveCleaveChartData = computed(() => ({
  labels: labels.value,
  datasets: [makeDataset('Cleave DPS', 'cleaveDps', 123, 179, 91)],
}))

const pveBoonChartData = computed(() => ({
  labels: labels.value,
  datasets: [
    makeDataset('Alacrity %', 'alacrity', 99, 179, 237),
    makeDataset('Quickness %', 'quickness', 193, 105, 79),
  ],
}))

const pveHealingChartData = computed(() => ({
  labels: labels.value,
  datasets: [makeDataset('Healing', 'healing', 123, 179, 91)],
}))

const pveSurvivabilityChartData = computed(() => ({
  labels: labels.value,
  datasets: [
    { ...makeDataset('Deaths', 'deaths', 219, 44, 67), fill: false },
    { ...makeDataset('Downed', 'timesDowned', 193, 105, 79), fill: false },
  ],
}))

const mechanicNames = computed(() => {
  if (isWvW.value) return []
  const names = new Set<string>()
  for (const p of filteredPoints.value) {
    for (const k of Object.keys((p as any).mechanics ?? {})) {
      names.add(k)
    }
  }
  return [...names].sort()
})

const mechanicMaxMap = computed(() => {
  const result: Record<string, { value: number; logId: number | null }> = {}
  for (const name of mechanicNames.value) {
    let maxVal = 0, maxLogId: number | null = null
    for (const p of displayPoints.value) {
      const v = Number(((p as any).mechanics ?? {})[name] ?? 0)
      if (v > maxVal) { maxVal = v; maxLogId = (p as any).fightLogId }
    }
    result[name] = { value: maxVal, logId: maxLogId }
  }
  return result
})

const mechanicAvgMap = computed(() => {
  const result: Record<string, number> = {}
  for (const name of mechanicNames.value) {
    const vals = displayPoints.value
      .map(p => Number(((p as any).mechanics ?? {})[name] ?? 0))
      .filter(v => v > 0)
    result[name] = vals.length ? vals.reduce((a, b) => a + b, 0) / vals.length : 0
  }
  return result
})

const mechanicMedianMap = computed(() => {
  const result: Record<string, number> = {}
  for (const name of mechanicNames.value) {
    const vals = displayPoints.value
      .map(p => Number(((p as any).mechanics ?? {})[name] ?? 0))
      .sort((a, b) => a - b)
    const mid = Math.floor(vals.length / 2)
    result[name] = vals.length % 2 === 0
      ? ((vals[mid - 1] ?? 0) + (vals[mid] ?? 0)) / 2
      : (vals[mid] ?? 0)
  }
  return result
})

const mechanicChartDataMap = computed(() => {
  const map: Record<string, any> = {}
  for (const name of mechanicNames.value) {
    map[name] = {
      labels: labels.value,
      datasets: [{
        label: name,
        data: displayPoints.value.map(p => Number(((p as any).mechanics ?? {})[name] ?? 0)),
        borderColor: lineColor(168, 85, 247),
        backgroundColor: lineColor(168, 85, 247, 0.15),
        tension: 0.3,
        pointRadius: 4,
        pointHoverRadius: 7,
        fill: false,
      }],
    }
  }
  return map
})

const handleChartClick = (_event: any, elements: any[]) => {
  if (!elements.length) return
  const point = displayPoints.value[elements[0].index]
  if (point?.fightLogId) navigateTo(`/logs/${point.fightLogId}`)
}

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  onClick: handleChartClick,
  plugins: {
    legend: {
      labels: { color: '#94a3b8', boxWidth: 12 }
    },
    tooltip: {
      mode: 'index' as const,
      intersect: false,
      callbacks: {
        footer: () => ['Click to open log'],
      },
    },
  },
  scales: {
    x: {
      ticks: { color: '#64748b', maxTicksLimit: 10 },
      grid: { color: 'rgba(255,255,255,0.05)' }
    },
    y: {
      ticks: { color: '#64748b' },
      grid: { color: 'rgba(255,255,255,0.05)' }
    }
  }
}))
</script>

<style scoped>
.chart-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(420px, 1fr));
  gap: 1rem;
}

@media (max-width: 480px) {
  .chart-grid {
    grid-template-columns: 1fr;
  }
}
.mechanic-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 0.5rem;
}
</style>
