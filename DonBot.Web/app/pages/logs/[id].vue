<template>
  <div>
<ProgressSpinner v-if="pending" />
    <template v-else-if="fight">
      <!-- Header -->
      <div style="display: flex; align-items: baseline; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem;">
        <h1 class="page-title" style="margin: 0;">{{ fightName(fight.log.fightType) }}</h1>
        <span style="color: var(--p-text-muted-color); font-size: 0.875rem;">
          {{ new Date(fight.log.fightStart).toLocaleString() }} · {{ formatDuration(fight.log.fightDurationInMs) }}
        </span>
        <Tag v-if="fight.log.fightType !== 0" :severity="fight.log.isSuccess ? 'success' : 'danger'" :value="fight.log.isSuccess ? 'Kill' : `${fight.log.fightPercent}% — Wipe`" />
        <div class="header-actions">
          <NuxtLink :to="`/progression?fightType=${fight.log.fightType}`" class="header-btn">
            <i class="pi pi-chart-line" /> Progression
          </NuxtLink>
          <a v-if="fight.log.url" :href="fight.log.url" target="_blank" rel="noopener" class="header-btn">
            <i class="pi pi-external-link" /> View Log
          </a>
          <button v-if="fight.log.fightType !== 0 && fight.log.url" class="header-btn" :disabled="wingmanQueued" @click="uploadToWingman">
            <i class="pi pi-upload" />
            {{ wingmanQueued ? 'Queued!' : 'Wingman' }}
          </button>
        </div>
      </div>

      <!-- WvW layout -->
      <template v-if="fight.log.fightType === 0">
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <Card class="stat-card agg-card">
            <template #content>
              <div class="stat-label">Attacks Missed</div>
              <div class="agg-row"><span class="agg-side">Ours (blinded)</span><span class="stat-value">{{ sum('numberOfHitsWhileBlinded').toLocaleString() }}</span></div>
              <div class="agg-row"><span class="agg-side">Theirs (missed)</span><span class="stat-value">{{ sum('numberOfMissesAgainst').toLocaleString() }}</span></div>
            </template>
          </Card>
          <Card class="stat-card agg-card">
            <template #content>
              <div class="stat-label">Attacks Blocked</div>
              <div class="agg-row"><span class="agg-side">Ours</span><span class="stat-value">{{ sum('numberOfTimesBlockedAttack').toLocaleString() }}</span></div>
              <div class="agg-row"><span class="agg-side">Theirs</span><span class="stat-value">{{ sum('numberOfTimesEnemyBlockedAttack').toLocaleString() }}</span></div>
            </template>
          </Card>
          <Card class="stat-card agg-card">
            <template #content>
              <div class="stat-label">Boons Stripped</div>
              <div class="agg-row"><span class="agg-side">Ours (strips)</span><span class="stat-value">{{ sum('strips').toLocaleString() }}</span></div>
              <div class="agg-row"><span class="agg-side">Theirs (ripped)</span><span class="stat-value">{{ sum('numberOfBoonsRipped').toLocaleString() }}</span></div>
            </template>
          </Card>
          <Card class="stat-card agg-card">
            <template #content>
              <div class="stat-label">Damage Taken vs Barrier</div>
              <div class="agg-row"><span class="agg-side">Dmg Taken</span><span class="stat-value">{{ sum('damageTaken').toLocaleString() }}</span></div>
              <div class="agg-row"><span class="agg-side">Barrier Mit.</span><span class="stat-value">{{ sum('barrierMitigation').toLocaleString() }}</span></div>
              <div class="agg-row"><span class="agg-side">Mit. %</span><span class="stat-value">{{ sum('damageTaken') > 0 ? ((sum('barrierMitigation') / sum('damageTaken')) * 100).toFixed(1) : '0.0' }}%</span></div>
            </template>
          </Card>
        </div>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <Card class="stat-card">
            <template #content><div class="stat-label">Players</div><div class="stat-value">{{ fight.players.length }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Damage</div><div class="stat-value">{{ sum('damage').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Down Contribution</div><div class="stat-value">{{ sum('damageDownContribution').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Kills</div><div class="stat-value">{{ sum('kills').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Downs</div><div class="stat-value">{{ sum('downs').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Deaths</div><div class="stat-value">{{ sum('deaths').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Healing</div><div class="stat-value">{{ sum('healing').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Cleanses</div><div class="stat-value">{{ sum('cleanses').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Strips</div><div class="stat-value">{{ sum('strips').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Barrier</div><div class="stat-value">{{ sum('barrierGenerated').toLocaleString() }}</div></template>
          </Card>

        </div>
        <h2 class="section-title">Players</h2>
        <DataTable :value="wvwPlayers" striped-rows scrollable>
          <Column field="guildWarsAccountName" header="Account" frozen style="min-width: 160px;" />
          <Column field="subGroup" header="Sub" style="width: 4rem;" />
          <Column header="Damage" sortable sort-field="damage">
            <template #body="{ data }">{{ data.damage.toLocaleString() }}</template>
          </Column>
          <Column header="Down C." sortable sort-field="damageDownContribution">
            <template #body="{ data }">{{ data.damageDownContribution.toLocaleString() }}</template>
          </Column>
          <Column header="Kills" sortable sort-field="kills">
            <template #body="{ data }">{{ data.kills }}</template>
          </Column>
          <Column header="Downs" sortable sort-field="downs">
            <template #body="{ data }">{{ data.downs }}</template>
          </Column>
          <Column header="Deaths" sortable sort-field="deaths">
            <template #body="{ data }">{{ data.deaths }}</template>
          </Column>
          <Column header="Healing" sortable sort-field="healing">
            <template #body="{ data }">{{ data.healing.toLocaleString() }}</template>
          </Column>
          <Column header="Cleanses" sortable sort-field="cleanses">
            <template #body="{ data }">{{ data.cleanses.toLocaleString() }}</template>
          </Column>
          <Column header="Strips" sortable sort-field="strips">
            <template #body="{ data }">{{ data.strips.toLocaleString() }}</template>
          </Column>
          <Column header="Barrier" sortable sort-field="barrierGenerated">
            <template #body="{ data }">{{ data.barrierGenerated.toLocaleString() }}</template>
          </Column>
          <Column header="Stab (On)" sortable sort-field="stabGenOnGroup">
            <template #body="{ data }">{{ Number(data.stabGenOnGroup).toFixed(2) }}</template>
          </Column>
          <Column header="Stab (Off)" sortable sort-field="stabGenOffGroup">
            <template #body="{ data }">{{ Number(data.stabGenOffGroup).toFixed(2) }}</template>
          </Column>
          <Column header="Quick%" sortable sort-field="quicknessDuration">
            <template #body="{ data }">{{ Number(data.quicknessDuration).toFixed(1) }}%</template>
          </Column>
          <Column header="Downed" sortable sort-field="timesDowned">
            <template #body="{ data }">{{ data.timesDowned }}</template>
          </Column>
          <Column header="Dist. Tag" sortable sort-field="distanceFromTag">
            <template #body="{ data }">{{ Number(data.distanceFromTag).toFixed(0) }}</template>
          </Column>
        </DataTable>

      </template>

      <!-- PvE layout -->
      <template v-else>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <Card class="stat-card">
            <template #content><div class="stat-label">Players</div><div class="stat-value">{{ fight.players.length }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Damage</div><div class="stat-value">{{ sum('damage').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Cleave</div><div class="stat-value">{{ sum('cleave').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Avg Alac%</div><div class="stat-value">{{ avg('alacDuration').toFixed(1) }}%</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Avg Quick%</div><div class="stat-value">{{ avg('quicknessDuration').toFixed(1) }}%</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Deaths</div><div class="stat-value">{{ sum('deaths').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Times Downed</div><div class="stat-value">{{ sum('timesDowned').toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Dmg Taken</div><div class="stat-value">{{ sum('damageTaken').toLocaleString() }}</div></template>
          </Card>
        </div>
        <h2 class="section-title">Player Overview</h2>
        <DataTable :value="pvePlayersSortedByDamage" striped-rows scrollable>
          <Column field="guildWarsAccountName" header="Account" frozen style="min-width: 160px;" />
          <Column header="Damage" sortable sort-field="damage">
            <template #body="{ data }">{{ data.damage.toLocaleString() }}</template>
          </Column>
          <Column header="Cleave" sortable sort-field="cleave">
            <template #body="{ data }">{{ data.cleave.toLocaleString() }}</template>
          </Column>
          <Column header="Alac%" sortable sort-field="alacDuration">
            <template #body="{ data }">{{ Number(data.alacDuration).toFixed(2) }}%</template>
          </Column>
          <Column header="Quick%" sortable sort-field="quicknessDuration">
            <template #body="{ data }">{{ Number(data.quicknessDuration).toFixed(2) }}%</template>
          </Column>
        </DataTable>

        <h2 class="section-title">Survivability</h2>
        <DataTable :value="pvePlayersSortedByRes" striped-rows scrollable>
          <Column field="guildWarsAccountName" header="Account" style="min-width: 160px;" />
          <Column header="Res (s)" sortable sort-field="resurrectionTime">
            <template #body="{ data }">{{ (data.resurrectionTime / 1000).toFixed(1) }}</template>
          </Column>
          <Column header="Dmg Taken" sortable sort-field="damageTaken">
            <template #body="{ data }">{{ data.damageTaken.toLocaleString() }}</template>
          </Column>
          <Column header="Downed" sortable sort-field="timesDowned">
            <template #body="{ data }">{{ data.timesDowned }}</template>
          </Column>
          <Column header="Deaths" sortable sort-field="deaths">
            <template #body="{ data }">{{ data.deaths }}</template>
          </Column>
        </DataTable>

        <template v-if="hasCerusMechanics">
          <h2 class="section-title">Cerus Mechanics</h2>
          <DataTable :value="pvePlayersSortedByDamage" striped-rows scrollable>
            <Column field="guildWarsAccountName" header="Account" style="min-width: 160px;" />
            <Column header="P1 Damage" sortable sort-field="cerusPhaseOneDamage">
              <template #body="{ data }">{{ Number(data.cerusPhaseOneDamage).toLocaleString() }}</template>
            </Column>
            <Column header="Orbs" sortable sort-field="cerusOrbsCollected">
              <template #body="{ data }">{{ data.cerusOrbsCollected }}</template>
            </Column>
            <Column header="Spread Hits" sortable sort-field="cerusSpreadHitCount">
              <template #body="{ data }">{{ data.cerusSpreadHitCount }}</template>
            </Column>
          </DataTable>
        </template>

        <template v-if="hasDeimoseMechanics">
          <h2 class="section-title">Deimos Mechanics</h2>
          <DataTable :value="pvePlayersSortedByDamage" striped-rows scrollable>
            <Column field="guildWarsAccountName" header="Account" style="min-width: 160px;" />
            <Column header="Oils Triggered" sortable sort-field="deimosOilsTriggered">
              <template #body="{ data }">{{ data.deimosOilsTriggered }}</template>
            </Column>
          </DataTable>
        </template>

        <template v-if="hasUraMechanics">
          <h2 class="section-title">Ura Mechanics</h2>
          <DataTable :value="pvePlayersSortedByDamage" striped-rows scrollable>
            <Column field="guildWarsAccountName" header="Account" style="min-width: 160px;" />
            <Column header="Shards Picked" sortable sort-field="shardPickUp">
              <template #body="{ data }">{{ data.shardPickUp }}</template>
            </Column>
            <Column header="Shards Used" sortable sort-field="shardUsed">
              <template #body="{ data }">{{ data.shardUsed }}</template>
            </Column>
          </DataTable>
        </template>
      </template>
    </template>
    <p v-else-if="!pending">Log not found.</p>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const route = useRoute()

