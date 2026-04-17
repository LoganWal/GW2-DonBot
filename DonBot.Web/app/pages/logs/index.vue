<template>
  <div>
    <h1 class="page-title">Fight Logs</h1>

    <!-- Quick access buttons -->
    <div class="quick-buttons">
      <div v-for="cat in quickCategories" :key="cat.label" class="quick-row">
        <span class="quick-label">{{ cat.label }}</span>
        <Button size="small" severity="secondary" label="View" @click="viewToday(cat.types)" />
        <Button size="small" severity="secondary" label="Agg 24h" :loading="aggregating === cat.label + '24h'" @click="aggregateRange(cat, 24 * 60 * 60 * 1000, '24h')" />
        <Button size="small" severity="secondary" label="Agg Week" :loading="aggregating === cat.label + 'week'" @click="aggregateRange(cat, 7 * 24 * 60 * 60 * 1000, 'week')" />
        <Button size="small" severity="secondary" label="Agg Month" :loading="aggregating === cat.label + 'month'" @click="aggregateRange(cat, 30 * 24 * 60 * 60 * 1000, 'month')" />
      </div>
    </div>
    <Message v-if="noLogsToday" severity="warn" :closable="true" style="margin-bottom: 1rem;" @close="noLogsToday = false">
      No logs found for today in that category.
    </Message>

    <div style="display: flex; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 1rem;">
      <MultiSelect
        v-model="selectedFightTypes"
        :options="fightTypeGroupedOptions"
        option-group-label="label"
        option-group-children="items"
        option-label="label"
        option-value="value"
        placeholder="All fight types"
        filter
        :filter-fields="['label', 'group']"
        show-clear
        display="chip"
        scroll-height="400px"
        style="min-width: 240px; flex: 1;"
        @change="onFilterChange"
      />
      <MultiSelect
        v-model="selectedCharacters"
        :options="availableCharacters"
        placeholder="All characters"
        :loading="charactersPending"
        show-clear
        display="chip"
        style="min-width: 200px; flex: 1;"
        @change="onFilterChange"
      />
    </div>
    <div style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 1rem; align-items: center;">
      <div style="display: flex; gap: 0.4rem;">
        <Button size="small" label="All" :severity="successFilter === 'all' ? 'primary' : 'secondary'" @click="successFilter = 'all'; onFilterChange()" />
        <Button size="small" label="Kills" :severity="successFilter === 'kills' ? 'success' : 'secondary'" @click="successFilter = 'kills'; onFilterChange()" />
        <Button size="small" label="Wipes" :severity="successFilter === 'wipes' ? 'danger' : 'secondary'" @click="successFilter = 'wipes'; onFilterChange()" />

      </div>
      <div style="display: flex; gap: 0.4rem;">
        <Button size="small" label="All modes" :severity="difficultyFilter === null ? 'primary' : 'secondary'" @click="difficultyFilter = null; onFilterChange()" />
        <Button size="small" label="NM" :severity="difficultyFilter === 0 ? 'primary' : 'secondary'" @click="difficultyFilter = 0; onFilterChange()" />
        <Button size="small" label="CM" :severity="difficultyFilter === 1 ? 'primary' : 'secondary'" @click="difficultyFilter = 1; onFilterChange()" />
        <Button size="small" label="LCM" :severity="difficultyFilter === 2 ? 'primary' : 'secondary'" @click="difficultyFilter = 2; onFilterChange()" />
      </div>
    </div>
    <ProgressSpinner v-if="pending && !logs.length" />
    <DataTable
      v-else
      :value="logs"
      :loading="pending"
      striped-rows
      @row-click="onRowClick"
      style="cursor: pointer; user-select: none;"
    >
      <Column style="width: 3rem; padding: 0;">
        <template #header>
          <div class="checkbox-cell" @click.stop="togglePageSelection">
            <Checkbox :model-value="allOnPageSelected" :indeterminate="someOnPageSelected" binary style="pointer-events: none;" />
          </div>
        </template>
        <template #body="{ data }">
          <div class="checkbox-cell" @click.stop="onCheckboxClick($event, data)">
            <input type="checkbox" :checked="isSelected(data)" style="pointer-events: none; width: 1rem; height: 1rem;" />
          </div>
        </template>
      </Column>
      <Column header="Fight">
        <template #body="{ data }">
          <a :href="`/logs/${data.fightLogId}`" @click.prevent.stop="navigateTo(`/logs/${data.fightLogId}`)" style="color: inherit; text-decoration: none;">{{ fightName(data.fightType) }}</a>
        </template>
      </Column>
      <Column header="Character">
        <template #body="{ data }">{{ data.characterName || '-' }}</template>
      </Column>
      <Column header="Date">
        <template #body="{ data }">{{ new Date(data.fightStart).toLocaleString() }}</template>
      </Column>
      <Column header="Duration">
        <template #body="{ data }">{{ formatDuration(data.fightDurationInMs) }}</template>
      </Column>
      <Column header="Result">
        <template #body="{ data }">
          <Tag
            v-if="data.fightType !== 0"
            :severity="data.isSuccess ? 'success' : 'danger'"
            :value="data.isSuccess ? 'Kill' : `${data.fightPercent}%`"
          />
          <Tag v-else severity="secondary" value="WvW" />
        </template>
      </Column>
    </DataTable>
    <div style="display: flex; align-items: center; gap: 1rem; margin-top: 1rem; justify-content: center;">
      <Button icon="pi pi-chevron-left" severity="secondary" text :disabled="page <= 1" @click="page--" />
      <span style="color: var(--p-text-muted-color); font-size: 0.875rem;">Page {{ page }}</span>
      <Button icon="pi pi-chevron-right" severity="secondary" text :disabled="logs.length < pageSize" @click="page++" />
    </div>
    <div v-if="selectedLogs.length > 0" class="selection-bar">
      <span style="color: var(--p-text-muted-color); font-size: 0.875rem;">{{ selectedLogs.length }} log{{ selectedLogs.length !== 1 ? 's' : '' }} selected</span>
      <Button label="Aggregate" icon="pi pi-chart-bar" @click="goToAggregate" />
      <Button icon="pi pi-times" severity="secondary" text @click="selectedLogs = []" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { fightName, fightTypeGroupedOptions } from '~/composables/useFightTypes'

