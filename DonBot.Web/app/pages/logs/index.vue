<template>
  <div>
    <div style="display: flex; align-items: center; gap: 0.5rem; margin-bottom: 1rem; flex-wrap: wrap;">
      <h1 class="page-title" style="margin: 0;">Fight Logs</h1>
      <Button icon="pi pi-filter" :label="activeFilterCount ? `Filters (${activeFilterCount})` : 'Filters'" severity="secondary" size="small" style="margin-left: auto;" @click="showFilters = true" />
      <Button icon="pi pi-sliders-h" label="Quick Aggregate" severity="secondary" size="small" @click="showQuickFilters = true" />
      <Button icon="pi pi-upload" label="Upload" severity="secondary" size="small" @click="navigateTo('/logs/upload')" />
    </div>

    <Message v-if="noLogsToday" severity="warn" :closable="true" style="margin-bottom: 1rem;" @close="noLogsToday = false">
      No logs found for today in that category.
    </Message>

    <div class="quick-filter-bar">
      <div class="quick-filter-group">
        <span>Result</span>
        <FilterButtonGroup
          :options="successFilterOptions"
          :model-value="successFilter"
          @update:model-value="successFilter = $event; onFilterChange()"
        />
      </div>
      <div class="quick-filter-group">
        <span>Difficulty</span>
        <FilterButtonGroup
          :options="difficultyFilterOptions"
          :model-value="difficultyFilter"
          @update:model-value="difficultyFilter = $event; onFilterChange()"
        />
      </div>
    </div>

    <Dialog v-model:visible="showFilters" modal header="Filter fight logs" :style="{ width: '46rem', maxWidth: '95vw' }" :dismissable-mask="true" @show="captureFilterSnapshot" @hide="restoreFilterSnapshot">
      <div class="filter-grid">
        <label class="filter-field filter-wide"><span>Fight types</span><MultiSelect v-model="selectedFightTypes" :options="fightTypeGroupedOptions" option-group-label="label" option-group-children="items" option-label="label" option-value="value" placeholder="All fight types" filter auto-filter-focus :filter-fields="['label', 'group']" show-clear display="chip" scroll-height="400px" /></label>
        <label class="filter-field filter-wide"><span>Guilds / Discord servers</span><MultiSelect v-model="selectedGuildIds" :options="availableGuilds" option-label="guildName" option-value="guildId" placeholder="All guilds" :loading="guildsPending" filter auto-filter-focus show-clear display="chip" /></label>
        <label class="filter-field"><span>Characters</span><MultiSelect v-model="selectedCharacters" :options="availableCharacters" placeholder="All characters" :loading="charactersPending" filter auto-filter-focus show-clear display="chip" /></label>
        <label class="filter-field"><span>Roles</span><MultiSelect v-model="selectedPlaystyles" :options="playstyleGroupedOptions" option-group-label="label" option-group-children="items" option-label="label" option-value="value" placeholder="All roles" show-clear display="chip" /></label>
        <label class="filter-field"><span>From</span><DatePicker v-model="startDate" show-time hour-format="24" show-icon /></label>
        <label class="filter-field"><span>To</span><DatePicker v-model="endDate" show-time hour-format="24" show-icon /></label>
        <label class="filter-field"><span>Minimum duration (seconds)</span><InputNumber v-model="minDurationSeconds" :min="0" :max="MAX_API_INT" :min-fraction-digits="0" :max-fraction-digits="0" show-buttons /></label>
        <label class="filter-field"><span>Maximum duration (seconds)</span><InputNumber v-model="maxDurationSeconds" :min="0" :max="MAX_API_INT" :min-fraction-digits="0" :max-fraction-digits="0" show-buttons /></label>
        <label class="filter-field"><span>Minimum boss health %</span><InputNumber v-model="minFightPercent" :min="0" :max="100" :min-fraction-digits="0" :max-fraction-digits="2" /></label>
        <label class="filter-field"><span>Maximum boss health %</span><InputNumber v-model="maxFightPercent" :min="0" :max="100" :min-fraction-digits="0" :max-fraction-digits="2" /></label>
        <label class="filter-field"><span>View</span><Select v-model="sortMode" :options="sortModeOptions" option-label="label" option-value="value" /></label>
      </div>
      <template #footer>
        <Button label="Clear all" severity="secondary" text @click="clearFilters" />
        <Button label="Apply filters" icon="pi pi-check" @click="applyFilters" />
      </template>
    </Dialog>

    <Dialog v-model:visible="showQuickFilters" modal header="Quick Aggregation Filters" :style="{ width: '36rem', maxWidth: '95vw' }" :dismissable-mask="true">
      <div class="quick-buttons-modal">
        <div v-for="cat in quickCategories" :key="cat.label" class="quick-row">
          <span class="quick-label">{{ cat.label }}</span>
          <div class="quick-actions">
            <Button size="small" severity="secondary" label="View" @click="viewTodayFromModal(cat.types)" />
            <Button size="small" severity="secondary" label="Agg 24h" :loading="aggregating === cat.label + '24h'" @click="aggregateRange(cat, 24 * 60 * 60 * 1000, '24h')" />
            <Button size="small" severity="secondary" label="Agg Week" :loading="aggregating === cat.label + 'week'" @click="aggregateRange(cat, 7 * 24 * 60 * 60 * 1000, 'week')" />
            <Button size="small" severity="secondary" label="Agg Month" :loading="aggregating === cat.label + 'month'" @click="aggregateRange(cat, 30 * 24 * 60 * 60 * 1000, 'month')" />
          </div>
        </div>
      </div>
    </Dialog>

    <template v-if="categoryMode">
      <ProgressSpinner v-if="categoryLoading" />
      <Message v-else-if="categoryError" severity="warn" :closable="false">
        Category logs could not be loaded.
        <Button label="Retry" severity="secondary" size="small" @click="loadCategoryLogs" />
      </Message>
      <template v-else>
        <div v-for="cat in groupedCategoryLogs" :key="cat.label">
          <CollapsibleSection :title="cat.label">
            <div v-for="group in cat.groups" :key="group.label" class="category-fight-group-wrap">
              <CollapsibleSection :title="group.label" :collapsed="true">
                <DataTable
                  :value="group.items"
                  striped-rows
                  size="small"
                  class="category-table"
                  lazy
                  :sort-field="sortField"
                  :sort-order="sortOrder === 'asc' ? 1 : -1"
                  @sort="onCategorySort"
                >
                  <Column style="width: 3rem; padding: 0;">
                    <template #body="{ data }">
                      <div class="checkbox-cell" role="checkbox" tabindex="0" :aria-checked="isSelected(data)" :aria-label="selectionLabel(data)" @mousedown="onCheckboxMouseDown" @click.stop="onCheckboxClick($event, data, group.items)" @keydown.space.prevent="toggleRow(data)" @keydown.enter.prevent="toggleRow(data)">
                        <input type="checkbox" :checked="isSelected(data)" tabindex="-1" aria-hidden="true" style="pointer-events: none; width: 1rem; height: 1rem;" />
                      </div>
                    </template>
                  </Column>
                  <Column header="Fight" sort-field="fightType" :sortable="true">
                    <template #body="{ data }">
                      <a :href="`/logs/${data.fightLogId}`" :title="`View ${fightName(data.fightType)} from ${formatDateTime(data.fightStart)}`" @click.prevent.stop="navigateTo(`/logs/${data.fightLogId}`)" style="color: inherit; text-decoration: none;">{{ fightName(data.fightType) }}</a>
                    </template>
                  </Column>
                  <Column header="Character" sort-field="characterName" :sortable="true">
                    <template #body="{ data }">{{ data.characterName || '-' }}</template>
                  </Column>
                  <Column header="Role" sort-field="playstyleLabel" :sortable="true">
                    <template #body="{ data }">
                      <Tag v-if="data.playstyleLabel" :severity="playstyleSeverity(data.playstyle)" :value="data.playstyleLabel" />
                      <span v-else>-</span>
                    </template>
                  </Column>
                  <Column header="Date" sort-field="fightStart" :sortable="true">
                    <template #body="{ data }">{{ formatDateTime(data.fightStart) }}</template>
                  </Column>
                  <Column header="Duration" sort-field="fightDurationInMs" :sortable="true">
                    <template #body="{ data }">{{ formatDuration(data.fightDurationInMs) }}</template>
                  </Column>
                  <Column header="Result" sort-field="isSuccess" :sortable="true">
                    <template #body="{ data }">
                      <Tag v-if="data.fightType !== 0" :severity="data.isSuccess ? 'success' : 'danger'" :value="data.isSuccess ? 'Kill' : `${data.fightPercent}%`" />
                      <Tag v-else severity="secondary" value="WvW" />
                    </template>
                  </Column>
                  <Column header="Guild" sort-field="guildName" :sortable="true">
                    <template #body="{ data }">{{ data.guildName || '-' }}</template>
                  </Column>
                  <Column header="Links" style="width: 6rem;">
                    <template #body="{ data }">
                      <div class="log-links">
                        <Button icon="pi pi-eye" severity="secondary" text size="small" aria-label="View log details" v-tooltip.top="'View log details'" @click.stop="navigateTo(`/logs/${data.fightLogId}`)" />
                        <a v-if="data.url" :href="data.url" target="_blank" rel="noopener" aria-label="Open on dps.report" v-tooltip.top="'Open on dps.report'" class="external-log-link" @click.stop>
                          <i class="pi pi-external-link" />
                        </a>
                      </div>
                    </template>
                  </Column>
                </DataTable>
              </CollapsibleSection>
            </div>
          </CollapsibleSection>
        </div>
        <div v-if="categoryTotal > categoryPageSize" class="table-pager">
          <Button icon="pi pi-chevron-left" severity="secondary" text aria-label="Previous category page" :disabled="page <= 1" @click="page--" />
          <span>Page {{ page }} of {{ categoryPageCount }}</span>
          <Button icon="pi pi-chevron-right" severity="secondary" text aria-label="Next category page" :disabled="page >= categoryPageCount" @click="page++" />
        </div>
      </template>
    </template>

    <template v-else>
      <ProgressSpinner v-if="pending && !logs.length" />
      <DataTable
        v-else
        :value="logs"
        :loading="pending"
        striped-rows
        lazy
        :sort-field="sortField"
        :sort-order="sortOrder === 'asc' ? 1 : -1"
        @sort="onSort"
      >
        <Column style="width: 3rem; padding: 0;">
          <template #header>
            <div class="checkbox-cell" role="checkbox" tabindex="0" :aria-checked="someOnPageSelected ? 'mixed' : allOnPageSelected" aria-label="Select all logs on this page" @click.stop="togglePageSelection" @keydown.space.prevent="togglePageSelection" @keydown.enter.prevent="togglePageSelection">
              <Checkbox :model-value="allOnPageSelected" :indeterminate="someOnPageSelected" binary style="pointer-events: none;" />
            </div>
          </template>
          <template #body="{ data }">
            <div class="checkbox-cell" role="checkbox" tabindex="0" :aria-checked="isSelected(data)" :aria-label="selectionLabel(data)" @mousedown="onCheckboxMouseDown" @click.stop="onCheckboxClick($event, data)" @keydown.space.prevent="toggleRow(data)" @keydown.enter.prevent="toggleRow(data)">
              <input type="checkbox" :checked="isSelected(data)" tabindex="-1" aria-hidden="true" style="pointer-events: none; width: 1rem; height: 1rem;" />
            </div>
          </template>
        </Column>
        <Column header="Fight" field="fightType" sortable>
          <template #body="{ data }">
            <a :href="`/logs/${data.fightLogId}`" :title="`View ${fightName(data.fightType)} from ${formatDateTime(data.fightStart)}`" @click.prevent.stop="navigateTo(`/logs/${data.fightLogId}`)" style="color: inherit; text-decoration: none;">{{ fightName(data.fightType) }}</a>
          </template>
        </Column>
        <Column header="Character" field="characterName" sortable>
          <template #body="{ data }">{{ data.characterName || '-' }}</template>
        </Column>
        <Column header="Role" field="playstyleLabel" sortable>
          <template #body="{ data }">
            <Tag v-if="data.playstyleLabel" :severity="playstyleSeverity(data.playstyle)" :value="data.playstyleLabel" />
            <span v-else>-</span>
          </template>
        </Column>
        <Column header="Date" field="fightStart" sortable>
          <template #body="{ data }">{{ formatDateTime(data.fightStart) }}</template>
        </Column>
        <Column header="Duration" field="fightDurationInMs" sortable>
          <template #body="{ data }">{{ formatDuration(data.fightDurationInMs) }}</template>
        </Column>
        <Column header="Result" field="isSuccess" sortable>
          <template #body="{ data }">
            <Tag
              v-if="data.fightType !== 0"
              :severity="data.isSuccess ? 'success' : 'danger'"
              :value="data.isSuccess ? 'Kill' : `${data.fightPercent}%`"
            />
            <Tag v-else severity="secondary" value="WvW" />
          </template>
        </Column>
        <Column header="Guild" field="guildName" sortable>
          <template #body="{ data }">{{ data.guildName || '-' }}</template>
        </Column>
        <Column header="Links" style="width: 6rem;">
          <template #body="{ data }">
            <div class="log-links">
              <Button icon="pi pi-eye" severity="secondary" text size="small" aria-label="View log details" v-tooltip.top="'View log details'" @click.stop="navigateTo(`/logs/${data.fightLogId}`)" />
              <a v-if="data.url" :href="data.url" target="_blank" rel="noopener" aria-label="Open on dps.report" v-tooltip.top="'Open on dps.report'" class="external-log-link" @click.stop>
                <i class="pi pi-external-link" />
              </a>
            </div>
          </template>
        </Column>
      </DataTable>
      <div style="display: flex; align-items: center; gap: 1rem; margin-top: 1rem; justify-content: center;">
        <Button icon="pi pi-chevron-left" severity="secondary" text aria-label="Previous page" :disabled="page <= 1" @click="page--" />
        <span style="color: var(--p-text-muted-color); font-size: 0.875rem;">Page {{ page }}</span>
        <Button icon="pi pi-chevron-right" severity="secondary" text aria-label="Next page" :disabled="page * pageSize >= logsTotal" @click="page++" />
      </div>
    </template>
    <div v-if="selectedLogs.length > 0" class="selection-bar">
      <span style="color: var(--p-text-muted-color); font-size: 0.875rem;">{{ selectedLogs.length }} log{{ selectedLogs.length !== 1 ? 's' : '' }} selected</span>
      <Button label="Aggregate" icon="pi pi-chart-bar" @click="goToAggregate" />
      <Button icon="pi pi-times" severity="secondary" text aria-label="Clear selected logs" @click="clearSelection" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { fightName, fightTypeGroupedOptions, fightTypeQuickCategories, groupBySuperCategory, formatDuration } from '~/composables/useFightTypes'
