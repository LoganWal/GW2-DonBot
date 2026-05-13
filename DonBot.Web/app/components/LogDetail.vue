<template>
  <div>
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
        <AggCard
          label="Attacks Missed"
          :rows="[
            { side: 'Ours (blinded)', value: sum('numberOfHitsWhileBlinded') },
            { side: 'Theirs (missed)', value: sum('numberOfMissesAgainst') },
          ]"
        />
        <AggCard
          label="Attacks Blocked"
          :rows="[
            { side: 'Ours', value: sum('numberOfTimesBlockedAttack') },
            { side: 'Theirs', value: sum('numberOfTimesEnemyBlockedAttack') },
          ]"
        />
        <AggCard
          label="Boons Stripped"
          :rows="[
            { side: 'Ours (strips)', value: sum('strips') },
            { side: 'Theirs (ripped)', value: sum('numberOfBoonsRipped') },
          ]"
        />
        <AggCard
          label="Damage vs Barrier"
          :rows="[
            { side: 'Dmg Taken', value: sum('damageTaken') },
            { side: 'Barrier Mit.', value: sum('barrierMitigation') },
            { side: 'Mit. %', value: sum('damageTaken') > 0 ? ((sum('barrierMitigation') / sum('damageTaken')) * 100).toFixed(1) + '%' : '0.0%' },
          ]"
        />
      </div>
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <StatCard label="Players" :value="data.players.length" />
        <StatCard label="Total Damage" :value="sum('damage')" />
        <StatCard label="Down Contribution" :value="sum('damageDownContribution')" />
        <StatCard label="Kills" :value="sum('kills')" />
        <StatCard label="Downs" :value="sum('downs')" />
        <StatCard label="Deaths" :value="sum('deaths')" />
        <StatCard label="Total Healing" :value="sum('healing')" />
        <StatCard label="Total Cleanses" :value="sum('cleanses')" />
        <StatCard label="Total Strips" :value="sum('strips')" />
        <StatCard label="Total Barrier" :value="sum('barrierGenerated')" />
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
        <StatCard label="Players" :value="data.players.length" />
        <StatCard label="Total Damage" :value="sum('damage')" />
        <StatCard label="Total Cleave" :value="sum('cleave')" />
        <StatCard label="Avg Alac%" :value="avg('alacDuration').toFixed(1) + '%'" />
        <StatCard label="Avg Quick%" :value="avg('quicknessDuration').toFixed(1) + '%'" />
        <StatCard label="Deaths" :value="sum('deaths')" />
        <StatCard label="Times Downed" :value="sum('timesDowned')" />
        <StatCard label="Total Dmg Taken" :value="sum('damageTaken')" />
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
              <MechanicSummaryCard
                v-for="name in mechanicNames"
                :key="'sum-'+name"
                :name="name"
                :max-value="mechanicMax[name]?.value ?? 0"
                :max-link="data.log.url && (mechanicMax[name]?.value ?? 0) > 0 ? data.log.url : null"
                :avg="(mechanicAvg[name]?.toFixed(1) ?? '0.0')"
                :median="mechanicMedian[name] ?? 0"
              />
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
import { fightName, formatDuration } from '~/composables/useFightTypes'
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
</style>
