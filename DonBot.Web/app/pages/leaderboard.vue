<template>
  <div>
    <h1 class="page-title">Leaderboard</h1>
    <div style="margin-bottom: 1.5rem; max-width: 320px;">
      <Select
        v-model="selectedGuildId"
        :options="guildOptions"
        option-label="label"
        option-value="value"
        placeholder="Select a guild"
        :loading="guildsPending"
        :disabled="!guildOptions.length"
        style="width: 100%;"
        @change="load"
      />
    </div>
    <DataTable v-if="leaderboard && leaderboard.length" :value="leaderboard" :loading="loading" striped-rows>
      <Column header="#" style="width: 3rem;">
        <template #body="{ index }">{{ index + 1 }}</template>
      </Column>
      <Column field="guildWarsAccountName" header="Account" />
      <Column field="totalDamage" header="Total Damage">
        <template #body="{ data }">{{ data.totalDamage.toLocaleString() }}</template>
      </Column>
      <Column field="totalFights" header="Fights" />
    </DataTable>
    <Message v-else-if="!loading && selectedGuildId !== null" severity="secondary" :closable="false">
      No leaderboard data for this guild.
    </Message>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()

const { data: guildIds, pending: guildsPending } = await useAsyncData(
  'my-guilds',
  () => api('/api/guilds/mine') as Promise<number[]>
)

const guildOptions = computed(() =>
  (guildIds.value ?? []).map((id: number) => ({ label: id === -1 ? 'Global' : `Guild ${id}`, value: id }))
)

const selectedGuildId = ref<number | null>(null)
const leaderboard = ref<any[] | null>(null)
const loading = ref(false)

watch(guildOptions, (opts) => {
  if (opts.length === 1 && selectedGuildId.value === null)
  {
    selectedGuildId.value = opts[0].value
    load()
  }
}, { immediate: true })

const load = async () => {
  if (selectedGuildId.value === null)
  {
    return
  }
  loading.value = true
  try
  {
    leaderboard.value = await api(`/api/guilds/${selectedGuildId.value}/leaderboard`) as any[]
  }
  finally
  {
    loading.value = false
  }
}
</script>