import { successFilterOptions, difficultyFilterOptions, type SuccessFilter, type DifficultyFilter } from '~/composables/useLogFilters'
import { buildFightLogListUrl, buildLogsRouteQuery, parseNumberListQuery, parseStringListQuery } from '~/composables/useLogQuery'
import { playstyleGroupedOptions, playstyleSeverity } from '~/composables/usePlaystyles'
import { formatDateTime } from '~/composables/useFormatters'
import CollapsibleSection from '~/components/CollapsibleSection.vue'

definePageMeta({ middleware: 'auth' })

const api = useApi()
const route = useRoute()
const router = useRouter()
const pageSize = 25
const categoryPageSize = 100
const MAX_API_INT = 2_147_483_647
const sortableFields = new Set(['fightType', 'guildName', 'characterName', 'playstyleLabel', 'fightStart', 'fightDurationInMs', 'isSuccess', 'fightPercent'])
const validFightTypes = new Set(fightTypeGroupedOptions.flatMap(group => group.items.map(item => item.value)))
const validPlaystyles = new Set(playstyleGroupedOptions.flatMap(group => group.items.map(item => item.value)))
const queryNumber = (value: unknown) => {
  if (value == null || value === '') return null
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}
const queryDate = (value: unknown) => {
  if (!value) return null
  const parsed = new Date(String(value))
  return Number.isNaN(parsed.getTime()) ? null : parsed
}
const queryBound = (value: unknown, maximum?: number) => {
  const parsed = queryNumber(value)
  if (parsed === null || parsed < 0 || (maximum !== undefined && parsed > maximum)) return null
  return parsed
}
const queryDuration = (value: unknown) => {
  const parsed = queryNumber(value)
  if (parsed === null || parsed < 0 || parsed > MAX_API_INT) return null
  return Math.trunc(parsed)
}
const queryPage = (value: unknown) => Math.min(Math.max(Math.trunc(queryNumber(value) ?? 1), 1), MAX_API_INT)
const queryDifficulty = (value: unknown): DifficultyFilter => {
  const parsed = queryNumber(value)
  return parsed !== null && [0, 1, 2].includes(parsed) ? parsed : null
}
const querySortField = (value: unknown) => sortableFields.has(String(value)) ? String(value) : 'fightStart'
const parseGuildIds = (value: unknown) => parseStringListQuery(value).filter(id => /^-?\d+$/.test(id))

