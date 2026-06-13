<template>
  <div class="dashboard-page">
    <div class="dashboard-heading">
      <div>
        <h1 class="page-title dashboard-title">Dashboard</h1>
        <p v-if="dashboard" class="dashboard-subtitle">
          {{ dashboard.lastFightDate ? `Last seen ${formatDate(dashboard.lastFightDate)}` : 'No fight activity yet' }}
        </p>
      </div>
      <Button
        icon="pi pi-refresh"
        severity="secondary"
        text
        rounded
        :loading="pending"
        @click="refresh()"
      />
    </div>

    <Message v-if="unavailablePanelLabels" severity="warn" :closable="false" class="dashboard-notice">
      Some dashboard panels could not be refreshed: {{ unavailablePanelLabels }}.
    </Message>

    <ProgressSpinner v-if="pending && !visuals" />

    <template v-else-if="dashboard">
      <div class="stat-grid dashboard-stat-grid">
        <StatCard label="Available Points" :value="availablePoints" />
        <StatCard label="Total Points Earned" :value="totalPoints" />
        <StatCard label="Total Fights" :value="dashboard.fights?.total ?? 0" />
        <StatCard label="Characters" :value="dashboard.characterCount ?? 0" />
        <Card v-if="dashboard.gw2Accounts?.length" class="stat-card account-card">
          <template #content>
            <div class="stat-label account-card-title">GW2 Accounts</div>
            <div class="account-tags">
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

      <div class="dashboard-grid">
        <Card v-if="recentLogs.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Recent Logs</span>
              <Tag :value="recentLogsLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <div class="log-stack">
              <button
                v-for="log in recentLogs"
                :key="log.fightLogId"
                class="log-row"
                type="button"
                @click="openLog(log.fightLogId)"
              >
                <div class="log-copy">
                  <span>{{ fightName(log.fightType) }} · {{ formatDuration(log.fightDurationInMs) }}</span>
                  <small>{{ formatDate(log.fightStart) }}</small>
                </div>
                <Tag
                  v-if="log.fightType !== 0"
                  :severity="log.isSuccess ? 'success' : 'danger'"
                  :value="log.isSuccess ? 'Kill' : `${log.fightPercent}%`"
                />
                <Tag v-else severity="secondary" value="WvW" />
              </button>
            </div>
          </template>
        </Card>

        <Card v-if="fightMixTotal > 0" class="dashboard-panel">
          <template #title>Fight Mix</template>
          <template #content>
            <div class="doughnut-layout">
              <Chart type="doughnut" :data="fightMixChartData" :options="doughnutOptions" class="doughnut-chart" />
              <div class="legend-stack">
                <div class="legend-row">
                  <span class="legend-dot wvw" />
                  <span>WvW</span>
                  <strong>{{ dashboard.fights?.wvw ?? 0 }}</strong>
                </div>
                <div class="legend-row">
                  <span class="legend-dot pve" />
                  <span>PvE</span>
                  <strong>{{ dashboard.fights?.pve ?? 0 }}</strong>
                </div>
              </div>
            </div>
          </template>
        </Card>

        <Card v-if="totalPoints > 0" class="dashboard-panel">
          <template #title>Points Balance</template>
          <template #content>
            <div class="doughnut-layout">
              <Chart type="doughnut" :data="pointsChartData" :options="doughnutOptions" class="doughnut-chart" />
              <div class="balance-copy">
                <div>
                  <span>Available</span>
                  <strong>{{ formatCompact(availablePoints) }}</strong>
                </div>
                <div>
                  <span>Spent</span>
                  <strong>{{ formatCompact(spentPoints) }}</strong>
                </div>
              </div>
            </div>
          </template>
        </Card>

        <Card v-if="boonBars.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Boon Uptime</span>
              <Tag :value="periodLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <div class="meter-stack">
              <div v-for="bar in boonBars" :key="bar.label" class="meter-row">
                <div class="meter-label">
                  <span>{{ bar.label }}</span>
                  <strong>{{ bar.value.toFixed(1) }}%</strong>
                </div>
                <div class="meter-track">
                  <span class="meter-fill" :class="bar.className" :style="{ width: `${Math.min(bar.value, 100)}%` }" />
                </div>
              </div>
            </div>
          </template>
        </Card>

        <Card v-if="combatProfile.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Combat Stats</span>
              <Tag :value="periodLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <div class="metric-stack">
              <div v-for="metric in combatProfile" :key="metric.label" class="metric-row">
                <div class="metric-label">
                  <span>{{ metric.label }}</span>
                  <strong>{{ formatCompact(metric.value) }}</strong>
                </div>
                <div class="metric-track">
                  <span class="metric-fill" :class="metric.className" :style="{ width: metricWidth(metric.value) }" />
                </div>
              </div>
            </div>
          </template>
        </Card>
      </div>

      <div class="wide-grid">
        <Card v-if="bestHighlights.length" class="dashboard-panel">
          <template #title>Personal Bests</template>
          <template #content>
            <div class="best-grid">
              <button
                v-for="best in bestHighlights"
                :key="best.label"
                class="best-tile"
                type="button"
                :disabled="!best.fightLogId"
                @click="openLog(best.fightLogId)"
              >
                <span>{{ best.label }}</span>
                <strong>{{ best.value }}</strong>
                <small>{{ best.meta }}</small>
                <i v-if="best.fightLogId" class="pi pi-arrow-up-right" />
              </button>
            </div>
          </template>
        </Card>
      </div>

      <div class="wide-grid">
        <Card v-if="characterRows.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Character Usage</span>
              <Tag :value="characterUsageLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <div class="character-chart-scroll">
              <Chart
                type="bar"
                :data="characterChartData"
                :options="stackedBarOptions"
                class="character-chart"
                :style="{ height: characterChartHeight }"
              />
            </div>
          </template>
        </Card>

        <Card v-if="weeklyRanks.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Weekly Rank Snapshot</span>
              <Tag :value="weeklyRankLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <div class="rank-stack">
              <div v-for="rank in weeklyRanks" :key="`${rank.guildId}-${rank.label}`" class="rank-row">
                <div class="rank-copy">
                  <span>{{ rank.guildName ?? 'Unknown server' }}</span>
                  <strong>
                    <span>{{ rank.label }}</span>
                    <span>{{ rank.value }}</span>
                  </strong>
                </div>
                <div class="rank-badge">
                  #{{ rank.rank }}<small>/{{ rank.total }}</small>
                </div>
              </div>
            </div>
          </template>
        </Card>
      </div>

      <div class="trend-grid">
        <Card v-if="logs.length" class="dashboard-panel trend-panel">
          <template #title>
            <div class="panel-title-row">
              <span>{{ selectedTrendTitle }}</span>
              <div class="panel-title-actions">
                <Tag :value="trendLimitLabel" severity="secondary" class="limit-tag" />
                <FightTypeSelect
                  v-model="selectedTrendFightType"
                  class="trend-select"
                />
              </div>
            </div>
          </template>
          <template #content>
            <ProgressSpinner v-if="trendPending" />
            <Message v-else-if="trendUnavailable" severity="warn" :closable="false">
              Trend data is temporarily unavailable.
            </Message>
            <Chart v-else-if="trendPoints.length" type="line" :data="selectedTrendChartData" :options="lineChartOptions" class="trend-chart" />
            <Message v-else severity="secondary" :closable="false">
              No trend data for this fight in the last year.
            </Message>
          </template>
        </Card>

        <Card v-if="recentActivityDays.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Recent Activity</span>
              <Tag :value="recentActivityLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <Chart type="bar" :data="activityChartData" :options="activityChartOptions" class="trend-chart" />
          </template>
        </Card>
      </div>

      <div class="dashboard-grid">
        <Card v-if="uploadItems.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Upload History</span>
              <Tag :value="uploadLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <div class="upload-stack">
              <div v-for="upload in uploadItems" :key="upload.logUploadId" class="upload-row">
                <div>
                  <span>{{ upload.fileName }}</span>
                  <small>{{ formatDate(upload.createdAt) }} · {{ upload.sourceType }}</small>
                </div>
                <Button
                  v-if="upload.fightLogId"
                  icon="pi pi-eye"
                  severity="secondary"
                  text
                  rounded
                  size="small"
                  @click="openLog(upload.fightLogId)"
                />
              </div>
            </div>
          </template>
        </Card>

        <Card v-if="liveRaidGuilds.length || guilds.length" class="dashboard-panel">
          <template #title>
            <div class="panel-title-row">
              <span>Server Access</span>
              <Tag v-if="isLiveRaidGuildPreviewLimited" :value="liveRaidGuildLimitLabel" severity="secondary" class="limit-tag" />
            </div>
          </template>
          <template #content>
            <div class="access-grid">
              <div>
                <span class="access-value">{{ guilds.length }}</span>
                <small>Leaderboard servers</small>
              </div>
              <div>
                <span class="access-value">{{ liveRaidGuilds.length }}</span>
                <small>Live raid servers</small>
              </div>
            </div>
            <div v-if="liveRaidGuilds.length" class="server-tags">
              <Tag
                v-for="guild in liveRaidGuildPreview"
                :key="guild.guildId"
                :value="guild.guildName ?? 'Unknown server'"
                severity="secondary"
              />
            </div>
          </template>
        </Card>
      </div>
    </template>

    <Message v-else :severity="unavailablePanelLabels ? 'warn' : 'info'" :closable="false">
      {{ unavailablePanelLabels ? 'Dashboard summary is temporarily unavailable.' : 'No dashboard data found.' }}
    </Message>
  </div>
