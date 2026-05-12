<template>
  <div>
    <!-- Header -->
    <div style="display: flex; align-items: baseline; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem;">
      <h1 class="page-title" style="margin: 0;">{{ fightName(data.log.fightType) }}</h1>
      <span style="color: var(--p-text-muted-color); font-size: 0.875rem;">
        {{ new Date(data.log.fightStart).toLocaleString() }} · {{ formatDuration(data.log.fightDurationInMs) }}
      </span>
      <Tag v-if="data.log.fightType !== 0" :severity="data.log.isSuccess ? 'success' : 'danger'" :value="data.log.isSuccess ? 'Kill' : `${data.log.fightPercent}% - Wipe`" />
      <div class="header-actions">
        <NuxtLink v-if="showProgressionLink" :to="`/progression?fightType=${data.log.fightType}`" class="header-btn">
          <i class="pi pi-chart-line" /> Progression
        </NuxtLink>
        <a v-if="data.log.url" :href="data.log.url" target="_blank" rel="noopener" class="header-btn">
          <i class="pi pi-external-link" /> View Log
        </a>
        <button v-if="showWingmanButton && data.log.fightType !== 0 && data.log.url" class="header-btn" :disabled="wingmanQueued" @click="uploadToWingman">
          <i class="pi pi-upload" />
          {{ wingmanQueued ? 'Queued!' : 'Wingman' }}
        </button>
      </div>
    </div>

    <!-- WvW layout -->
    <template v-if="data.log.fightType === 0">
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <Card class="stat-card agg-card">
          <template #content>
            <div class="stat-label">Attacks Missed</div>
            <div class="agg-row"><span class="agg-side">Ours (blinded)</span><span v-fit-text class="stat-value">{{ sum('numberOfHitsWhileBlinded').toLocaleString() }}</span></div>
            <div class="agg-row"><span class="agg-side">Theirs (missed)</span><span v-fit-text class="stat-value">{{ sum('numberOfMissesAgainst').toLocaleString() }}</span></div>
          </template>
        </Card>
        <Card class="stat-card agg-card">
          <template #content>
            <div class="stat-label">Attacks Blocked</div>
            <div class="agg-row"><span class="agg-side">Ours</span><span v-fit-text class="stat-value">{{ sum('numberOfTimesBlockedAttack').toLocaleString() }}</span></div>
            <div class="agg-row"><span class="agg-side">Theirs</span><span v-fit-text class="stat-value">{{ sum('numberOfTimesEnemyBlockedAttack').toLocaleString() }}</span></div>
          </template>
        </Card>
        <Card class="stat-card agg-card">
          <template #content>
            <div class="stat-label">Boons Stripped</div>
            <div class="agg-row"><span class="agg-side">Ours (strips)</span><span v-fit-text class="stat-value">{{ sum('strips').toLocaleString() }}</span></div>
            <div class="agg-row"><span class="agg-side">Theirs (ripped)</span><span v-fit-text class="stat-value">{{ sum('numberOfBoonsRipped').toLocaleString() }}</span></div>
          </template>
        </Card>
        <Card class="stat-card agg-card">
          <template #content>
            <div class="stat-label">Damage Taken vs Barrier</div>
            <div class="agg-row"><span class="agg-side">Dmg Taken</span><span v-fit-text class="stat-value">{{ sum('damageTaken').toLocaleString() }}</span></div>
            <div class="agg-row"><span class="agg-side">Barrier Mit.</span><span v-fit-text class="stat-value">{{ sum('barrierMitigation').toLocaleString() }}</span></div>
            <div class="agg-row"><span class="agg-side">Mit. %</span><span v-fit-text class="stat-value">{{ sum('damageTaken') > 0 ? ((sum('barrierMitigation') / sum('damageTaken')) * 100).toFixed(1) : '0.0' }}%</span></div>
          </template>
        </Card>
      </div>
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <Card class="stat-card">
          <template #content><div class="stat-label">Players</div><div v-fit-text class="stat-value">{{ data.players.length }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Damage</div><div v-fit-text class="stat-value">{{ sum('damage').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Down Contribution</div><div v-fit-text class="stat-value">{{ sum('damageDownContribution').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Kills</div><div v-fit-text class="stat-value">{{ sum('kills').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Downs</div><div v-fit-text class="stat-value">{{ sum('downs').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Deaths</div><div v-fit-text class="stat-value">{{ sum('deaths').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Healing</div><div v-fit-text class="stat-value">{{ sum('healing').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Cleanses</div><div v-fit-text class="stat-value">{{ sum('cleanses').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Strips</div><div v-fit-text class="stat-value">{{ sum('strips').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Barrier</div><div v-fit-text class="stat-value">{{ sum('barrierGenerated').toLocaleString() }}</div></template>
        </Card>
      </div>
      <CollapsibleSection title="Players">
        <DataTable :value="wvwPlayers" striped-rows scrollable>
          <Column field="guildWarsAccountName" header="Account" frozen style="min-width: 160px;" />
          <Column field="subGroup" header="Sub" style="width: 4rem;" />
          <Column header="Damage" :sortable="true" sort-field="damage">
            <template #body="{ data: row }">{{ row.damage.toLocaleString() }}</template>
          </Column>
          <Column header="Down C." :sortable="true" sort-field="damageDownContribution">
            <template #body="{ data: row }">{{ row.damageDownContribution.toLocaleString() }}</template>
          </Column>
          <Column header="Kills" :sortable="true" sort-field="kills">
            <template #body="{ data: row }">{{ row.kills }}</template>
          </Column>
          <Column header="Downs" :sortable="true" sort-field="downs">
            <template #body="{ data: row }">{{ row.downs }}</template>
          </Column>
          <Column header="Deaths" :sortable="true" sort-field="deaths">
            <template #body="{ data: row }">{{ row.deaths }}</template>
          </Column>
          <Column header="Healing" :sortable="true" sort-field="healing">
            <template #body="{ data: row }">{{ row.healing.toLocaleString() }}</template>
          </Column>
          <Column header="Cleanses" :sortable="true" sort-field="cleanses">
            <template #body="{ data: row }">{{ row.cleanses.toLocaleString() }}</template>
          </Column>
          <Column header="Strips" :sortable="true" sort-field="strips">
            <template #body="{ data: row }">{{ row.strips.toLocaleString() }}</template>
          </Column>
          <Column header="Barrier" :sortable="true" sort-field="barrierGenerated">
            <template #body="{ data: row }">{{ row.barrierGenerated.toLocaleString() }}</template>
          </Column>
          <Column header="Stab (On)" :sortable="true" sort-field="stabGenOnGroup">
            <template #body="{ data: row }">{{ Number(row.stabGenOnGroup).toFixed(2) }}</template>
          </Column>
          <Column header="Stab (Off)" :sortable="true" sort-field="stabGenOffGroup">
            <template #body="{ data: row }">{{ Number(row.stabGenOffGroup).toFixed(2) }}</template>
          </Column>
          <Column header="Quick%" :sortable="true" sort-field="quicknessDuration">
            <template #body="{ data: row }">{{ Number(row.quicknessDuration).toFixed(1) }}%</template>
          </Column>
          <Column header="Downed" :sortable="true" sort-field="timesDowned">
            <template #body="{ data: row }">{{ row.timesDowned }}</template>
          </Column>
          <Column header="Dist. Tag" :sortable="true" sort-field="distanceFromTag">
            <template #body="{ data: row }">{{ Number(row.distanceFromTag).toFixed(0) }}</template>
          </Column>
        </DataTable>
      </CollapsibleSection>
    </template>

    <!-- PvE layout -->
    <template v-else>
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <Card class="stat-card">
          <template #content><div class="stat-label">Players</div><div v-fit-text class="stat-value">{{ data.players.length }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Damage</div><div v-fit-text class="stat-value">{{ sum('damage').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Cleave</div><div v-fit-text class="stat-value">{{ sum('cleave').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Avg Alac%</div><div v-fit-text class="stat-value">{{ avg('alacDuration').toFixed(1) }}%</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Avg Quick%</div><div v-fit-text class="stat-value">{{ avg('quicknessDuration').toFixed(1) }}%</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Deaths</div><div v-fit-text class="stat-value">{{ sum('deaths').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Times Downed</div><div v-fit-text class="stat-value">{{ sum('timesDowned').toLocaleString() }}</div></template>
        </Card>
        <Card class="stat-card">
          <template #content><div class="stat-label">Total Dmg Taken</div><div v-fit-text class="stat-value">{{ sum('damageTaken').toLocaleString() }}</div></template>
        </Card>
      </div>
      <Tabs value="overview">
        <TabList>
          <Tab value="overview">Damage & Combat</Tab>
          <Tab value="survivability">Survivability</Tab>
          <Tab v-if="mechanicNames.length > 0" value="mechanics">Mechanics</Tab>
        </TabList>
        <TabPanels>
          <TabPanel value="overview">
            <DataTable :value="pvePlayersSortedByDamage" striped-rows scrollable>
              <Column field="guildWarsAccountName" header="Account" frozen style="min-width: 160px;" />
              <Column header="Damage" :sortable="true" sort-field="damage">
                <template #body="{ data: row }">{{ row.damage.toLocaleString() }}</template>
              </Column>
              <Column header="Cleave" :sortable="true" sort-field="cleave">
                <template #body="{ data: row }">{{ row.cleave.toLocaleString() }}</template>
              </Column>
              <Column header="Alac%" :sortable="true" sort-field="alacDuration">
                <template #body="{ data: row }">{{ Number(row.alacDuration).toFixed(2) }}%</template>
              </Column>
              <Column header="Quick%" :sortable="true" sort-field="quicknessDuration">
                <template #body="{ data: row }">{{ Number(row.quicknessDuration).toFixed(2) }}%</template>
              </Column>
            </DataTable>
          </TabPanel>

          <TabPanel value="survivability">
            <DataTable :value="pvePlayersSortedByRes" striped-rows scrollable>
              <Column field="guildWarsAccountName" header="Account" style="min-width: 160px;" />
              <Column header="Res (s)" :sortable="true" sort-field="resurrectionTime">
                <template #body="{ data: row }">{{ (row.resurrectionTime / 1000).toFixed(1) }}</template>
              </Column>
              <Column header="Dmg Taken" :sortable="true" sort-field="damageTaken">
                <template #body="{ data: row }">{{ row.damageTaken.toLocaleString() }}</template>
              </Column>
              <Column header="Downed" :sortable="true" sort-field="timesDowned">
                <template #body="{ data: row }">{{ row.timesDowned }}</template>
              </Column>
              <Column header="Deaths" :sortable="true" sort-field="deaths">
                <template #body="{ data: row }">{{ row.deaths }}</template>
              </Column>
            </DataTable>
          </TabPanel>

          <TabPanel v-if="mechanicNames.length > 0" value="mechanics">
            <div class="mechanic-summary-grid" style="margin-bottom: 1rem;">
              <div v-for="name in mechanicNames" :key="'sum-'+name" class="mechanic-summary-card">
                <div class="mechanic-summary-name" :title="name">{{ name }}</div>
                <div class="mechanic-summary-row">
                  <span class="mechanic-summary-label">Max</span>
                  <a v-if="data.log.url && (mechanicMax[name]?.value ?? 0) > 0" :href="data.log.url" target="_blank" rel="noopener" class="mechanic-max-link" :title="`${mechanicMax[name]?.account}: ${mechanicMax[name]?.value}`">{{ mechanicMax[name]?.value ?? 0 }}</a>
                  <span v-else>{{ mechanicMax[name]?.value ?? 0 }}</span>
                </div>
                <div class="mechanic-summary-row">
                  <span class="mechanic-summary-label">Avg</span>
                  <span>{{ mechanicAvg[name]?.toFixed(1) ?? '0.0' }}</span>
                </div>
                <div class="mechanic-summary-row">
                  <span class="mechanic-summary-label">Median</span>
                  <span>{{ mechanicMedian[name] ?? 0 }}</span>
                </div>
              </div>
            </div>
            <DataTable :value="mechanicTableRows" striped-rows scrollable>
              <Column field="account" header="Account" style="min-width: 160px;" frozen />
              <Column v-for="name in mechanicNames" :key="name" :field="name" :header="name" :sortable="true" header-style="white-space: nowrap">
                <template #body="{ data: row }">{{ row[name] ?? 0 }}</template>
              </Column>
            </DataTable>
          </TabPanel>
        </TabPanels>
      </Tabs>
    </template>
  </div>