const page = ref(queryPage(route.query.page))
const selectedLogs = ref<any[]>([])
const lastClickedIndex = ref<number | null>(null)
let lastClickedSource: any[] | null = null

const sanitizeFightTypes = (values: number[]) => values.filter(value => validFightTypes.has(value))
const sanitizePlaystyles = (values: string[]) => values.filter(value => validPlaystyles.has(value))

const initialFightTypes = parseNumberListQuery(route.query.fightTypes)
const initialPlaystyles = parseStringListQuery(route.query.playstyles)
const selectedFightTypes = ref<number[]>(sanitizeFightTypes(initialFightTypes))
const selectedGuildIds = ref<string[]>(parseGuildIds(route.query.guildIds))
const selectedCharacters = ref<string[]>(parseStringListQuery(route.query.characters))
const selectedPlaystyles = ref<string[]>(sanitizePlaystyles(initialPlaystyles))
const successFilter = ref<SuccessFilter>(['kills', 'wipes'].includes(String(route.query.result)) ? String(route.query.result) as SuccessFilter : 'all')
const difficultyFilter = ref<DifficultyFilter>(queryDifficulty(route.query.mode))
const startDate = ref<Date | null>(queryDate(route.query.from))
const endDate = ref<Date | null>(queryDate(route.query.to))
const minDurationSeconds = ref<number | null>(queryDuration(route.query.minDuration))
const maxDurationSeconds = ref<number | null>(queryDuration(route.query.maxDuration))
const minFightPercent = ref<number | null>(queryBound(route.query.minPercent, 100))
const maxFightPercent = ref<number | null>(queryBound(route.query.maxPercent, 100))
const sortField = ref(querySortField(route.query.sortField))
const sortOrder = ref<'asc' | 'desc'>(route.query.sortOrder === 'asc' ? 'asc' : 'desc')

