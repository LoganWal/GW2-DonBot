<template>
  <div>
    <h1 class="page-title">Mechanics Overview</h1>

    <ProgressSpinner v-if="pending" />
    <Message v-else-if="!data?.length" severity="info" :closable="false">
      No mechanics data found. Play some PvE content to see your stats here.
    </Message>

    <template v-else>
      <div v-for="cat in byCategory" :key="cat.label">
        <CollapsibleSection :title="cat.label">
          <div v-for="group in cat.groups" :key="group.label" class="fight-group-wrap">
            <CollapsibleSection :title="group.label" :collapsed="true">
              <div v-for="item in group.items" :key="item.fightType" class="boss-wrap">
                <button class="boss-toggle" @click="toggleBoss(item.fightType)">
                  <span>{{ fightName(item.fightType) }}</span>
                  <i :class="openBosses.has(item.fightType) ? 'pi pi-chevron-up' : 'pi pi-chevron-down'" class="boss-toggle-icon" />
                </button>
                <div v-show="openBosses.has(item.fightType)" class="mechanic-card-section">
                  <div class="mechanic-summary-grid">
                    <MechanicSummaryCard
                      v-for="m in item.mechanics"
                      :key="m.mechanicName"
                      :name="m.mechanicName"
                      :max-value="m.max"
                      :max-link="m.maxFightLogId ? `/logs/${m.maxFightLogId}` : null"
                      :avg="m.avg.toFixed(1)"
                      :median="m.median"
                    />
                  </div>
                </div>
              </div>
            </CollapsibleSection>
          </div>
        </CollapsibleSection>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { fightName, groupBySuperCategory } from '~/composables/useFightTypes'
import CollapsibleSection from '~/components/CollapsibleSection.vue'

definePageMeta({ middleware: 'auth' })

const api = useApi()

const { data, pending } = await useAsyncData(
  'mechanics-overview',
  () => api('/api/stats/mechanics') as Promise<{ fightType: number; mechanics: { mechanicName: string; max: number; maxFightLogId: number | null; avg: number; median: number }[] }[]>
)

const byCategory = computed(() =>
  groupBySuperCategory(data.value ?? [])
)

const openBosses = ref(new Set<number>())
const toggleBoss = (fightType: number) => {
  if (openBosses.value.has(fightType)) openBosses.value.delete(fightType)
  else openBosses.value.add(fightType)
  openBosses.value = new Set(openBosses.value)
}
</script>

<style scoped>
.fight-group-wrap :deep(.collapsible-section) {
  margin-top: 0.4rem;
}
.fight-group-wrap :deep(.collapsible-title) {
  font-size: 0.875rem;
}
.boss-wrap {
  margin: 0.2rem 0 0 1rem;
}
.boss-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 0.4rem 0;
  margin-top: 0.2rem;
  background: none;
  border: none;
  border-bottom: 1px solid var(--p-surface-border);
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--p-text-color);
  gap: 0.5rem;
}
.boss-toggle:hover {
  color: var(--p-primary-color);
}
.boss-toggle-icon {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
  flex-shrink: 0;
}
.mechanic-card-section {
  padding: 0.5rem 0 0.25rem;
}
.mechanic-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 0.5rem;
}
</style>