</template>

<script setup lang="ts">
import CollapsibleSection from '~/components/CollapsibleSection.vue'

const props = defineProps<{
  data: { log: any; players: any[]; mechanics: any[] }
  showProgressionLink?: boolean
  showWingmanButton?: boolean
}>()

const showProgressionLink = computed(() => props.showProgressionLink ?? true)
const showWingmanButton = computed(() => props.showWingmanButton ?? true)

const api = useApi()

const sum = (field: string) =>
  (props.data.players ?? []).reduce((s: number, p: any) => s + (Number(p[field]) || 0), 0)

const avg = (field: string) => {
  const p = props.data.players ?? []
  return p.length === 0 ? 0 : sum(field) / p.length
}

const wvwPlayers = computed(() =>
  [...(props.data.players ?? [])].sort((a, b) => b.damage - a.damage)
)

const pvePlayersSortedByDamage = computed(() =>
  [...(props.data.players ?? [])].sort((a, b) => b.damage - a.damage)
)

const pvePlayersSortedByRes = computed(() =>
  [...(props.data.players ?? [])].sort((a, b) => a.resurrectionTime - b.resurrectionTime)
)

const mechanicNames = computed(() => {
  const names = new Set<string>()
  for (const m of (props.data.mechanics ?? [])) {
    if (m.mechanicCount > 0) names.add(m.mechanicName)
  }
  return [...names].sort()
})