definePageMeta({ middleware: 'auth' })

const api = useApi()
const route = useRoute()
const router = useRouter()
const pageSize = 25

const page = ref(Number(route.query.page ?? 1))
const selectedLogs = ref<any[]>([])
const lastClickedIndex = ref<number | null>(null)

const selectedFightTypes = ref<number[]>(
  route.query.fightTypes ? String(route.query.fightTypes).split(',').map(Number) : []
)
const selectedCharacters = ref<string[]>(
  route.query.characters ? String(route.query.characters).split(',') : []
)
const successFilter = ref<'all' | 'kills' | 'wipes'>('all')
const difficultyFilter = ref<number | null>(null)

const STRIKE_TYPES = [27, 28, 29, 30, 31, 32, 33, 47, 48, 49, 50, 51]
const RAID_TYPES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 44, 45, 46, 53, 55]
const FRACTAL_TYPES = [34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 52, 54]
const PVE_TYPES = [...RAID_TYPES, ...STRIKE_TYPES, ...FRACTAL_TYPES]

const quickCategories = [
  { label: 'All PvE', types: PVE_TYPES },
  { label: 'WvW', types: [0] },
  { label: 'Strikes', types: STRIKE_TYPES },
  { label: 'Raids (Wings)', types: RAID_TYPES },
  { label: 'Fractals', types: FRACTAL_TYPES },
]

const aggregating = ref<string | null>(null)
const noLogsToday = ref(false)

const viewToday = (types: number[]) => {
  selectedFightTypes.value = [...types]
  page.value = 1
  syncUrl()
  refresh()
}

const aggregateRange = async (cat: { label: string; types: number[] }, windowMs: number, key: string) => {
  aggregating.value = cat.label + key
  noLogsToday.value = false
  try {
    const now = new Date()
    const since = new Date(now.getTime() - windowMs)
    const url = `/api/logs?page=1&pageSize=500&startDateTime=${since.toISOString()}&endDateTime=${now.toISOString()}&fightTypes=${cat.types.join(',')}`
    const res = await api(url) as { data: any[] }
    const ids = (res.data ?? []).map((l: any) => l.fightLogId)
    if (ids.length === 0) {
      noLogsToday.value = true
      return
    }
    navigateTo(`/logs/aggregate?ids=${ids.join(',')}`)
  } finally {
    aggregating.value = null
  }
}

const { data: availableCharacters, pending: charactersPending } = await useAsyncData(
  'log-characters',
  () => api('/api/logs/characters') as Promise<string[]>
)