</template>

<script setup lang="ts">
import { fightName, formatDuration } from '~/composables/useFightTypes'

definePageMeta({ middleware: 'auth' })

type DashboardAccount = {
  points?: number
  availablePoints?: number
}

type DashboardResponse = {
  account: DashboardAccount | null
  gw2Accounts: { guildWarsAccountName: string }[]
  lastFightDate: string | null
  characterCount?: number
  fights: {
    total: number
    wvw: number
    pve: number
    totalDamage: number
    totalKills: number
    totalDeaths: number
    totalHealing: number
    totalCleanses: number
    totalStrips: number
    totalDownContribution: number
    avgQuickness: number
    avgAlac: number
    bestDamageFight?: { fightLogId: number; fightType: number; damage: number } | null
    bestKillsFight?: { fightLogId: number; kills: number } | null
  } | null
}

type StatsResponse = {
  wvw: Record<string, any> | null
  pve: Record<string, any> | null
  characters?: { characterName: string; pveLogs: number; wvwLogs: number }[]
}

type BestEntry = {
  value: number
  durationMs: number
  fightLogId: number
  fightType: number
  fightDate: string
}

type BestsResponse = {
  wvw: Record<string, BestEntry> | null
  pve: Record<string, BestEntry> | null
  bestTimes?: { fightType: number; durationMs: number; fightLogId: number; fightDate: string; playerDps: number }[] | null
}