const mechanicTableRows = computed(() => {
  const playerLogIds: Record<number, string> = {}
  for (const p of (props.data.players ?? [])) {
    playerLogIds[p.playerFightLogId] = p.guildWarsAccountName
  }

  const byAccount: Record<string, Record<string, number>> = {}
  for (const m of (props.data.mechanics ?? [])) {
    if (m.mechanicCount <= 0) {
      continue
    }
    const account = playerLogIds[m.playerFightLogId] ?? '?'
    if (!byAccount[account]) {
      byAccount[account] = {}
    }
    byAccount[account][m.mechanicName] = (byAccount[account][m.mechanicName] ?? 0) + m.mechanicCount
  }

  return Object.entries(byAccount)
    .map(([account, counts]) => ({ account, ...counts }))
    .sort((a, b) => a.account.localeCompare(b.account))
})

const mechanicMax = computed(() => {
  const result: Record<string, { value: number; account: string }> = {}
  for (const name of mechanicNames.value) {
    let maxVal = 0
    let maxAccount = ''
    for (const row of mechanicTableRows.value as any[]) {
      const v = Number(row[name] ?? 0)
      if (v > maxVal) {
        maxVal = v
        maxAccount = row.account
      }
    }
    result[name] = { value: maxVal, account: maxAccount }
  }
  return result
})