const quickCategories = fightTypeQuickCategories

const aggregating = ref<string | null>(null)
const noLogsToday = ref(false)
const showQuickFilters = ref(false)
const showFilters = ref(false)
type FilterSnapshot = {
  fightTypes: number[]; guildIds: string[]; characters: string[]; playstyles: string[]
  success: SuccessFilter; difficulty: DifficultyFilter; start: Date | null; end: Date | null
  minDuration: number | null; maxDuration: number | null; minPercent: number | null; maxPercent: number | null
  view: 'time' | 'category'
}
let filterSnapshot: FilterSnapshot | null = null
let routeSelectionSanitized = selectedFightTypes.value.length !== initialFightTypes.length || selectedPlaystyles.value.length !== initialPlaystyles.length
const sortMode = ref<'time' | 'category'>(route.query.sort === 'category' ? 'category' : 'time')
const categoryMode = computed(() => sortMode.value === 'category')
const categoryLogs = ref<any[]>([])
const categoryTotal = ref(0)
const categoryPageCount = computed(() => Math.max(Math.ceil(categoryTotal.value / categoryPageSize), 1))
const categoryLoading = ref(false)
const categoryError = ref(false)
let categoryRequestId = 0

const sortModeOptions = [
  { label: 'Sort: By Time', value: 'time' },
  { label: 'Sort: By Category', value: 'category' },
]