type ProgressionPoint = {
  fightLogId: number
  date: string
  durationMs: number
  characterName: string
  dps: number
  kills?: number
  downs?: number
  downContribution?: number
  cleanses?: number
  strips?: number
  healing?: number
  deaths?: number
  cleaveDps?: number
  quickness?: number
  alacrity?: number
  timesDowned?: number
}

type LogRow = {
  fightLogId: number
  fightType: number
  fightMode: number
  fightStart: string
  fightDurationInMs: number
  isSuccess: boolean
  fightPercent: number
  characterName: string
}

type LogsResponse = {
  total: number
  data: LogRow[]
}

type UploadResponse = {
  items: {
    logUploadId: number
    fileName: string
    sourceType: string
    dpsReportUrl?: string | null
    fightLogId?: number | null
    createdAt: string
  }[]
}

type LeaderboardResponse = {
  wvw: Record<string, any>[]
  pve: Record<string, any>[]
}

type GuildSummary = {
  guildId: string
  guildName: string | null
}

type LeaderboardSnapshot = {
  guildId: string
  guildName: string | null
  data: LeaderboardResponse
}

type WeeklyRankRow = {
  guildId: string
  guildName: string | null
  label: string
  rank: number
  total: number
  value: string
  percentile: number
}

type DashboardPanelFailure = {
  key: string
  label: string
}

type DashboardVisuals = {
  dashboard: DashboardResponse | null
  periodDashboard: DashboardResponse | null
  stats: StatsResponse | null
  bests: BestsResponse | null
  logs: LogsResponse
  activityLogs: LogsResponse
  uploads: UploadResponse
  guilds: GuildSummary[]
  liveRaidGuilds: GuildSummary[]
  leaderboards: LeaderboardSnapshot[]
  unavailable: DashboardPanelFailure[]
}

type ApiResult<T> = {
  data: T
  failed: boolean
}

const api = useApi()

const DASHBOARD_PERIOD_DAYS = 30
const TREND_DAYS = 365
const RECENT_ACTIVITY_DAYS = 14
const RECENT_LOG_LIMIT = 5
const RECENT_LOG_FETCH_LIMIT = 100
const ACTIVITY_LOG_PAGE_SIZE = 250
const UPLOAD_LIMIT = 5
const CHARACTER_CHART_BASE_HEIGHT = 96
const CHARACTER_CHART_ROW_HEIGHT = 34
const LEADERBOARD_DAYS = 7
const LEADERBOARD_GUILD_LIMIT = 5
const LIVE_RAID_GUILD_PREVIEW_LIMIT = 4
const MS_PER_DAY = 24 * 60 * 60 * 1000

const periodLimitLabel = `Last ${DASHBOARD_PERIOD_DAYS} days`
const trendLimitLabel = 'Last 1 year'
const recentActivityLimitLabel = `Past ${RECENT_ACTIVITY_DAYS} days`
const uploadLimitLabel = `Latest ${UPLOAD_LIMIT} · Last 24h`
const weeklyRankLimitLabel = `Last ${LEADERBOARD_DAYS} days · Global + ${LEADERBOARD_GUILD_LIMIT} guilds`
const liveRaidGuildLimitLabel = `Showing ${LIVE_RAID_GUILD_PREVIEW_LIMIT}`

const { data: visuals, pending, refresh } = await useAsyncData('dashboard-visuals', loadDashboardVisuals)

const selectedTrendFightType = ref<number | null>(null)
const trendPoints = ref<ProgressionPoint[]>([])
const trendPending = ref(false)
const trendUnavailable = ref(false)
let trendRequestId = 0

const dashboard = computed(() => visuals.value?.dashboard ?? null)
const periodDashboard = computed(() => visuals.value?.periodDashboard ?? null)
const stats = computed(() => visuals.value?.stats ?? null)
const bests = computed(() => visuals.value?.bests ?? null)
const logs = computed(() => visuals.value?.logs.data ?? [])
const logsTotal = computed(() => visuals.value?.logs.total ?? logs.value.length)
const activityLogs = computed(() => visuals.value?.activityLogs.data ?? [])
const guilds = computed(() => visuals.value?.guilds ?? [])
const liveRaidGuilds = computed(() => visuals.value?.liveRaidGuilds ?? [])
const uploadItems = computed(() => visuals.value?.uploads.items ?? [])
const liveRaidGuildPreview = computed(() => liveRaidGuilds.value.slice(0, LIVE_RAID_GUILD_PREVIEW_LIMIT))
const isLiveRaidGuildPreviewLimited = computed(() => liveRaidGuilds.value.length > LIVE_RAID_GUILD_PREVIEW_LIMIT)
const unavailablePanelLabels = computed(() => {
  const labels = [
    ...(visuals.value?.unavailable.map(panel => panel.label) ?? []),
    ...(trendUnavailable.value ? ['Trend'] : []),
  ]
  return [...new Set(labels)].join(', ')
})