const mechanicAvg = computed(() => {
  const result: Record<string, number> = {}
  for (const name of mechanicNames.value) {
    const vals = (mechanicTableRows.value as any[]).map(r => Number(r[name] ?? 0)).filter(v => v > 0)
    result[name] = vals.length ? vals.reduce((a: number, b: number) => a + b, 0) / vals.length : 0
  }
  return result
})

const mechanicMedian = computed(() => {
  const result: Record<string, number> = {}
  for (const name of mechanicNames.value) {
    const vals = (mechanicTableRows.value as any[]).map(r => Number(r[name] ?? 0)).sort((a, b) => a - b)
    const mid = Math.floor(vals.length / 2)
    result[name] = vals.length % 2 === 0 ? ((vals[mid - 1] ?? 0) + (vals[mid] ?? 0)) / 2 : (vals[mid] ?? 0)
  }
  return result
})

const FIGHT_NAMES: Record<number, string> = {
  0: 'WvW', 1: 'Vale Guardian', 2: 'Gorseval', 3: 'Sabetha', 4: 'Slothasor',
  5: 'Trio', 6: 'Matthias', 7: 'Escort', 8: 'Keep Construct', 9: 'Twisted Castle',
  10: 'Xera', 11: 'Cairn', 12: 'Mursaat Overseer', 13: 'Samarog', 14: 'Deimos',
  15: 'Soulless Horror', 16: 'River of Souls', 17: 'Broken King', 18: 'Eater of Souls',
  19: 'Voice in the Void', 20: 'Dhuum', 21: 'Conjured Amalgamate', 22: 'Twin Largos',
  23: 'Qadim', 24: 'Cardinal Adina', 25: 'Cardinal Sabir', 26: 'Qadim the Peerless',
  27: 'Aetherblade Hideout', 28: 'Xunlai Jade Junkyard', 29: 'Kaineng Overlook',
  30: 'Harvest Temple', 31: "Old Lion's Court", 32: 'Cosmic Observatory', 33: 'Temple of Febe',
  34: 'MAMA', 35: 'Siax', 36: 'Ensolyss', 37: 'Skorvald', 38: 'Artsariiv', 39: 'Arkk',
  40: 'Ai (Ele)', 41: 'Ai (Dark)', 42: 'Ai (Both)', 43: 'Kanaxai', 44: 'Greer',
  45: 'Decima', 46: 'Ura', 47: 'Icebrood Construct', 48: 'Fraenir', 49: 'Voice of the Fallen',
  50: 'Whisper of Jormag', 51: 'Boneskinner', 52: 'Eparch', 53: 'Spirit Woods',
  54: 'Shadow of the Dragon', 55: 'Kela', 32766: 'Golem',
}

