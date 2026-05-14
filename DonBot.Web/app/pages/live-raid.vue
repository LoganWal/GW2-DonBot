<template>
  <div>
    <div class="header-row">
      <h1 class="page-title" style="margin: 0;">Live Raid</h1>
      <Select
        v-model="selectedGuildIdStr"
        :options="guildOptions ?? []"
        option-label="guildName"
        option-value="guildId"
        :placeholder="guildsPending ? 'Loading servers...' : 'Select a server'"
        :loading="guildsPending"
        :disabled="guildsPending"
        style="min-width: 260px;"
      />
      <Tag v-if="report?.isOpen" severity="success" value="LIVE" />
      <Tag v-else-if="report" severity="secondary" :value="`Closed ${formatRelative(report.fightsEnd)}`" />
      <span v-if="report" style="color: var(--p-text-muted-color); font-size: 0.85rem;">
        Started {{ new Date(report.fightsStart).toLocaleString() }}
      </span>
      <div style="margin-left: auto; display: flex; gap: 0.5rem;">
        <Button
          v-if="guildsPending || reportPending"
          icon="pi pi-spinner pi-spin"
          label="Loading..."
          severity="secondary"
          disabled
        />
        <Button
          v-else-if="!report || !report.isOpen"
          icon="pi pi-play"
          label="Start Raid"
          severity="success"
          :disabled="!selectedGuildIdStr || actionPending"
          @click="startRaid"
        />
        <Button
          v-else
          icon="pi pi-stop"
          label="Stop Raid"
          severity="danger"
          :disabled="actionPending"
          @click="stopRaid"
        />
      </div>
    </div>

    <Message v-if="actionError" severity="error" :closable="true" @close="actionError = null">
      {{ actionError }}
    </Message>

    <template v-if="!selectedGuildIdStr">
      <Message severity="info" :closable="false">Select a server above to view its raid.</Message>
    </template>

    <template v-else-if="reportPending">
      <ProgressSpinner />
    </template>

    <template v-else-if="!report">
      <Message severity="secondary" :closable="false">
        No raid has been started for this server yet.
      </Message>
    </template>

    <template v-else-if="report.fightLogIds.length === 0 && report.isOpen">
      <Message severity="info" :closable="false">
        Raid started. Waiting for the first log to come in...
      </Message>
    </template>

    <template v-else-if="report.fightLogIds.length === 0">
      <Message severity="secondary" :closable="false">
        No raid is currently running. Start a raid above to begin tracking.
      </Message>
    </template>

    <template v-else>
      <section v-if="selectedFightLogId" class="section">
        <SectionTitle style="margin: 0 0 0.75rem">Last Log</SectionTitle>
        <LogsAggregate
          :fetch-aggregate="fetchSingleLogAggregate"
          :reload-key="selectedFightLogId"
          hide-logs-tab
        />
      </section>

      <section class="section">
        <SectionTitle style="margin: 0 0 0.75rem">Aggregate</SectionTitle>
        <LogsAggregate
          :fetch-aggregate="fetchAggregate"
          :reload-key="reloadKey"
          :selected-log-id="selectedFightLogId"
          row-action="select"
          @select-log="onSelectLog"
        />
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import LogsAggregate from '~/components/LogsAggregate.vue'

definePageMeta({ middleware: 'auth' })

usePageTitle()

const api = useApi()
const apiBase = useRuntimeConfig().public.apiBase as string

const { data: guildOptions, pending: guildsPending } = useLazyAsyncData(
  'live-raid-guilds',
  () => $fetch<{ guildId: string; guildName: string }[]>(`${apiBase}/api/live-raid/guilds`, { credentials: 'include' }),
  { default: () => [] }
)

const selectedGuildIdStr = ref<string | null>(null)

watchEffect(() => {
  if (!selectedGuildIdStr.value && (guildOptions.value?.length ?? 0) > 0) {
    selectedGuildIdStr.value = guildOptions.value![0]!.guildId
  }
})

// Discord snowflakes exceed Number.MAX_SAFE_INTEGER, so we must keep them as
// strings end-to-end. Converting via Number() corrupts the last few digits.
const guildIdStr = computed<string | null>(() => selectedGuildIdStr.value)

const { report, pending: reportPending, reloadKey, refresh: refreshReport } = useLiveRaid(guildIdStr)

const selectedFightLogId = ref<number | null>(null)

watch(report, (r, prev) => {
  if (!r) {
    selectedFightLogId.value = null
    return
  }
  const ids = r.fightLogIds
  if (ids.length === 0) {
    selectedFightLogId.value = null
    return
  }
  // Auto-select latest unless user manually picked one that's still present.
  const hasSelection = selectedFightLogId.value && ids.includes(selectedFightLogId.value)
  if (!hasSelection) {
    selectedFightLogId.value = ids[ids.length - 1]!
  } else if (prev && r.reportId === prev.reportId) {
    // Same report, new fight added: keep user's selection unless they're already on the newest.
    const wasOnLatest = selectedFightLogId.value === prev.fightLogIds[prev.fightLogIds.length - 1]
    if (wasOnLatest) {
      selectedFightLogId.value = ids[ids.length - 1]!
    }
  }
}, { deep: true })

const onSelectLog = (fightLogId: number) => {
  selectedFightLogId.value = fightLogId
}

const fetchAggregate = (logIds?: number[]) => {
  const qs = logIds && logIds.length > 0 ? `?logIds=${logIds.join(',')}` : ''
  return $fetch(`${apiBase}/api/live-raid/${guildIdStr.value}/aggregate${qs}`, { credentials: 'include' })
}

const fetchSingleLogAggregate = () => {
  if (!selectedFightLogId.value) {
    return Promise.resolve(null)
  }
  return $fetch(`${apiBase}/api/live-raid/${guildIdStr.value}/aggregate?logIds=${selectedFightLogId.value}`, { credentials: 'include' })
}

const actionPending = ref(false)
const actionError = ref<string | null>(null)

const startRaid = async () => {
  if (!guildIdStr.value) {
    return
  }
  actionPending.value = true
  actionError.value = null
  try {
    await api(`/api/live-raid/${guildIdStr.value}/start`, { method: 'POST' })
    await refreshReport()
  } catch (e: any) {
    actionError.value = e?.data?.error ?? e?.message ?? 'Failed to start raid.'
  } finally {
    actionPending.value = false
  }
}

const stopRaid = async () => {
  if (!guildIdStr.value) {
    return
  }
  actionPending.value = true
  actionError.value = null
  try {
    await api(`/api/live-raid/${guildIdStr.value}/stop`, { method: 'POST' })
    await refreshReport()
  } catch (e: any) {
    actionError.value = e?.data?.error ?? e?.message ?? 'Failed to stop raid.'
  } finally {
    actionPending.value = false
  }
}

const formatRelative = (iso: string | null) => {
  if (!iso) {
    return ''
  }
  const diff = Date.now() - new Date(iso).getTime()
  const minutes = Math.floor(diff / 60_000)
  if (minutes < 1) {
    return 'just now'
  }
  if (minutes < 60) {
    return `${minutes}m ago`
  }
  const hours = Math.floor(minutes / 60)
  if (hours < 24) {
    return `${hours}h ago`
  }
  const days = Math.floor(hours / 24)
  return `${days}d ago`
}
</script>

<style scoped>
.header-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 1.5rem;
}

.section {
  margin-top: 2rem;
}


</style>