const { data: fight, pending } = await useAsyncData(
  `log-${route.params.id}`,
  () => api(`/api/logs/${route.params.id}`) as Promise<{ log: any; players: any[] }>
)

const sum = (field: string) =>
  (fight.value?.players ?? []).reduce((s: number, p: any) => s + (Number(p[field]) || 0), 0)

const avg = (field: string) => {
  const p = fight.value?.players ?? []
  return p.length === 0 ? 0 : sum(field) / p.length
}

const wvwPlayers = computed(() =>
  [...(fight.value?.players ?? [])].sort((a, b) => b.damage - a.damage)
)

const pvePlayersSortedByDamage = computed(() =>
  [...(fight.value?.players ?? [])].sort((a, b) => b.damage - a.damage)
)

const pvePlayersSortedByRes = computed(() =>
  [...(fight.value?.players ?? [])].sort((a, b) => a.resurrectionTime - b.resurrectionTime)
)

const hasCerusMechanics = computed(() =>
  (fight.value?.players ?? []).some((p: any) => p.cerusOrbsCollected > 0 || p.cerusSpreadHitCount > 0 || p.cerusPhaseOneDamage > 0)
)

const hasDeimoseMechanics = computed(() =>
  (fight.value?.players ?? []).some((p: any) => p.deimosOilsTriggered > 0)
)

const hasUraMechanics = computed(() =>
  (fight.value?.players ?? []).some((p: any) => p.shardPickUp > 0 || p.shardUsed > 0)
)

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
  if (!fight.value || wingmanQueued.value) return
  wingmanQueued.value = true
  api('/api/logs/wingman', {
    method: 'POST',
    body: { logIds: [fight.value.log.fightLogId] },
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

.section-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 1.5rem 0 0.75rem;
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