const viewToday = async (types: number[]) => {
  selectedFightTypes.value = [...types]
  await reloadFromFirstPage()
}

const viewTodayFromModal = (types: number[]) => {
  viewToday(types)
  showQuickFilters.value = false
}

const loadCategoryLogs = async () => {
  const requestId = ++categoryRequestId
  categoryLoading.value = true
  categoryError.value = false
  try {
    const res = await api(buildUrl(page.value, categoryPageSize)) as { total: number; data: any[] }
    if (requestId === categoryRequestId) {
      categoryTotal.value = res.total ?? 0
      const maximumPage = Math.max(Math.ceil(categoryTotal.value / categoryPageSize), 1)
      if (page.value > maximumPage) {
        categoryLogs.value = []
        page.value = maximumPage
        return
      }
      categoryLogs.value = res.data ?? []
    }
  } catch {
    if (requestId === categoryRequestId) {
      categoryLogs.value = []
      categoryTotal.value = 0
      categoryError.value = true
    }
  } finally {
    if (requestId === categoryRequestId) {
      categoryLoading.value = false
    }
  }
}

const onSortModeChange = async () => {
  syncUrl()
  if (categoryMode.value && categoryLogs.value.length === 0)
  {
    await loadCategoryLogs()
  }
}

const groupedCategoryLogs = computed(() => groupBySuperCategory(categoryLogs.value))

const aggregateRange = async (cat: { label: string; types: number[] }, windowMs: number, key: string) => {
  aggregating.value = cat.label + key
  noLogsToday.value = false
  try {
    const now = new Date()
    const since = new Date(now.getTime() - windowMs)
    const ids: number[] = []
    let aggregatePage = 1
    let total = 0
    do {
      const url = buildFightLogListUrl({
        page: aggregatePage,
        pageSize: 500,
        startDateTime: since.toISOString(),
        endDateTime: now.toISOString(),
        fightTypes: cat.types,
      })
      const res = await api(url) as { total: number; data: any[] }
      const rows = res.data ?? []
      total = res.total ?? 0
      ids.push(...rows.map((log: any) => log.fightLogId))
      if (rows.length === 0) {
        break
      }
      aggregatePage += 1
    } while (ids.length < total)
    if (ids.length === 0) {
      noLogsToday.value = true
      return
    }
    showQuickFilters.value = false
    navigateTo(`/logs/aggregate?ids=${ids.join(',')}`)
  } finally {
    aggregating.value = null
  }
}

onMounted(() => {
  if (categoryMode.value)
  {
    loadCategoryLogs()
  }
})

const charactersRequest = useAsyncData(
  'log-characters',
  () => api('/api/logs/characters') as Promise<string[]>
)
const guildsRequest = useAsyncData(
  'log-guilds',
  () => api('/api/logs/guilds') as Promise<{ guildId: string; guildName: string }[]>
)

