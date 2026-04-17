<template>
  <div>
    <h1 class="page-title">Personal Bests</h1>
    <ProgressSpinner v-if="pending" />
    <Message v-else-if="!bests?.wvw && !bests?.pve" severity="info" :closable="false">No fight data found.</Message>
    <Tabs v-else value="pve">
      <TabList>
        <Tab v-if="bests.pve" value="pve">PvE</Tab>
        <Tab v-if="bests.wvw" value="wvw">WvW</Tab>
      </TabList>

      <TabPanel v-if="bests.pve" value="pve">
        <h2 class="section-title">Stats</h2>
        <div class="bests-grid">
          <BestCard label="Damage"              :entry="bests.pve.damage"              :fmt="n => n.toLocaleString()" />
          <BestCard label="Best DPS"            :entry="bests.pve.damagePerSecond"     :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Cleave"              :entry="bests.pve.cleave"              :fmt="n => n.toLocaleString()" />
          <BestCard label="Best Cleave DPS"     :entry="bests.pve.cleavePerSecond"     :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Healing"             :entry="bests.pve.healing"             :fmt="n => n.toLocaleString()" />
          <BestCard label="Best HPS"            :entry="bests.pve.healingPerSecond"    :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Cleanses"            :entry="bests.pve.cleanses"            :fmt="n => n.toLocaleString()" />
          <BestCard label="Barrier Generated"   :entry="bests.pve.barrierGenerated"    :fmt="n => n.toLocaleString()" />
          <BestCard label="Quickness"           :entry="bests.pve.quickness"           :fmt="n => Number(n).toFixed(1) + '%'" />
          <BestCard label="Alacrity"            :entry="bests.pve.alacrity"            :fmt="n => Number(n).toFixed(1) + '%'" />
          <BestCard label="Cerus P1 Damage"     :entry="bests.pve.cerusPhaseOneDamage" :fmt="n => Number(n).toLocaleString()" />
          <BestCard label="Cerus Orbs"          :entry="bests.pve.cerusOrbsCollected"  :fmt="n => n.toLocaleString()" />
          <BestCard label="Deimos Oils"         :entry="bests.pve.deimosOilsTriggered" :fmt="n => n.toLocaleString()" />
          <BestCard label="Ura Shards Picked"   :entry="bests.pve.shardPickUp"         :fmt="n => n.toLocaleString()" />
        </div>

        <template v-if="bests.bestTimes?.length">
          <template v-for="group in bestTimeGroups" :key="group.label">
            <h2 class="section-title">{{ group.label }}</h2>
            <div class="bests-grid">
              <Card class="stat-card total-card">
                <template #content>
                  <div class="stat-label">Total</div>
                  <div v-fit-text class="stat-value">{{ formatDuration(group.items.filter(t => t.fightType !== 42).reduce((sum, t) => sum + t.durationMs, 0)) }}</div>
                </template>
              </Card>
              <Card
                v-for="t in group.items"
                :key="t.fightType"
                :title="t.fightType === 42 ? 'Not included in total' : undefined"
                :class="['stat-card', t.fightType === 42 ? 'best-card excluded-card' : 'best-card']"
                style="cursor: pointer;"
                @click="navigateTo(`/logs/${t.fightLogId}`)"
              >
                <template #content>
                  <div class="stat-label">{{ fightName(t.fightType) }}</div>
                  <div v-fit-text class="stat-value">{{ formatDuration(t.durationMs) }}</div>
                  <div style="font-size: 0.8rem; color: var(--p-text-muted-color); margin-top: 0.2rem;">
                    {{ t.playerDps.toLocaleString() }} DPS
                  </div>
                  <div class="best-meta">
                    <span>{{ new Date(t.fightDate).toLocaleDateString() }}</span>
                    <span style="color: var(--p-primary-color);">View →</span>
                  </div>
                </template>
              </Card>
            </div>
          </template>
        </template>
      </TabPanel>

      <TabPanel v-if="bests.wvw" value="wvw">
        <h2 class="section-title">Stats</h2>
        <div class="bests-grid">
          <BestCard label="Damage"                  :entry="bests.wvw.damage"                   :fmt="n => n.toLocaleString()" />
          <BestCard label="Best DPS"               :entry="bests.wvw.damagePerSecond"          :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Kills"                  :entry="bests.wvw.kills"                    :fmt="n => n.toLocaleString()" />
          <BestCard label="Best Kills/s"           :entry="bests.wvw.killsPerSecond"           :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Downs"                  :entry="bests.wvw.downs"                    :fmt="n => n.toLocaleString()" />
          <BestCard label="Best Downs/s"           :entry="bests.wvw.downsPerSecond"           :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Down Contribution"      :entry="bests.wvw.downContribution"         :fmt="n => n.toLocaleString()" />
          <BestCard label="Best Down Contrib/s"    :entry="bests.wvw.downContributionPerSecond" :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Cleanses"               :entry="bests.wvw.cleanses"                 :fmt="n => n.toLocaleString()" />
          <BestCard label="Best Cleanses/s"        :entry="bests.wvw.cleansesPerSecond"        :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Strips"                 :entry="bests.wvw.strips"                   :fmt="n => n.toLocaleString()" />
          <BestCard label="Best Strips/s"          :entry="bests.wvw.stripsPerSecond"          :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Healing"                :entry="bests.wvw.healing"                  :fmt="n => n.toLocaleString()" />
          <BestCard label="Best HPS"               :entry="bests.wvw.healingPerSecond"         :fmt="n => n.toLocaleString() + '/s'" />
          <BestCard label="Barrier Generated"      :entry="bests.wvw.barrierGenerated"         :fmt="n => n.toLocaleString()" />
          <BestCard label="Stab (On Group)"   :entry="bests.wvw.stabOnGroup"      :fmt="n => Number(n).toFixed(2)" />
          <BestCard label="Stab (Off Group)"  :entry="bests.wvw.stabOffGroup"     :fmt="n => Number(n).toFixed(2)" />
          <BestCard label="Quickness"         :entry="bests.wvw.quickness"        :fmt="n => Number(n).toFixed(1) + '%'" />
          <BestCard label="Alacrity"          :entry="bests.wvw.alacrity"         :fmt="n => Number(n).toFixed(1) + '%'" />
        </div>
      </TabPanel>
    </Tabs>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const { data: bests, pending } = await useAsyncData('bests', () => api('/api/stats/bests') as Promise<any>)

const bestTimeGroups = computed(() =>
  groupByFightType<any>((bests.value as any)?.bestTimes ?? [])
)

const formatDuration = (ms: number) => {
  const s = Math.floor(ms / 1000)
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}
</script>

<style scoped>
.section-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 1.5rem 0 0.75rem;
  display: block;
}

.total-card {
  border-color: rgba(99, 179, 237, 0.5) !important;
  background: rgba(99, 179, 237, 0.06) !important;
}

/* noinspection CssUnusedSymbol */
.excluded-card {
  border-color: rgba(250, 204, 21, 0.5) !important;
  background: rgba(250, 204, 21, 0.06) !important;
}

/* noinspection CssUnusedSymbol */
.excluded-card:hover {
  border-color: rgba(250, 204, 21, 0.8) !important;
}

.bests-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 0.75rem;
}

/* noinspection CssUnusedSymbol */
.best-card:hover {
  border-color: var(--p-primary-color);
}

.best-meta {
  display: flex;
  justify-content: space-between;
  font-size: 0.72rem;
  color: var(--p-text-muted-color);
  margin-top: 0.35rem;
}
</style>
