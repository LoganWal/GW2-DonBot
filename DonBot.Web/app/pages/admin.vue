<template>
  <div>
    <h1 class="page-title">Server Administration</h1>

    <ProgressSpinner v-if="guildsPending" style="width: 2rem; height: 2rem;" />

    <Card v-else-if="!guilds?.length">
      <template #content>
        <p style="margin: 0; color: var(--p-text-muted-color);">
          You don't have administrator permission in any server DonBot is in.
        </p>
      </template>
    </Card>

    <template v-else>
      <Card style="margin-bottom: 1.5rem;">
        <template #content>
          <div class="guild-picker">
            <label for="guild-select">Server</label>
            <Select
              id="guild-select"
              v-model="selectedGuildId"
              :options="guilds"
              option-label="name"
              option-value="guildId"
              placeholder="Select a server"
              style="min-width: 320px;"
            />
          </div>
        </template>
      </Card>

      <ProgressSpinner v-if="configPending && selectedGuildId" style="width: 2rem; height: 2rem;" />

      <div v-else-if="working" class="config-grid">
        <Card>
          <template #title>Channels</template>
          <template #content>
            <div class="field-grid">
              <div v-for="f in channelFields" :key="f.key" class="field">
                <label :for="f.key">
                  {{ f.label }}
                  <i class="pi pi-info-circle field-info" v-tooltip.top="f.tip" />
                </label>
                <Select
                  :id="f.key"
                  v-model="working[f.key]"
                  :options="channels"
                  option-label="name"
                  option-value="id"
                  placeholder="None"
                  show-clear
                  filter
                  style="width: 100%;"
                />
              </div>
            </div>
          </template>
        </Card>

        <Card>
          <template #title>Discord roles</template>
          <template #content>
            <div class="field-grid">
              <div v-for="f in roleFields" :key="f.key" class="field">
                <label :for="f.key">
                  {{ f.label }}
                  <i class="pi pi-info-circle field-info" v-tooltip.top="f.tip" />
                </label>
                <Select
                  :id="f.key"
                  v-model="working[f.key]"
                  :options="roles"
                  option-label="name"
                  option-value="id"
                  placeholder="None"
                  show-clear
                  filter
                  style="width: 100%;"
                />
              </div>
            </div>
          </template>
        </Card>

        <Card>
          <template #title>GW2 guild IDs</template>
          <template #content>
            <div class="field-grid">
              <div class="field">
                <label for="gw2-guild">
                  Primary GW2 guild ID
                  <i class="pi pi-info-circle field-info" v-tooltip.top="gw2PrimaryTip" />
                </label>
                <InputText id="gw2-guild" v-model="working.gw2GuildMemberRoleId" style="width: 100%;" />
              </div>
              <div class="field">
                <label for="gw2-secondary">
                  Secondary GW2 guild IDs (comma separated)
                  <i class="pi pi-info-circle field-info" v-tooltip.top="gw2SecondaryTip" />
                </label>
                <InputText id="gw2-secondary" v-model="working.gw2SecondaryMemberRoleIds" style="width: 100%;" />
              </div>
            </div>
          </template>
        </Card>

        <Card>
          <template #title>Feature toggles</template>
          <template #content>
            <div class="toggle-grid">
              <div v-for="t in toggleFields" :key="t.key" class="toggle-row">
                <ToggleSwitch v-model="working[t.key]" :input-id="t.key" />
                <label :for="t.key">
                  {{ t.label }}
                  <i class="pi pi-info-circle field-info" v-tooltip.top="t.tip" />
                </label>
              </div>
            </div>
          </template>
        </Card>

        <div class="actions">
          <Button label="Cancel" severity="secondary" :disabled="!isDirty || saving" @click="cancel" />
          <Button label="Save changes" icon="pi pi-check" :loading="saving" :disabled="!isDirty || saving" @click="save" />
        </div>
      </div>

    </template>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()
const toast = useToast()

type GuildSummary = { guildId: string; name: string; iconUrl: string | null }
type Channel = { id: string; name: string }
type Role = { id: string; name: string }
type Config = {
  logDropOffChannelId: string | null
  discordGuildMemberRoleId: string | null
  discordSecondaryMemberRoleId: string | null
  discordVerifiedRoleId: string | null
  gw2GuildMemberRoleId: string | null
  gw2SecondaryMemberRoleIds: string | null
  announcementChannelId: string | null
  logReportChannelId: string | null
  advanceLogReportChannelId: string | null
  streamLogChannelId: string | null
  raidAlertEnabled: boolean
  raidAlertChannelId: string | null
  removeSpamEnabled: boolean
  removedMessageChannelId: string | null
  autoSubmitToWingman: boolean
  autoAggregateLogs: boolean
  autoReplySingleLog: boolean
  wvwLeaderboardEnabled: boolean
  wvwLeaderboardChannelId: string | null
  pveLeaderboardEnabled: boolean
  pveLeaderboardChannelId: string | null
}
type ConfigResponse = {
  guildId: string
  guildName: string
  config: Config
  channels: Channel[]
  roles: Role[]
}

type Field = { key: keyof Config; label: string; tip: string }