const fightName = (type: number) => FIGHT_NAMES[type] ?? 'Unknown'

const formatDuration = (ms: number) => {
  const s = Math.floor(ms / 1000)
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}

const wingmanQueued = ref(false)

const uploadToWingman = () => {
  if (wingmanQueued.value) {
    return
  }
  wingmanQueued.value = true
  api('/api/logs/wingman', {
    method: 'POST',
    body: { logIds: [props.data.log.fightLogId] },
  }).catch(() => {})
}
</script>

<style scoped>
.header-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: auto;
}

.header-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.75rem;
  font-size: 0.8rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-border-radius-sm, 4px);
  color: var(--p-text-color);
  text-decoration: none;
  transition: border-color 0.15s, color 0.15s;
}

.header-btn:hover {
  border-color: var(--p-primary-color);
  color: var(--p-primary-color);
}

.mechanic-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.mechanic-summary-card {
  background: var(--p-surface-ground);
  border: 1px solid var(--p-surface-border);
  border-radius: 0.5rem;
  padding: 0.5rem 0.75rem;
  font-size: 0.8rem;
}

.mechanic-summary-name {
  font-weight: 600;
  color: var(--p-text-muted-color);
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  margin-bottom: 0.35rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mechanic-summary-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.25rem;
  line-height: 1.6;
}

.mechanic-summary-label {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
}

.mechanic-max-link {
  color: var(--p-primary-color);
  text-decoration: none;
  font-weight: 600;
}

.mechanic-max-link:hover {
  text-decoration: underline;
}

/* noinspection CssUnusedSymbol */
.agg-card :deep(.p-card-content) {
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
