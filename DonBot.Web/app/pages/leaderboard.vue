<template>
  <div>
    <div style="display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem;">
      <h1 class="page-title" style="margin: 0;">Leaderboard</h1>
      <span v-if="data" style="color: var(--p-text-muted-color); font-size: 0.875rem;">
        {{ data.sinceDate }} - {{ data.untilDate }} (last 7 days)
      </span>
      <div style="margin-left: auto;">
        <Select
          v-model="selectedGuildId"
          :options="guildOptions"
          option-label="label"
          option-value="value"
          :loading="guildsPending"
          style="min-width: 180px;"
        />
      </div>
    </div>

    <ProgressSpinner v-if="pending" />

    <template v-else-if="data">
      <!-- PvE Boards -->
      <template v-if="data.pve.length">
        <button class="section-toggle" @click="expanded.pveDamage = !expanded.pveDamage">
          <i :class="expanded.pveDamage ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="toggle-icon" />
          PvE - Damage & Combat
          <span class="section-count">{{ data.pve.length }} players</span>
        </button>
        <template v-if="expanded.pveDamage">
          <DataTable :value="data.pve" striped-rows scrollable class="mb-section" sort-field="dps" :sort-order="-1">
            <Column header="#" style="width: 3rem;" frozen>
              <template #body="{ index }">{{ index + 1 }}</template>
            </Column>
            <Column field="accountName" header="Account" frozen :sortable="true" style="min-width: 160px;" />
            <Column field="fights" header="Fights" :sortable="true" style="min-width: 70px;" />
            <Column header="DPS" :sortable="true" sort-field="dps" style="min-width: 90px;">
              <template #body="{ data: row }">{{ row.dps.toLocaleString() }}</template>
            </Column>
            <Column header="Cleave DPS" :sortable="true" sort-field="cleaveDps" style="min-width: 105px;">
              <template #body="{ data: row }">{{ row.cleaveDps.toLocaleString() }}</template>
            </Column>
            <Column header="Quick %" :sortable="true" sort-field="avgQuick" style="min-width: 85px;">
              <template #body="{ data: row }">{{ row.avgQuick }}%</template>
            </Column>
            <Column header="Alac %" :sortable="true" sort-field="avgAlac" style="min-width: 80px;">
              <template #body="{ data: row }">{{ row.avgAlac }}%</template>
            </Column>
          </DataTable>
        </template>

        <button class="section-toggle" @click="expanded.pveSupport = !expanded.pveSupport">
          <i :class="expanded.pveSupport ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="toggle-icon" />
          PvE - Support
          <span class="section-count">{{ data.pve.length }} players</span>
        </button>
        <template v-if="expanded.pveSupport">
          <DataTable :value="data.pve" striped-rows scrollable class="mb-section" sort-field="avgResTimeSec" :sort-order="-1">
            <Column header="#" style="width: 3rem;" frozen>
              <template #body="{ index }">{{ index + 1 }}</template>
            </Column>
            <Column field="accountName" header="Account" frozen :sortable="true" style="min-width: 160px;" />
            <Column field="fights" header="Fights" :sortable="true" style="min-width: 70px;" />
            <Column header="Avg Res Time (s)" :sortable="true" sort-field="avgResTimeSec" style="min-width: 140px;">
              <template #body="{ data: row }">{{ row.avgResTimeSec.toFixed(2) }}</template>
            </Column>
            <Column header="HPS" :sortable="true" sort-field="hps" style="min-width: 90px;">
              <template #body="{ data: row }">{{ row.hps.toLocaleString() }}</template>
            </Column>
          </DataTable>
        </template>

        <button class="section-toggle" @click="expanded.pveSurvival = !expanded.pveSurvival">
          <i :class="expanded.pveSurvival ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="toggle-icon" />
          PvE - Survivability
          <span class="section-count">{{ data.pve.length }} players</span>
        </button>
        <template v-if="expanded.pveSurvival">
          <DataTable :value="data.pve" striped-rows scrollable class="mb-section" sort-field="avgDeaths" :sort-order="-1">
            <Column header="#" style="width: 3rem;" frozen>
              <template #body="{ index }">{{ index + 1 }}</template>
            </Column>
            <Column field="accountName" header="Account" frozen :sortable="true" style="min-width: 160px;" />
            <Column field="fights" header="Fights" :sortable="true" style="min-width: 70px;" />
            <Column field="avgDeaths" header="Avg Deaths" :sortable="true" style="min-width: 105px;" />
            <Column field="avgTimesDowned" header="Avg Downed" :sortable="true" style="min-width: 110px;" />
          </DataTable>
        </template>
      </template>

      <!-- WvW Boards -->
      <template v-if="data.wvw.length">
        <button class="section-toggle" @click="expanded.wvwDamage = !expanded.wvwDamage">
          <i :class="expanded.wvwDamage ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="toggle-icon" />
          WvW - Damage & Combat
          <span class="section-count">{{ data.wvw.length }} players</span>
        </button>
        <template v-if="expanded.wvwDamage">
          <DataTable :value="data.wvw" striped-rows scrollable class="mb-section" sort-field="avgDamage" :sort-order="-1">
            <Column header="#" style="width: 3rem;" frozen>
              <template #body="{ index }">{{ index + 1 }}</template>
            </Column>
            <Column field="accountName" header="Account" frozen :sortable="true" style="min-width: 160px;" />
            <Column field="fights" header="Fights" :sortable="true" style="min-width: 70px;" />
            <Column header="Avg Damage" :sortable="true" sort-field="avgDamage" style="min-width: 110px;">
              <template #body="{ data: row }">{{ row.avgDamage.toLocaleString() }}</template>
            </Column>
            <Column header="Avg DDC" :sortable="true" sort-field="avgDdc" style="min-width: 100px;">
              <template #body="{ data: row }">{{ row.avgDdc.toLocaleString() }}</template>
            </Column>
            <Column field="avgKills" header="Avg Kills" :sortable="true" style="min-width: 85px;" />
            <Column field="avgDowns" header="Avg Downs" :sortable="true" style="min-width: 90px;" />
            <Column field="avgBoonsRipped" header="Avg Boons Ripped" :sortable="true" style="min-width: 130px;" />
          </DataTable>
        </template>

        <button class="section-toggle" @click="expanded.wvwSupport = !expanded.wvwSupport">
          <i :class="expanded.wvwSupport ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="toggle-icon" />
          WvW - Support
          <span class="section-count">{{ data.wvw.length }} players</span>
        </button>
        <template v-if="expanded.wvwSupport">
          <DataTable :value="data.wvw" striped-rows scrollable class="mb-section" sort-field="avgHealing" :sort-order="-1">
            <Column header="#" style="width: 3rem;" frozen>
              <template #body="{ index }">{{ index + 1 }}</template>
            </Column>
            <Column field="accountName" header="Account" frozen :sortable="true" style="min-width: 160px;" />
            <Column field="fights" header="Fights" :sortable="true" style="min-width: 70px;" />
            <Column header="Avg Healing" :sortable="true" sort-field="avgHealing" style="min-width: 110px;">
              <template #body="{ data: row }">{{ row.avgHealing.toLocaleString() }}</template>
            </Column>
            <Column field="avgCleanses" header="Avg Cleanses" :sortable="true" style="min-width: 115px;" />
            <Column field="avgStrips" header="Avg Strips" :sortable="true" style="min-width: 100px;" />
            <Column header="Avg Barrier" :sortable="true" sort-field="avgBarrier" style="min-width: 105px;">
              <template #body="{ data: row }">{{ row.avgBarrier.toLocaleString() }}</template>
            </Column>
          </DataTable>
        </template>

        <button class="section-toggle" @click="expanded.wvwSurvival = !expanded.wvwSurvival">
          <i :class="expanded.wvwSurvival ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="toggle-icon" />
          WvW - Survivability
          <span class="section-count">{{ data.wvw.length }} players</span>
        </button>
        <template v-if="expanded.wvwSurvival">
          <DataTable :value="data.wvw" striped-rows scrollable class="mb-section" sort-field="avgDeaths" :sort-order="-1">
            <Column header="#" style="width: 3rem;" frozen>
              <template #body="{ index }">{{ index + 1 }}</template>
            </Column>
            <Column field="accountName" header="Account" frozen :sortable="true" style="min-width: 160px;" />
            <Column field="fights" header="Fights" :sortable="true" style="min-width: 70px;" />
            <Column field="avgDeaths" header="Avg Deaths" :sortable="true" style="min-width: 100px;" />
            <Column field="avgTimesDowned" header="Avg Downed" :sortable="true" style="min-width: 105px;" />
          </DataTable>
        </template>
      </template>

      <Message v-if="!data.wvw.length && !data.pve.length" severity="secondary" :closable="false">
        No leaderboard data for the selected period.
      </Message>
    </template>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()