const syncUrl = () => {
  router.replace({
    query: buildLogsRouteQuery({
      page: page.value,
      fightTypes: selectedFightTypes.value,
      guildIds: selectedGuildIds.value,
      characters: selectedCharacters.value,
      playstyles: selectedPlaystyles.value,
      sortMode: sortMode.value,
      successFilter: successFilter.value,
      difficultyFilter: difficultyFilter.value,
      startDateTime: startDate.value?.toISOString(),
      endDateTime: endDate.value?.toISOString(),
      minDurationSeconds: minDurationSeconds.value,
      maxDurationSeconds: maxDurationSeconds.value,
      minFightPercent: minFightPercent.value,
      maxFightPercent: maxFightPercent.value,
      sortField: sortField.value,
      sortOrder: sortOrder.value,
    }),
  })
}
const buildUrl = (pageOverride = page.value, pageSizeOverride = pageSize) => buildFightLogListUrl({
  page: pageOverride,
  pageSize: pageSizeOverride,
  fightTypes: selectedFightTypes.value,
  guildIds: selectedGuildIds.value,
  characters: selectedCharacters.value,
  playstyles: selectedPlaystyles.value,
  successFilter: successFilter.value,
  difficultyFilter: difficultyFilter.value,
  startDateTime: startDate.value?.toISOString(),
  endDateTime: endDate.value?.toISOString(),
  minDurationSeconds: minDurationSeconds.value,
  maxDurationSeconds: maxDurationSeconds.value,
  minFightPercent: minFightPercent.value,
  maxFightPercent: maxFightPercent.value,
  sortField: sortField.value,
  sortOrder: sortOrder.value,
})

const logsRequest = useAsyncData(
  'logs',
  () => categoryMode.value
    ? Promise.resolve({ total: 0, data: [] })
    : api(buildUrl()) as Promise<{ total: number; data: any[] }>,
  { watch: [page] }
)

const [charactersState, guildsState, logsState] = await Promise.all([charactersRequest, guildsRequest, logsRequest])
const { data: availableCharacters, pending: charactersPending } = charactersState
const { data: availableGuilds, pending: guildsPending } = guildsState
const { data, pending, refresh } = logsState

const allowedCharacters = new Set(availableCharacters.value ?? [])
const validCharacters = selectedCharacters.value.filter(character => allowedCharacters.has(character))
const allowedGuildIds = new Set((availableGuilds.value ?? []).map(guild => guild.guildId))
const validGuildIds = selectedGuildIds.value.filter(id => allowedGuildIds.has(id))
routeSelectionSanitized = routeSelectionSanitized || validGuildIds.length !== selectedGuildIds.value.length || validCharacters.length !== selectedCharacters.value.length
selectedCharacters.value = validCharacters
selectedGuildIds.value = validGuildIds
if (routeSelectionSanitized) {
  syncUrl()
  if (!categoryMode.value) {
    await refresh()
  }
}

watch(page, () => {
  lastClickedIndex.value = null
  lastClickedSource = null
  syncUrl()
})
watch(page, () => {
  if (categoryMode.value) {
    loadCategoryLogs()
  }
})

watch(() => route.query, async (query) => {
  const rawFightTypes = parseNumberListQuery(query.fightTypes)
  const rawGuildIds = parseGuildIds(query.guildIds)
  const rawCharacters = parseStringListQuery(query.characters)
  const rawPlaystyles = parseStringListQuery(query.playstyles)
  const allowedGuildIds = new Set((availableGuilds.value ?? []).map(guild => guild.guildId))
  const allowedCharacters = new Set(availableCharacters.value ?? [])
  const nextFightTypes = sanitizeFightTypes(rawFightTypes)
  const nextGuildIds = rawGuildIds.filter(id => allowedGuildIds.has(id))
  const nextCharacters = rawCharacters.filter(character => allowedCharacters.has(character))
  const nextPlaystyles = sanitizePlaystyles(rawPlaystyles)
  const nextSuccess: SuccessFilter = ['kills', 'wipes'].includes(String(query.result)) ? String(query.result) as SuccessFilter : 'all'
  const nextDifficulty = queryDifficulty(query.mode)
  const nextStart = queryDate(query.from)
  const nextEnd = queryDate(query.to)
  const nextPage = queryPage(query.page)
  const nextSortField = querySortField(query.sortField)
  const nextSortOrder: 'asc' | 'desc' = query.sortOrder === 'asc' ? 'asc' : 'desc'
  const nextSortMode: 'time' | 'category' = query.sort === 'category' ? 'category' : 'time'
  const stateChanged = JSON.stringify([
    selectedFightTypes.value, selectedGuildIds.value, selectedCharacters.value, selectedPlaystyles.value,
    successFilter.value, difficultyFilter.value, startDate.value?.toISOString(), endDate.value?.toISOString(),
    minDurationSeconds.value, maxDurationSeconds.value, minFightPercent.value, maxFightPercent.value,
    sortField.value, sortOrder.value, sortMode.value,
  ]) !== JSON.stringify([
    nextFightTypes, nextGuildIds, nextCharacters, nextPlaystyles,
    nextSuccess, nextDifficulty, nextStart?.toISOString(), nextEnd?.toISOString(),
    queryDuration(query.minDuration), queryDuration(query.maxDuration), queryBound(query.minPercent, 100), queryBound(query.maxPercent, 100),
    nextSortField, nextSortOrder, nextSortMode,
  ])
  const pageChanged = page.value !== nextPage
  const routeNeedsSanitizing = rawFightTypes.length !== nextFightTypes.length || rawGuildIds.length !== nextGuildIds.length ||
    rawCharacters.length !== nextCharacters.length || rawPlaystyles.length !== nextPlaystyles.length ||
    (query.page != null && queryNumber(query.page) !== nextPage) || queryNumber(query.minDuration) !== queryDuration(query.minDuration) ||
    queryNumber(query.maxDuration) !== queryDuration(query.maxDuration)
  if (!stateChanged && !pageChanged) {
    if (routeNeedsSanitizing) {
      syncUrl()
    }
    return
  }
  selectedFightTypes.value = nextFightTypes
  selectedGuildIds.value = nextGuildIds
  selectedCharacters.value = nextCharacters
  selectedPlaystyles.value = nextPlaystyles
  successFilter.value = nextSuccess
  difficultyFilter.value = nextDifficulty
  startDate.value = nextStart
  endDate.value = nextEnd
  minDurationSeconds.value = queryDuration(query.minDuration)
  maxDurationSeconds.value = queryDuration(query.maxDuration)
  minFightPercent.value = queryBound(query.minPercent, 100)
  maxFightPercent.value = queryBound(query.maxPercent, 100)
  sortField.value = nextSortField
  sortOrder.value = nextSortOrder
  sortMode.value = nextSortMode
  page.value = nextPage
  if (routeNeedsSanitizing) {
    syncUrl()
  }
  if (!pageChanged && !categoryMode.value) {
    await refresh()
  }
  if (categoryMode.value) {
    if (!pageChanged) {
      await loadCategoryLogs()
    }
  }
})

