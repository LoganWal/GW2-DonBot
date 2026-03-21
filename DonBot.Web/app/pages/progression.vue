<template>
  <div>
    <h1 class="page-title">Progression</h1>

    <div style="margin-bottom: 1.5rem; max-width: 320px;">
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
        style="width: 100%;"
        @change="load"
      />
    </div>

    <ProgressSpinner v-if="loading" />
    <Message v-else-if="selectedFightType !== null && points.length === 0" severity="info" :closable="false">
      No data found for this fight type.
    </Message>

    <template v-else-if="points.length > 0">
      <!-- Character filter -->
      <div v-if="availableCharacters.length > 1" style="margin-bottom: 1rem; max-width: 360px;">
        <MultiSelect
          v-model="selectedCharacters"
          :options="availableCharacters"
          placeholder="All characters"
          show-clear
          display="chip"
          style="width: 100%;"
        />
      </div>

      <!-- Zoom hint / reset bar -->
      <div class="zoom-bar">
        <template v-if="isZoomed">
          <span class="zoom-hint">
            Showing {{ displayPoints.length }} of {{ filteredPoints.length }} fights &mdash; click a point to open its log
          </span>
          <Button label="Reset Zoom" icon="pi pi-arrow-left" size="small" severity="secondary" @click="resetZoom" />
        </template>
        <span v-else class="zoom-hint">Click a data point to zoom in; click again to open the log</span>
      </div>

      <!-- Summary cards -->
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Fights</div>
            <div class="stat-value">{{ displayPoints.length }}<span v-if="isZoomed" class="zoom-total"> / {{ points.length }}</span></div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Avg DPS</div>
            <div class="stat-value">{{ avgOf('dps').toLocaleString() }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Peak DPS</div>
            <div class="stat-value">{{ maxOf('dps').toLocaleString() }}</div>
          </template>
        </Card>
        <Card v-if="isWvW" class="stat-card">
          <template #content>
            <div class="stat-label">Avg Kills</div>
            <div class="stat-value">{{ avgOf('kills').toFixed(1) }}</div>
          </template>
        </Card>
        <Card v-if="isWvW" class="stat-card">
          <template #content>
            <div class="stat-label">Avg Cleanses</div>
            <div class="stat-value">{{ avgOf('cleanses').toFixed(0) }}</div>
          </template>
        </Card>
        <Card v-if="isWvW" class="stat-card">
          <template #content>
            <div class="stat-label">Avg Strips</div>
            <div class="stat-value">{{ avgOf('strips').toFixed(0) }}</div>
          </template>
        </Card>
        <Card v-if="!isWvW" class="stat-card">
          <template #content>
            <div class="stat-label">Avg Cleave DPS</div>
            <div class="stat-value">{{ avgOf('cleaveDps').toLocaleString() }}</div>
          </template>
        </Card>
        <Card v-if="!isWvW" class="stat-card">
          <template #content>
            <div class="stat-label">Avg Alacrity</div>
            <div class="stat-value">{{ avgOf('alacrity').toFixed(1) }}%</div>
          </template>
        </Card>
        <Card v-if="!isWvW" class="stat-card">
          <template #content>
            <div class="stat-label">Avg Quickness</div>
            <div class="stat-value">{{ avgOf('quickness').toFixed(1) }}%</div>
          </template>
        </Card>
      </div>

      <!-- DPS chart -->
      <Card style="margin-bottom: 1.5rem;">
        <template #title>DPS over Time</template>
        <template #content>
          <Chart type="line" :data="dpsChartData" :options="chartOptions" style="height: 300px;" />
        </template>
      </Card>

      <!-- WvW charts -->
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

      <!-- PvE charts -->
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
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const route = useRoute()
const router = useRouter()
const selectedFightType = ref<number | null>(
  route.query.fightType != null ? Number(route.query.fightType) : null
)
const points = ref<any[]>([])
const loading = ref(false)
const isZoomed = ref(false)
const zoomCenter = ref<number | null>(null)
const ZOOM_WINDOW = 12
const selectedCharacters = ref<string[]>([])

const availableCharacters = computed(() =>
  [...new Set(points.value.map(p => p.characterName).filter(Boolean))]
)


const load = async () => {
  if (selectedFightType.value === null)
  {
    return
  }
  router.replace({ query: { fightType: selectedFightType.value } })
  loading.value = true
  isZoomed.value = false
  zoomCenter.value = null
  selectedCharacters.value = []
  try
  {
    points.value = await api(`/api/stats/progression?fightType=${selectedFightType.value}`) as any[]
  }
  finally
  {
    loading.value = false
  }
}

onMounted(() => {
  if (selectedFightType.value !== null)
  {
    load()
  }
})

const isWvW = computed(() => selectedFightType.value === 0)

const filteredPoints = computed(() =>
  selectedCharacters.value.length > 0
    ? points.value.filter(p => selectedCharacters.value.includes(p.characterName))
    : points.value
)

const displayPoints = computed(() => {
  if (!isZoomed.value || zoomCenter.value === null)
  {
    return filteredPoints.value
  }
  const half = Math.floor(ZOOM_WINDOW / 2)
  const start = Math.max(0, zoomCenter.value - half)
  const end = Math.min(filteredPoints.value.length, start + ZOOM_WINDOW)
  return filteredPoints.value.slice(start, end)
})

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

const handleChartClick = (_event: any, elements: any[]) => {
  if (!elements.length)
  {
    return
  }
  const clickedIndex = elements[0].index
  if (!isZoomed.value)
  {
    zoomCenter.value = clickedIndex
    isZoomed.value = true
  }
  else
  {
    const point = displayPoints.value[clickedIndex]
    if (point?.fightLogId)
    {
      navigateTo(`/logs/${point.fightLogId}`)
    }
  }
}

const resetZoom = () => {
  isZoomed.value = false
  zoomCenter.value = null
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
        footer: () => isZoomed.value ? ['Click to open log'] : ['Click to zoom in'],
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

.zoom-bar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
  min-height: 2rem;
}

.zoom-hint {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}

.zoom-total {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
  font-weight: 400;
}
</style>