const totalPoints = computed(() => Number(dashboard.value?.account?.points ?? 0))
const availablePoints = computed(() => Number(dashboard.value?.account?.availablePoints ?? 0))
const spentPoints = computed(() => Math.max(totalPoints.value - availablePoints.value, 0))

const fightMixTotal = computed(() => Number(dashboard.value?.fights?.total ?? 0))

const fightMixChartData = computed(() => ({
  labels: ['WvW', 'PvE'],
  datasets: [{
    data: [dashboard.value?.fights?.wvw ?? 0, dashboard.value?.fights?.pve ?? 0],
    backgroundColor: ['rgba(99, 179, 237, 0.9)', 'rgba(123, 179, 91, 0.9)'],
    borderColor: 'rgba(15, 23, 42, 0.85)',
    borderWidth: 2,
  }],
}))

const pointsChartData = computed(() => ({
  labels: ['Available', 'Spent'],
  datasets: [{
    data: [availablePoints.value, spentPoints.value],
    backgroundColor: ['rgba(99, 179, 237, 0.9)', 'rgba(193, 105, 79, 0.9)'],
    borderColor: 'rgba(15, 23, 42, 0.85)',
    borderWidth: 2,
  }],
}))

const boonBars = computed(() => {
  const fights = periodDashboard.value?.fights
  return [
    { label: 'Quickness', value: Number(fights?.avgQuickness ?? 0), className: 'fill-blue' },
    { label: 'Alacrity', value: Number(fights?.avgAlac ?? 0), className: 'fill-green' },
  ].filter(b => b.value > 0)
})

const combatProfile = computed(() => {
  const fights = periodDashboard.value?.fights
  if (!fights) {
    return []
  }
  return [
    { label: 'Damage', value: Number(fights.totalDamage ?? 0), className: 'fill-blue' },
    { label: 'Healing', value: Number(fights.totalHealing ?? 0), className: 'fill-green' },
    { label: 'Down Contribution', value: Number(fights.totalDownContribution ?? 0), className: 'fill-amber' },
    { label: 'Cleanses', value: Number(fights.totalCleanses ?? 0), className: 'fill-teal' },
    { label: 'Strips', value: Number(fights.totalStrips ?? 0), className: 'fill-red' },
    { label: 'WvW Kills', value: Number(fights.totalKills ?? 0), className: 'fill-slate' },
  ].filter(m => m.value > 0)
})

const maxCombatMetric = computed(() =>
  combatProfile.value.length ? Math.max(...combatProfile.value.map(m => m.value), 1) : 1
)

const characterRows = computed(() => stats.value?.characters ?? [])

const characterUsageLimitLabel = computed(() => {
  const count = characterRows.value.length
  return count === 1 ? 'All 1 character' : `All ${formatCompact(count)} characters`
})

const characterChartHeight = computed(() =>
  `${Math.max(320, characterRows.value.length * CHARACTER_CHART_ROW_HEIGHT + CHARACTER_CHART_BASE_HEIGHT)}px`
)

const characterChartData = computed(() => ({
  labels: characterRows.value.map(c => c.characterName),
  datasets: [
    {
      label: 'PvE',
      data: characterRows.value.map(c => c.pveLogs),
      backgroundColor: 'rgba(123, 179, 91, 0.85)',
      borderRadius: 4,
    },
    {
      label: 'WvW',
      data: characterRows.value.map(c => c.wvwLogs),
      backgroundColor: 'rgba(99, 179, 237, 0.85)',
      borderRadius: 4,
    },
  ],
}))

const selectedTrendTitle = computed(() =>
  selectedTrendFightType.value == null
    ? 'Trend'
    : `${fightName(selectedTrendFightType.value)} Trend`
)

const trendDisplayPoints = computed(() => trendPoints.value)

const selectedTrendChartData = computed(() => {
  const labels = trendDisplayPoints.value.map(p => formatShortDate(p.date))
  const isWvW = selectedTrendFightType.value === 0

  return {
    labels,
    datasets: isWvW
      ? [
          makeLineDataset('Kills', trendDisplayPoints.value.map(p => Number(p.kills ?? 0)), 'rgba(99, 179, 237, 1)'),
          makeLineDataset('Downs', trendDisplayPoints.value.map(p => Number(p.downs ?? 0)), 'rgba(123, 179, 91, 1)'),
          makeLineDataset('Cleanses', trendDisplayPoints.value.map(p => Number(p.cleanses ?? 0)), 'rgba(45, 212, 191, 1)'),
          makeLineDataset('Strips', trendDisplayPoints.value.map(p => Number(p.strips ?? 0)), 'rgba(219, 44, 67, 1)'),
        ]
      : [
          makeLineDataset('DPS', trendDisplayPoints.value.map(p => Number(p.dps ?? 0)), 'rgba(99, 179, 237, 1)'),
          makeLineDataset('Cleave DPS', trendDisplayPoints.value.map(p => Number(p.cleaveDps ?? 0)), 'rgba(123, 179, 91, 1)'),
        ],
  }
})