const syncUrl = () => {
  const q: Record<string, any> = {}
  if (page.value > 1)
  {
    q.page = page.value
  }
  if (selectedFightTypes.value.length)
  {
    q.fightTypes = selectedFightTypes.value.join(',')
  }
  if (selectedCharacters.value.length)
  {
    q.characters = selectedCharacters.value.join(',')
  }
  router.replace({ query: q })
}

const buildUrl = () => {
  let url = `/api/logs?page=${page.value}&pageSize=${pageSize}`
  if (selectedFightTypes.value.length > 0)
    url += `&fightTypes=${selectedFightTypes.value.join(',')}`
  if (selectedCharacters.value.length > 0)
    url += `&characters=${encodeURIComponent(selectedCharacters.value.join(','))}`
  if (successFilter.value === 'kills') url += '&isSuccess=true'
  else if (successFilter.value === 'wipes') url += '&isSuccess=false'
  if (difficultyFilter.value !== null) url += `&fightMode=${difficultyFilter.value}`
  return url
}

const { data, pending, refresh } = await useAsyncData(
  'logs',
  () => api(buildUrl()) as Promise<{ total: number; data: any[] }>,
  { watch: [page] }
)

watch(page, syncUrl)

const onFilterChange = () => {
  page.value = 1
  syncUrl()
  refresh()
}

const logs = computed(() => data.value?.data ?? [])

const selectedIds = computed(() => new Set(selectedLogs.value.map((l: any) => l.fightLogId)))
const isSelected = (row: any) => selectedIds.value.has(row.fightLogId)

const allOnPageSelected = computed(() => logs.value.length > 0 && logs.value.every((l: any) => selectedIds.value.has(l.fightLogId)))
const someOnPageSelected = computed(() => logs.value.some((l: any) => selectedIds.value.has(l.fightLogId)) && !allOnPageSelected.value)

const toggleRow = (row: any) => {
  if (isSelected(row))
  {
    selectedLogs.value = selectedLogs.value.filter((l: any) => l.fightLogId !== row.fightLogId)
  }
  else
  {
    selectedLogs.value = [...selectedLogs.value, row]
  }
}

const togglePageSelection = () => {
  if (allOnPageSelected.value)
  {
    const pageIds = new Set(logs.value.map((l: any) => l.fightLogId))
    selectedLogs.value = selectedLogs.value.filter((l: any) => !pageIds.has(l.fightLogId))
  }
  else
  {
    const existing = selectedIds.value
    const toAdd = logs.value.filter((l: any) => !existing.has(l.fightLogId))
    selectedLogs.value = [...selectedLogs.value, ...toAdd]
  }
}

const handleSelect = (shiftKey: boolean, row: any) => {
  const index = logs.value.findIndex((l: any) => l.fightLogId === row.fightLogId)

  if (shiftKey && lastClickedIndex.value !== null)
  {
    const min = Math.min(lastClickedIndex.value, index)
    const max = Math.max(lastClickedIndex.value, index)
    const range = logs.value.slice(min, max + 1)
    const existing = selectedIds.value
    const toAdd = range.filter((l: any) => !existing.has(l.fightLogId))
    selectedLogs.value = [...selectedLogs.value, ...toAdd]
  }
  else
  {
    toggleRow(row)
    lastClickedIndex.value = index
  }
}

const onRowClick = (e: any) => {
  navigateTo(`/logs/${e.data.fightLogId}`)
}

const onCheckboxClick = (e: MouseEvent, row: any) => {
  handleSelect(e.shiftKey, row)
}

const formatDuration = (ms: number) => {
  const s = Math.floor(ms / 1000)
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}

const goToAggregate = () => {
  const ids = selectedLogs.value.map((l: any) => l.fightLogId).join(',')
  navigateTo(`/logs/aggregate?ids=${ids}`)
}
</script>

<style scoped>
.checkbox-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  min-height: 2.5rem;
  cursor: pointer;
}

.quick-buttons {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  margin-bottom: 1rem;
  padding: 0.75rem 1rem;
  background: var(--p-surface-card);
  border: 1px solid var(--p-surface-border);
  border-radius: 0.75rem;
}

.quick-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.selection-bar {
  position: fixed;
  bottom: 1.5rem;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.4rem 1rem;
  white-space: nowrap;
  z-index: 100;
  border-radius: 0.5rem;
  border: 1px solid var(--p-primary-color);
  background: rgba(15, 15, 20, 0.2);
  backdrop-filter: blur(6px);
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.9);
}

.quick-label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  min-width: 110px;
}
</style>