const reloadFromFirstPage = async () => {
  const pageChanged = page.value !== 1
  lastClickedIndex.value = null
  lastClickedSource = null
  page.value = 1
  syncUrl()
  if (!pageChanged && !categoryMode.value) {
    await refresh()
  }
  if (categoryMode.value) {
    if (!pageChanged) {
      await loadCategoryLogs()
    }
  }
}

const onFilterChange = () => reloadFromFirstPage()

const applyFilters = async () => {
  filterSnapshot = null
  showFilters.value = false
  await reloadFromFirstPage()
}

const captureFilterSnapshot = () => {
  filterSnapshot = {
    fightTypes: [...selectedFightTypes.value], guildIds: [...selectedGuildIds.value],
    characters: [...selectedCharacters.value], playstyles: [...selectedPlaystyles.value],
    success: successFilter.value, difficulty: difficultyFilter.value,
    start: startDate.value ? new Date(startDate.value) : null, end: endDate.value ? new Date(endDate.value) : null,
    minDuration: minDurationSeconds.value, maxDuration: maxDurationSeconds.value,
    minPercent: minFightPercent.value, maxPercent: maxFightPercent.value, view: sortMode.value,
  }
}

const restoreFilterSnapshot = () => {
  if (!filterSnapshot) {
    return
  }
  selectedFightTypes.value = filterSnapshot.fightTypes
  selectedGuildIds.value = filterSnapshot.guildIds
  selectedCharacters.value = filterSnapshot.characters
  selectedPlaystyles.value = filterSnapshot.playstyles
  successFilter.value = filterSnapshot.success
  difficultyFilter.value = filterSnapshot.difficulty
  startDate.value = filterSnapshot.start
  endDate.value = filterSnapshot.end
  minDurationSeconds.value = filterSnapshot.minDuration
  maxDurationSeconds.value = filterSnapshot.maxDuration
  minFightPercent.value = filterSnapshot.minPercent
  maxFightPercent.value = filterSnapshot.maxPercent
  sortMode.value = filterSnapshot.view
  filterSnapshot = null
}

const clearFilters = () => {
  selectedFightTypes.value = []
  selectedGuildIds.value = []
  selectedCharacters.value = []
  selectedPlaystyles.value = []
  successFilter.value = 'all'
  difficultyFilter.value = null
  startDate.value = null
  endDate.value = null
  minDurationSeconds.value = null
  maxDurationSeconds.value = null
  minFightPercent.value = null
  maxFightPercent.value = null
}

const activeFilterCount = computed(() => [
  selectedFightTypes.value.length > 0,
  selectedGuildIds.value.length > 0,
  selectedCharacters.value.length > 0,
  selectedPlaystyles.value.length > 0,
  successFilter.value !== 'all',
  difficultyFilter.value !== null,
  startDate.value !== null,
  endDate.value !== null,
  minDurationSeconds.value !== null,
  maxDurationSeconds.value !== null,
  minFightPercent.value !== null,
  maxFightPercent.value !== null,
].filter(Boolean).length)

watch(availableGuilds, async (guildOptions) => {
  if (!guildOptions) {
    return
  }
  const allowedIds = new Set(guildOptions.map(guild => guild.guildId))
  const validIds = selectedGuildIds.value.filter(id => allowedIds.has(id))
  if (validIds.length !== selectedGuildIds.value.length) {
    selectedGuildIds.value = validIds
    await reloadFromFirstPage()
  }
}, { immediate: true })