const channelFields: Field[] = [
  { key: 'logDropOffChannelId', label: 'Log drop-off channel', tip: 'Webhook messages with dps.report links posted here are auto-processed without prompting users.' },
  { key: 'announcementChannelId', label: 'Announcement channel', tip: 'Channel used for general bot announcements (raid signups, etc.).' },
  { key: 'logReportChannelId', label: 'Log report channel', tip: 'Where the bot posts the standard fight summary embeds for processed logs.' },
  { key: 'advanceLogReportChannelId', label: 'Advance log report channel', tip: 'Optional second channel for the more detailed WvW report. Leave empty to skip.' },
  { key: 'streamLogChannelId', label: 'Stream log channel', tip: 'Channel for raw streaming log output.' },
  { key: 'raidAlertChannelId', label: 'Raid alert channel', tip: 'Channel where raid alerts are posted when "Raid alerts enabled" is on.' },
  { key: 'removedMessageChannelId', label: 'Removed message channel', tip: 'Where removed spam messages are logged when "Remove spam enabled" is on.' },
  { key: 'wvwLeaderboardChannelId', label: 'WvW leaderboard channel', tip: 'Channel where the weekly WvW leaderboard is posted when enabled.' },
  { key: 'pveLeaderboardChannelId', label: 'PvE leaderboard channel', tip: 'Channel where the weekly PvE leaderboard is posted when enabled.' },
]

const roleFields: Field[] = [
  { key: 'discordGuildMemberRoleId', label: 'Guild member role', tip: 'Discord role assigned to members of the primary GW2 guild.' },
  { key: 'discordSecondaryMemberRoleId', label: 'Secondary member role', tip: 'Discord role assigned to members of any of the secondary GW2 guilds.' },
  { key: 'discordVerifiedRoleId', label: 'Verified role', tip: 'Discord role assigned to anyone who has verified their GW2 account.' },
]

const toggleFields: Field[] = [
  { key: 'raidAlertEnabled', label: 'Raid alerts enabled', tip: 'Allow scheduled raid alert messages to be posted in the raid alert channel.' },
  { key: 'removeSpamEnabled', label: 'Remove spam enabled', tip: 'Auto-delete messages from unverified users that contain dps.report or wingman links.' },
  { key: 'autoSubmitToWingman', label: 'Auto-submit logs to Wingman', tip: 'Automatically forward processed dps.report logs to gw2wingman for import.' },
  { key: 'autoAggregateLogs', label: 'Auto-aggregate logs', tip: 'When multiple logs are shared at once, prompt to post a single combined summary.' },
  { key: 'autoReplySingleLog', label: 'Auto-reply to single logs', tip: 'Reply with a fight summary whenever a single log is shared.' },
  { key: 'wvwLeaderboardEnabled', label: 'WvW leaderboard enabled', tip: 'Post the weekly WvW leaderboard automatically.' },
  { key: 'pveLeaderboardEnabled', label: 'PvE leaderboard enabled', tip: 'Post the weekly PvE leaderboard automatically.' },
]

const gw2PrimaryTip = 'GW2 guild ID (UUID) used to assign the primary Guild member role.'
const gw2SecondaryTip = 'Comma-separated GW2 guild IDs (UUIDs) used to assign the Secondary member role.'

const selectedGuildId = ref<string | null>(null)
const working = ref<Config | null>(null)
const original = ref<Config | null>(null)
const channels = ref<Channel[]>([])
const roles = ref<Role[]>([])
const saving = ref(false)

const { data: guilds, pending: guildsPending } = await useAsyncData(
  'admin-guilds',
  () => api('/api/admin/guilds') as Promise<GuildSummary[]>
)

watch(guilds, (gs) => {
  if (!selectedGuildId.value && gs?.length) selectedGuildId.value = gs[0]?.guildId ?? null
}, { immediate: true })

const configPending = ref(false)

const loadConfig = async (guildId: string) => {
  configPending.value = true
  try {
    const res = await api(`/api/admin/guilds/${guildId}/config`) as ConfigResponse
    channels.value = res.channels
    roles.value = res.roles
    original.value = { ...res.config }
    working.value = { ...res.config }
  } catch {
    toast.add({ severity: 'error', summary: 'Load failed', detail: 'Failed to load configuration.', life: 4000 })
    working.value = null
    original.value = null
  } finally {
    configPending.value = false
  }
}

const isDirty = computed(() => {
  if (!working.value || !original.value) return false
  return JSON.stringify(working.value) !== JSON.stringify(original.value)
})

let suppressGuildWatch = false

watch(selectedGuildId, (id, previousId) => {
  if (suppressGuildWatch) {
    suppressGuildWatch = false
    return
  }
  if (!id) return
  if (isDirty.value && previousId && previousId !== id) {
    const ok = window.confirm('You have unsaved changes. Discard them and switch server?')
    if (!ok) {
      suppressGuildWatch = true
      selectedGuildId.value = previousId
      return
    }
  }
  loadConfig(id)
}, { immediate: true })

const cancel = () => {
  if (original.value) working.value = { ...original.value }
}

const save = async () => {
  if (!selectedGuildId.value || !working.value) return
  saving.value = true
  try {
    const updated = await api(`/api/admin/guilds/${selectedGuildId.value}/config`, {
      method: 'PUT',
      body: working.value,
    }) as Config
    original.value = { ...updated }
    working.value = { ...updated }
    toast.add({ severity: 'success', summary: 'Saved', detail: 'Server configuration updated.', life: 3000 })
  } catch (err: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: err?.data ?? 'Could not save changes.', life: 5000 })
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.guild-picker {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.guild-picker label {
  font-weight: 600;
  font-size: 0.9rem;
}

.config-grid {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.field-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.field label {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
  font-weight: 500;
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.field-info {
  font-size: 0.75rem;
  color: var(--p-primary-color);
  opacity: 0.75;
  cursor: help;
}

.field-info:hover {
  opacity: 1;
}

.toggle-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 0.75rem;
}

.toggle-row {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.toggle-row label {
  font-size: 0.875rem;
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
}
</style>
