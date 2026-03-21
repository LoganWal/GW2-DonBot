<template>
  <div>
    <h1 class="page-title">Dashboard</h1>
    <ProgressSpinner v-if="pending" />
    <template v-else-if="dashboard">

      <!-- Account row -->
      <div class="stat-grid" style="margin-bottom: 1.5rem;">
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Available Points</div>
            <div class="stat-value">{{ dashboard.account?.availablePoints ?? 0 }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Total Points Earned</div>
            <div class="stat-value">{{ dashboard.account?.points ?? 0 }}</div>
          </template>
        </Card>
        <Card class="stat-card">
          <template #content>
            <div class="stat-label">Last Active</div>
            <div class="stat-value" style="font-size: 1rem;">
              {{ dashboard.lastFightDate ? new Date(dashboard.lastFightDate).toLocaleDateString() : '—' }}
            </div>
          </template>
        </Card>
        <Card v-if="dashboard.characterCount" class="stat-card">
          <template #content>
            <div class="stat-label">Characters</div>
            <div class="stat-value">{{ dashboard.characterCount }}</div>
          </template>
        </Card>
        <Card v-if="dashboard.gw2Accounts?.length" class="stat-card" style="grid-column: span 2;">
          <template #content>
            <div class="stat-label" style="margin-bottom: 0.5rem;">GW2 Accounts</div>
            <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
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

      <template v-if="dashboard.fights">
        <h2 class="section-title">Fight Summary</h2>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Fights</div><div class="stat-value">{{ dashboard.fights.total.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">WvW Fights</div><div class="stat-value">{{ dashboard.fights.wvw.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">PvE Fights</div><div class="stat-value">{{ dashboard.fights.pve.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Deaths</div><div class="stat-value">{{ dashboard.fights.totalDeaths.toLocaleString() }}</div></template>
          </Card>
        </div>

        <h2 class="section-title">Career Totals</h2>
        <div class="stat-grid" style="margin-bottom: 1.5rem;">
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Damage</div><div class="stat-value">{{ dashboard.fights.totalDamage.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Kills (WvW)</div><div class="stat-value">{{ dashboard.fights.totalKills.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Down Contribution (WvW)</div><div class="stat-value">{{ dashboard.fights.totalDownContribution.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Healing</div><div class="stat-value">{{ dashboard.fights.totalHealing.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Cleanses</div><div class="stat-value">{{ dashboard.fights.totalCleanses.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Total Strips</div><div class="stat-value">{{ dashboard.fights.totalStrips.toLocaleString() }}</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Avg Alacrity</div><div class="stat-value">{{ dashboard.fights.avgAlac?.toFixed(1) }}%</div></template>
          </Card>
          <Card class="stat-card">
            <template #content><div class="stat-label">Avg Quickness</div><div class="stat-value">{{ dashboard.fights.avgQuickness?.toFixed(1) }}%</div></template>
          </Card>
        </div>
      </template>

    </template>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const { data: dashboard, pending } = await useAsyncData('dashboard', () => api('/api/dashboard'))

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
</script>

<style scoped>
.section-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 0 0 0.75rem;
}
</style>