const recentActivityDays = computed(() => {
  const map = new Map<string, { label: string; wvw: number; pve: number }>()
  for (let i = RECENT_ACTIVITY_DAYS - 1; i >= 0; i--)
  {
    const date = new Date()
    date.setDate(date.getDate() - i)
    const key = date.toISOString().slice(0, 10)
    map.set(key, { label: formatShortDate(date.toISOString()), wvw: 0, pve: 0 })
  }
  for (const log of activityLogs.value)
  {
    const key = new Date(log.fightStart).toISOString().slice(0, 10)
    const day = map.get(key)
    if (!day) {
      continue
    }
    if (log.fightType === 0) {
      day.wvw += 1
    }
    else {
      day.pve += 1
    }
  }
  return [...map.values()]
})

const activityChartData = computed(() => ({
  labels: recentActivityDays.value.map(d => d.label),
  datasets: [
    {
      label: 'WvW',
      data: recentActivityDays.value.map(d => d.wvw),
      backgroundColor: 'rgba(99, 179, 237, 0.85)',
      borderRadius: 4,
    },
    {
      label: 'PvE',
      data: recentActivityDays.value.map(d => d.pve),
      backgroundColor: 'rgba(123, 179, 91, 0.85)',
      borderRadius: 4,
    },
  ],
}))

const recentLogs = computed(() => logs.value.slice(0, RECENT_LOG_LIMIT))

const recentLogsLimitLabel = computed(() => {
  const visible = Math.min(logs.value.length, RECENT_LOG_LIMIT)
  return logsTotal.value > RECENT_LOG_LIMIT
    ? `Latest ${visible} of ${formatCompact(logsTotal.value)}`
    : `Latest ${visible}`
})

const bestHighlights = computed(() => {
  const rows: { label: string; value: string; meta: string; fightLogId: number | null }[] = []
  addBest(rows, 'PvE Best DPS', bests.value?.pve?.damagePerSecond, v => `${formatCompact(v)}/s`)
  addBest(rows, 'PvE Best HPS', bests.value?.pve?.healingPerSecond, v => `${formatCompact(v)}/s`)
  addBest(rows, 'WvW Best Kills', bests.value?.wvw?.kills, v => formatCompact(v))
  addBest(rows, 'WvW Best Cleanses', bests.value?.wvw?.cleanses, v => formatCompact(v))

  const fastest = [...(bests.value?.bestTimes ?? [])].sort((a, b) => a.durationMs - b.durationMs)[0]
  if (fastest) {
    rows.push({
      label: `Fastest ${fightName(fastest.fightType)}`,
      value: formatDuration(fastest.durationMs),
      meta: `${formatCompact(fastest.playerDps)} DPS`,
      fightLogId: fastest.fightLogId,
    })
  }
  return rows
})

const userAccountNames = computed(() =>
  new Set((dashboard.value?.gw2Accounts ?? [])
    .map(a => a.guildWarsAccountName?.toLowerCase())
    .filter(Boolean))
)

const weeklyRanks = computed<WeeklyRankRow[]>(() => {
  const leaderboards = visuals.value?.leaderboards ?? []
  if (leaderboards.length === 0 || userAccountNames.value.size === 0) {
    return []
  }

  return leaderboards
    .flatMap(leaderboard => [
      rankRow(leaderboard, 'PvE DPS', leaderboard.data.pve, 'dps', true, v => `${formatCompact(v)}/s`),
      rankRow(leaderboard, 'PvE HPS', leaderboard.data.pve, 'hps', true, v => `${formatCompact(v)}/s`),
      rankRow(leaderboard, 'WvW Damage', leaderboard.data.wvw, 'avgDamage', true, v => formatCompact(v)),
      rankRow(leaderboard, 'WvW Healing', leaderboard.data.wvw, 'avgHealing', true, v => formatCompact(v)),
    ])
    .filter((rank): rank is WeeklyRankRow => rank !== null)
    .sort((a, b) =>
      b.percentile - a.percentile
      || b.total - a.total
      || a.rank - b.rank
      || compareNullableText(a.guildName, b.guildName)
      || compareNullableText(a.label, b.label)
    )
})

const doughnutOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  cutout: '68%',
  plugins: {
    legend: { display: false },
    tooltip: {
      callbacks: {
        label: (ctx: any) => `${ctx.label}: ${formatCompact(Number(ctx.raw ?? 0))}`,
      },
    },
  },
}))

const lineChartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  plugins: {
    legend: { labels: { color: '#94a3b8', boxWidth: 12 } },
    tooltip: { mode: 'index' as const, intersect: false },
  },
  scales: {
    x: {
      ticks: { color: '#64748b', maxTicksLimit: 8 },
      grid: { color: 'rgba(255,255,255,0.05)' },
    },
    y: {
      beginAtZero: true,
      ticks: { color: '#64748b' },
      grid: { color: 'rgba(255,255,255,0.05)' },
    },
  },
}))

const stackedBarOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  indexAxis: 'y' as const,
  plugins: {
    legend: { labels: { color: '#94a3b8', boxWidth: 12 } },
  },
  scales: {
    x: {
      stacked: true,
      ticks: { color: '#64748b' },
      grid: { color: 'rgba(255,255,255,0.05)' },
    },
    y: {
      stacked: true,
      ticks: { color: '#94a3b8' },
      grid: { display: false },
    },
  },
}))

const activityChartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { labels: { color: '#94a3b8', boxWidth: 12 } },
  },
  scales: {
    x: {
      stacked: true,
      ticks: { color: '#64748b', maxRotation: 0 },
      grid: { display: false },
    },
    y: {
      stacked: true,
      beginAtZero: true,
      ticks: { color: '#64748b', precision: 0 },
      grid: { color: 'rgba(255,255,255,0.05)' },
    },
  },
}))

watch(logs, (items) => {
  if (items.length === 0) {
    selectedTrendFightType.value = null
    return
  }
  if (selectedTrendFightType.value == null) {
    selectedTrendFightType.value = items[0].fightType
  }
}, { immediate: true })

watch(selectedTrendFightType, (fightType) => {
  loadTrend(fightType)
}, { immediate: true })

async function loadDashboardVisuals(): Promise<DashboardVisuals> {
  const dashboardResult = await safeApi<DashboardResponse | null>('/api/dashboard', null)
  const activityStartDateTime = recentActivityStartDateTime()

  const [
    periodDashboardResult,
    statsResult,
    bestsResult,
    logsResult,
    activityLogsResult,
    uploadsResult,
    guildsResult,
    liveRaidGuildsResult,
  ] = await Promise.all([
    safeApi<DashboardResponse | null>(`/api/dashboard?days=${DASHBOARD_PERIOD_DAYS}`, null),
    safeApi<StatsResponse | null>('/api/stats/me', null),
    safeApi<BestsResponse | null>('/api/stats/bests', null),
    safeApi<LogsResponse>(`/api/logs?page=1&pageSize=${RECENT_LOG_FETCH_LIMIT}`, { total: 0, data: [] }),
    fetchLogsSince(activityStartDateTime),
    safeApi<UploadResponse>(`/api/upload/history?page=1&pageSize=${UPLOAD_LIMIT}`, { items: [] }),
    safeApi<GuildSummary[]>('/api/guilds/mine', []),
    safeApi<GuildSummary[]>('/api/live-raid/guilds', []),
  ])

  const guilds = guildsResult.data
  const leaderboardTargets = [
    { guildId: '-1', guildName: 'Global' },
    ...guilds.slice(0, LEADERBOARD_GUILD_LIMIT),
  ]
  const leaderboardResults = await Promise.all(leaderboardTargets.map(async guild => {
    const result = await safeApi<LeaderboardResponse | null>(`/api/guilds/${guild.guildId}/leaderboard?days=${LEADERBOARD_DAYS}`, null)
    return result.data ? { snapshot: { ...guild, data: result.data }, failed: result.failed } : { snapshot: null, failed: result.failed }
  }))
  const leaderboards = leaderboardResults
    .map(result => result.snapshot)
    .filter(Boolean) as LeaderboardSnapshot[]
  const unavailable = [
    panelFailure(dashboardResult, 'dashboard', 'Dashboard summary'),
    panelFailure(periodDashboardResult, 'period-dashboard', 'Boon uptime and combat stats'),
    panelFailure(statsResult, 'stats', 'Character usage'),
    panelFailure(bestsResult, 'bests', 'Personal bests'),
    panelFailure(logsResult, 'logs', 'Recent logs'),
    panelFailure(activityLogsResult, 'activity-logs', 'Recent activity'),
    panelFailure(uploadsResult, 'uploads', 'Upload history'),
    panelFailure(guildsResult, 'guilds', 'Server access'),
    panelFailure(liveRaidGuildsResult, 'live-raid-guilds', 'Live raid servers'),
    leaderboardResults.some(result => result.failed)
      ? { key: 'leaderboards', label: 'Weekly rank snapshot' }
      : null,
  ].filter(Boolean) as DashboardPanelFailure[]

  return {
    dashboard: dashboardResult.data,
    periodDashboard: periodDashboardResult.data,
    stats: statsResult.data,
    bests: bestsResult.data,
    logs: logsResult.data,
    activityLogs: activityLogsResult.data,
    uploads: uploadsResult.data,
    guilds,
    liveRaidGuilds: liveRaidGuildsResult.data,
    leaderboards,
    unavailable,
  }
}

async function fetchLogsSince(startDateTime: string): Promise<ApiResult<LogsResponse>> {
  const data: LogRow[] = []
  let page = 1
  let total = 0

  while (page === 1 || data.length < total)
  {
    const result = await safeApi<LogsResponse>(
      `/api/logs?page=${page}&pageSize=${ACTIVITY_LOG_PAGE_SIZE}&startDateTime=${encodeURIComponent(startDateTime)}`,
      { total: data.length, data: [] }
    )
    if (result.failed) {
      return { data: { total: data.length, data }, failed: true }
    }

    total = result.data.total
    data.push(...result.data.data)
    if (result.data.data.length === 0) {
      break
    }
    page += 1
  }

  return { data: { total, data }, failed: false }
}