watch(availableCharacters, async (characterOptions) => {
  if (!characterOptions) {
    return
  }
  const allowed = new Set(characterOptions)
  const valid = selectedCharacters.value.filter(character => allowed.has(character))
  if (valid.length !== selectedCharacters.value.length) {
    selectedCharacters.value = valid
    await reloadFromFirstPage()
  }
}, { immediate: true })

const onSort = async (event: { sortField?: string; sortOrder?: number }) => {
  sortField.value = event.sortField ?? 'fightStart'
  sortOrder.value = event.sortOrder === 1 ? 'asc' : 'desc'
  await reloadFromFirstPage()
}

const onCategorySort = async (event: { sortField?: string; sortOrder?: number }) => {
  sortField.value = querySortField(event.sortField)
  sortOrder.value = event.sortOrder === 1 ? 'asc' : 'desc'
  await reloadFromFirstPage()
}

const logs = computed(() => data.value?.data ?? [])
const logsTotal = computed(() => data.value?.total ?? 0)

watch(logsTotal, (total) => {
  if (categoryMode.value) {
    return
  }
  const maximumPage = Math.max(Math.ceil(total / pageSize), 1)
  if (page.value > maximumPage) {
    page.value = maximumPage
  }
}, { immediate: true })

const selectedIds = computed(() => new Set(selectedLogs.value.map((l: any) => l.fightLogId)))
const isSelected = (row: any) => selectedIds.value.has(row.fightLogId)
const selectionLabel = (row: any) => `Select ${fightName(row.fightType)} log for ${row.characterName || 'unknown character'} from ${formatDateTime(row.fightStart)}`

const clearSelection = () => {
  selectedLogs.value = []
  lastClickedIndex.value = null
  lastClickedSource = null
}

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
  lastClickedIndex.value = null
  lastClickedSource = null
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

const handleSelect = (shiftKey: boolean, row: any, sourceRows = logs.value) => {
  const index = sourceRows.findIndex((l: any) => l.fightLogId === row.fightLogId)

  if (shiftKey && lastClickedIndex.value !== null && lastClickedSource === sourceRows)
  {
    const min = Math.min(lastClickedIndex.value, index)
    const max = Math.max(lastClickedIndex.value, index)
    const range = sourceRows.slice(min, max + 1)
    const existing = selectedIds.value
    const toAdd = range.filter((l: any) => !existing.has(l.fightLogId))
    selectedLogs.value = [...selectedLogs.value, ...toAdd]
  }
  else
  {
    toggleRow(row)
    lastClickedIndex.value = index
    lastClickedSource = sourceRows
  }
}

const onCheckboxClick = (e: MouseEvent, row: any, sourceRows = logs.value) => {
  handleSelect(e.shiftKey, row, sourceRows)
}

const onCheckboxMouseDown = (e: MouseEvent) => {
  if (e.shiftKey) {
    e.preventDefault()
  }
}

const goToAggregate = () => {
  const ids = selectedLogs.value.map((l: any) => l.fightLogId).join(',')
  navigateTo(`/logs/aggregate?ids=${ids}`)
}

</script>

<style scoped>
.log-links {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.external-log-link {
  color: var(--p-text-muted-color);
  display: flex;
  align-items: center;
}

.external-log-link > i {
  font-size: 0.875rem;
}

.quick-filter-bar {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  flex-wrap: wrap;
  margin-bottom: 1rem;
}

.table-pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 1rem;
  color: var(--p-text-muted-color);
  font-size: 0.875rem;
}

.quick-filter-group {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.quick-filter-group > span {
  color: var(--p-text-muted-color);
  font-size: 0.8rem;
  font-weight: 600;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  min-width: 0;
}

.filter-field > span {
  color: var(--p-text-muted-color);
  font-size: 0.8rem;
  font-weight: 600;
}

.filter-field > :deep(*) {
  width: 100%;
}

.filter-wide {
  grid-column: 1 / -1;
}

@media (max-width: 640px) {
  .filter-grid {
    grid-template-columns: 1fr;
  }

  .filter-wide {
    grid-column: auto;
  }
}

.checkbox-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  min-height: 2.5rem;
  cursor: pointer;
  touch-action: manipulation;
  user-select: none;
  -webkit-user-select: none;
}

.quick-buttons-modal {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.quick-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.quick-actions {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
  margin-left: auto;
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

.category-fight-group-wrap {
  margin: 0.25rem 0 0 0.5rem;
}
.category-fight-group-wrap :deep(.collapsible-section) {
  margin-top: 0.35rem;
}
.category-fight-group-wrap :deep(.collapsible-title) {
  font-size: 0.875rem;
}
.category-table {
  margin-top: 0.25rem;
}
</style>