const { data: guilds, pending: guildsPending } = await useAsyncData(
  'my-guilds',
  () => api('/api/guilds/mine') as Promise<{ guildId: string; guildName: string }[]>
)

const guildOptions = computed(() => [
  { label: 'Global', value: '-1' },
  ...((guilds.value ?? []).map(g => ({ label: g.guildName, value: g.guildId }))),
])

const selectedGuildId = ref('-1')

const { data, pending, refresh } = await useAsyncData(
  'leaderboard',
  () => api(`/api/guilds/${selectedGuildId.value}/leaderboard`) as Promise<{
    sinceDate: string
    untilDate: string
    wvw: any[]
    pve: any[]
  }>
)

watch(selectedGuildId, () => refresh())

const expanded = reactive({
  wvwDamage: false,
  wvwSupport: false,
  wvwSurvival: false,
  pveDamage: false,
  pveSupport: false,
  pveSurvival: false,
})
</script>

<style scoped>
.mb-section {
  margin-bottom: 0.5rem;
}

.section-toggle {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  width: 100%;
  background: var(--p-surface-card);
  border: 1px solid var(--p-surface-border);
  border-radius: 8px;
  padding: 0.6rem 1rem;
  margin: 0.75rem 0 0.4rem;
  cursor: pointer;
  color: var(--p-text-color);
  font-size: 0.85rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  text-align: left;
  transition: background 0.15s, border-color 0.15s;
}

.section-toggle:hover {
  background: var(--p-surface-hover);
  border-color: var(--p-primary-color);
}

.toggle-icon {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
}

.section-count {
  margin-left: auto;
  font-size: 0.75rem;
  font-weight: 400;
  color: var(--p-text-muted-color);
  text-transform: none;
  letter-spacing: 0;
}
</style>