async function safeApi<T>(url: string, fallback: T): Promise<ApiResult<T>> {
  try
  {
    return { data: await api(url) as T, failed: false }
  }
  catch (error)
  {
    console.warn(`Dashboard request failed: ${url}`, error)
    return { data: fallback, failed: true }
  }
}

function panelFailure<T>(result: ApiResult<T>, key: string, label: string) {
  return result.failed ? { key, label } : null
}

function compareNullableText(a: string | null | undefined, b: string | null | undefined) {
  return (a ?? '').localeCompare(b ?? '')
}

function recentActivityStartDateTime() {
  const start = new Date()
  start.setUTCDate(start.getUTCDate() - RECENT_ACTIVITY_DAYS + 1)
  start.setUTCHours(0, 0, 0, 0)
  return start.toISOString()
}

async function loadTrend(fightType: number | null) {
  const requestId = ++trendRequestId
  if (fightType == null) {
    trendPoints.value = []
    trendUnavailable.value = false
    return
  }

  trendPending.value = true
  try
  {
    const since = new Date(Date.now() - TREND_DAYS * MS_PER_DAY).toISOString()
    const points = await safeApi<ProgressionPoint[]>(
      `/api/stats/progression?fightType=${fightType}&startDateTime=${encodeURIComponent(since)}`,
      []
    )
    if (requestId === trendRequestId) {
      trendPoints.value = points.data
      trendUnavailable.value = points.failed
    }
  }
  finally
  {
    if (requestId === trendRequestId) {
      trendPending.value = false
    }
  }
}

function makeLineDataset(label: string, data: number[], color: string) {
  const isDense = data.length > 60
  return {
    label,
    data,
    borderColor: color,
    backgroundColor: color.replace('1)', '0.15)'),
    tension: 0.35,
    pointRadius: isDense ? 0 : 3,
    pointHoverRadius: isDense ? 5 : 6,
    fill: false,
  }
}

function addBest(
  rows: { label: string; value: string; meta: string; fightLogId: number | null }[],
  label: string,
  entry: BestEntry | undefined,
  formatter: (value: number) => string
) {
  if (!entry) {
    return
  }
  rows.push({
    label,
    value: formatter(Number(entry.value ?? 0)),
    meta: `${fightName(entry.fightType)} · ${formatDate(entry.fightDate)}`,
    fightLogId: entry.fightLogId,
  })
}

function rankRow(
  leaderboard: LeaderboardSnapshot,
  label: string,
  rows: Record<string, any>[],
  field: string,
  descending: boolean,
  formatter: (value: number) => string
): WeeklyRankRow | null {
  if (!rows.length) {
    return null
  }
  const sorted = [...rows].sort((a, b) =>
    descending ? Number(b[field] ?? 0) - Number(a[field] ?? 0) : Number(a[field] ?? 0) - Number(b[field] ?? 0)
  )
  const index = sorted.findIndex(row => userAccountNames.value.has(String(row.accountName ?? '').toLowerCase()))
  if (index < 0) {
    return null
  }
  return {
    guildId: leaderboard.guildId,
    guildName: leaderboard.guildName,
    label,
    rank: index + 1,
    total: sorted.length,
    value: formatter(Number(sorted[index][field] ?? 0)),
    percentile: (sorted.length - index) / sorted.length,
  }
}

function metricWidth(value: number) {
  return `${Math.max(6, Math.round((value / maxCombatMetric.value) * 100))}%`
}

function openLog(fightLogId?: number | null) {
  if (fightLogId) {
    navigateTo(`/logs/${fightLogId}`)
  }
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString()
}

function formatShortDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}
</script>

<style scoped>
.dashboard-page {
  min-width: 0;
}

.dashboard-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.dashboard-title {
  margin-bottom: 0.15rem;
}

.dashboard-subtitle {
  color: var(--p-text-muted-color);
  font-size: 0.9rem;
}

.dashboard-notice {
  margin-bottom: 1rem;
}

.dashboard-stat-grid {
  grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
}

.account-card {
  grid-column: span 2;
}

.account-card-title {
  margin-bottom: 0.5rem;
}

.account-tags,
.server-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.dashboard-grid,
.wide-grid,
.trend-grid {
  display: grid;
  gap: 1rem;
  margin-bottom: 1rem;
}

.dashboard-grid {
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
}

.wide-grid {
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
}

.trend-grid {
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
}

.dashboard-panel {
  min-width: 0;
}

.doughnut-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  justify-items: center;
  gap: 1rem;
  min-height: 250px;
}

.doughnut-chart {
  width: min(220px, 100%);
  height: 190px;
}

.wide-chart {
  height: 320px;
}

.character-chart-scroll {
  max-height: 34rem;
  overflow-y: auto;
  padding-right: 0.35rem;
  scrollbar-gutter: stable;
}

.character-chart {
  min-height: 320px;
}

.trend-chart {
  height: 260px;
}

.legend-stack,
.meter-stack,
.metric-stack,
.rank-stack,
.log-stack,
.upload-stack {
  display: grid;
  gap: 0.7rem;
}

.legend-row,
.metric-label,
.meter-label,
.rank-row,
.log-row,
.upload-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.legend-row,
.rank-row,
.log-row,
.upload-row {
  border: 1px solid var(--p-surface-border);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.02);
  padding: 0.65rem 0.75rem;
}

.legend-stack,
.balance-copy {
  width: 100%;
  max-width: 24rem;
}

.legend-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
}

.legend-row span,
.rank-row span,
.log-row span,
.upload-row span {
  font-size: 0.84rem;
}

.log-copy {
  display: grid;
  gap: 0.15rem;
  min-width: 0;
}

.legend-row strong,
.rank-row strong,
.metric-row strong,
.meter-row strong {
  font-size: 0.86rem;
}

.legend-dot {
  width: 0.65rem;
  height: 0.65rem;
  border-radius: 999px;
  flex: 0 0 auto;
}

.legend-dot.wvw {
  background: rgba(99, 179, 237, 0.9);
}

.legend-dot.pve {
  background: rgba(123, 179, 91, 0.9);
}

.balance-copy {
  display: grid;
  gap: 0.75rem;
}

.balance-copy div {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 1rem;
  border: 1px solid var(--p-surface-border);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.02);
  padding: 0.65rem 0.75rem;
}

.legend-row strong,
.balance-copy strong {
  white-space: nowrap;
}

.balance-copy span,
.access-grid small,
.log-row small,
.upload-row small,
.best-tile small {
  color: var(--p-text-muted-color);
  font-size: 0.75rem;
}

.meter-track,
.metric-track {
  width: 100%;
  height: 0.55rem;
  overflow: hidden;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.14);
}

.meter-fill,
.metric-fill {
  display: block;
  height: 100%;
  min-width: 0.35rem;
  border-radius: inherit;
}

.fill-blue {
  background: rgba(99, 179, 237, 0.9);
}

.fill-green {
  background: rgba(123, 179, 91, 0.9);
}

.fill-amber {
  background: rgba(250, 204, 21, 0.9);
}

.fill-teal {
  background: rgba(45, 212, 191, 0.85);
}

.fill-red {
  background: rgba(219, 44, 67, 0.9);
}

.fill-slate {
  background: rgba(148, 163, 184, 0.85);
}

.rank-badge {
  display: inline-flex;
  align-items: baseline;
  gap: 0.15rem;
  color: var(--p-primary-color);
  font-weight: 700;
}

.rank-badge small {
  color: var(--p-text-muted-color);
  font-weight: 500;
}

.rank-copy {
  display: grid;
  gap: 0.15rem;
}

.rank-copy strong {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  align-items: baseline;
}

.rank-copy strong span {
  font-size: inherit;
}

.panel-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1rem;
  min-width: 0;
}

.panel-title-row > span {
  min-width: 0;
}

.panel-title-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 0.5rem;
  min-width: 0;
}

.limit-tag {
  flex: 0 0 auto;
  white-space: nowrap;
}

.trend-select {
  min-width: 180px;
}

.best-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 0.75rem;
}

.best-tile,
.log-row {
  position: relative;
  width: 100%;
  color: var(--p-text-color);
  cursor: pointer;
  text-align: left;
  transition: border-color 0.15s, transform 0.15s;
}

.best-tile {
  display: grid;
  gap: 0.2rem;
  min-height: 112px;
  border: 1px solid var(--p-surface-border);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.02);
  padding: 0.85rem;
}

.best-tile:disabled {
  cursor: default;
}

.best-tile:not(:disabled):hover,
.log-row:hover {
  border-color: var(--p-primary-color);
  transform: translateY(-1px);
}

.best-tile span {
  color: var(--p-text-muted-color);
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0;
  text-transform: uppercase;
}

.best-tile strong {
  font-size: 1.35rem;
  line-height: 1.15;
}

.best-tile i {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  color: var(--p-primary-color);
  font-size: 0.75rem;
}

.access-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  margin-bottom: 0.9rem;
}

.access-grid > div {
  border: 1px solid var(--p-surface-border);
  border-radius: 8px;
  padding: 0.8rem;
}

.access-value {
  display: block;
  font-size: 1.55rem;
  font-weight: 700;
  line-height: 1.1;
}

@media (max-width: 640px) {
  .dashboard-heading {
    align-items: center;
  }

  .account-card {
    grid-column: span 1;
  }

  .wide-grid,
  .trend-grid {
    grid-template-columns: 1fr;
  }

  .doughnut-layout {
    grid-template-columns: 1fr;
    min-height: 0;
  }

  .doughnut-chart {
    width: min(200px, 100%);
    height: 180px;
  }

  .panel-title-row {
    align-items: flex-start;
    flex-direction: column;
  }

  .panel-title-actions {
    justify-content: flex-start;
    width: 100%;
  }

  .trend-select {
    width: 100%;
  }
}
</style>
