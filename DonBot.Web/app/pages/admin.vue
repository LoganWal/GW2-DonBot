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
          <template #title>GW2 guilds</template>
          <template #content>
            <div class="gw2-section">
              <div class="gw2-current">
                <div class="gw2-current-row">
                  <label class="gw2-current-label">
                    Primary
                    <i class="pi pi-info-circle field-info" v-tooltip.top="gw2PrimaryTip" />
                  </label>
                  <div class="gw2-current-value">
                    <Chip v-if="primaryChip" :label="primaryChip.label" removable @remove="clearPrimary" />
                    <span v-else class="empty-hint">None set.</span>
                  </div>
                </div>
                <div class="gw2-current-row">
                  <label class="gw2-current-label">
                    Secondary
                    <i class="pi pi-info-circle field-info" v-tooltip.top="gw2SecondaryTip" />
                  </label>
                  <div class="gw2-current-value">
                    <template v-if="secondaryChips.length">
                      <Chip
                        v-for="chip in secondaryChips"
                        :key="chip.id"
                        :label="chip.label"
                        removable
                        @remove="removeSecondary(chip.id)"
                      />
                    </template>
                    <span v-else class="empty-hint">None set.</span>
                  </div>
                </div>
              </div>

              <Divider />

              <div v-if="myGuildsPending">
                <ProgressSpinner style="width: 1.5rem; height: 1.5rem;" />
              </div>
              <Message v-else-if="myGuilds && !myGuilds.hasAccount" severity="info" :closable="false">
                <span>
                  Link a GW2 account on the
                  <NuxtLink to="/verify" style="color: var(--p-primary-color); text-decoration: underline;">verify page</NuxtLink>
                  to pick guilds from a list instead of pasting IDs.
                </span>
              </Message>
              <div v-else-if="myGuilds && myGuilds.guilds.length === 0" style="font-size: 0.85rem; color: var(--p-text-muted-color);">
                Your linked GW2 account isn't a member of any guilds.
              </div>
              <div v-else-if="myGuilds" class="my-guilds">
                <div class="section-label">Your guilds</div>
                <div v-for="g in myGuilds.guilds" :key="g.id" class="my-guild-row">
                  <div class="my-guild-name">
                    <span>
                      {{ g.name }}
                      <span v-if="g.tag" class="my-guild-tag">[{{ g.tag }}]</span>
                    </span>
                    <span class="my-guild-id">{{ g.id }}</span>
                  </div>
                  <div class="my-guild-actions">
                    <Button
                      v-if="working.gw2GuildMemberRoleId === g.id"
                      label="Primary"
                      icon="pi pi-check"
                      size="small"
                      severity="success"
                      disabled
                    />
                    <Button
                      v-else
                      label="Set primary"
                      icon="pi pi-star"
                      size="small"
                      severity="secondary"
                      outlined
                      @click="setPrimary(g)"
                    />
                    <Button
                      v-if="secondaryIdSet.has(g.id)"
                      label="In secondary"
                      icon="pi pi-check"
                      size="small"
                      severity="success"
                      disabled
                    />
                    <Button
                      v-else
                      label="Add secondary"
                      icon="pi pi-plus"
                      size="small"
                      severity="secondary"
                      outlined
                      :disabled="working.gw2GuildMemberRoleId === g.id"
                      @click="addSecondary(g)"
                    />
                  </div>
                </div>
              </div>

              <Divider />

              <details class="manual-entry">
                <summary>Add a guild by name</summary>
                <div class="manual-entry-body">
                  <InputText
                    v-model="searchName"
                    placeholder="Guild name (min 3 chars)"
                    style="flex: 1;"
                    @keyup.enter="runSearch"
                  />
                  <Button
                    label="Search"
                    icon="pi pi-search"
                    size="small"
                    :loading="searching"
                    :disabled="searchName.trim().length < 3"
                    @click="runSearch"
                  />
                </div>
                <div v-if="searchAttempted && !searching" class="search-results">
                  <div v-if="searchResults.length === 0" class="empty-hint">
                    No guilds found matching "{{ lastSearchTerm }}".
                  </div>
                  <div v-else class="my-guilds">
                    <div v-for="g in searchResults" :key="g.id" class="my-guild-row">
                      <div class="my-guild-name">
                        <span>
                          {{ g.name }}
                          <span v-if="g.tag" class="my-guild-tag">[{{ g.tag }}]</span>
                        </span>
                        <span class="my-guild-id">{{ g.id }}</span>
                      </div>
                      <div class="my-guild-actions">
                        <Button
                          v-if="working.gw2GuildMemberRoleId === g.id"
                          label="Primary"
                          icon="pi pi-check"
                          size="small"
                          severity="success"
                          disabled
                        />
                        <Button
                          v-else
                          label="Set primary"
                          icon="pi pi-star"
                          size="small"
                          severity="secondary"
                          outlined
                          @click="setPrimary(g)"
                        />
                        <Button
                          v-if="secondaryIdSet.has(g.id)"
                          label="In secondary"
                          icon="pi pi-check"
                          size="small"
                          severity="success"
                          disabled
                        />
                        <Button
                          v-else
                          label="Add secondary"
                          icon="pi pi-plus"
                          size="small"
                          severity="secondary"
                          outlined
                          :disabled="working.gw2GuildMemberRoleId === g.id"
                          @click="addSecondary(g)"
                        />
                      </div>
                    </div>
                  </div>
                </div>
              </details>
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

        <Card>
          <template #title>Server quotes</template>
          <template #content>
            <div class="quotes-section">
              <div v-if="quotesPending" class="empty-hint">
                <ProgressSpinner style="width: 1.5rem; height: 1.5rem;" />
              </div>
              <template v-else>
                <div v-if="!quotes.length" class="empty-hint">No quotes yet.</div>
                <div v-else class="quotes-list">
                  <div v-for="q in quotes" :key="q.quoteId" class="quote-row">
                    <template v-if="editingQuoteId === q.quoteId">
                      <Textarea
                        v-model="editingQuoteText"
                        auto-resize
                        rows="2"
                        style="flex: 1;"
                        :maxlength="maxQuoteLength"
                      />
                      <div class="quote-actions">
                        <Button
                          label="Save"
                          icon="pi pi-check"
                          size="small"
                          :loading="quoteSaving"
                          :disabled="!editingQuoteText.trim() || quoteSaving"
                          @click="saveEditQuote(q)"
                        />
                        <Button
                          label="Cancel"
                          icon="pi pi-times"
                          size="small"
                          severity="secondary"
                          outlined
                          :disabled="quoteSaving"
                          @click="cancelEditQuote"
                        />
                      </div>
                    </template>
                    <template v-else>
                      <div class="quote-text">{{ q.quote }}</div>
                      <div class="quote-actions">
                        <Button
                          icon="pi pi-pencil"
                          size="small"
                          severity="secondary"
                          outlined
                          aria-label="Edit"
                          @click="startEditQuote(q)"
                        />
                        <Button
                          icon="pi pi-trash"
                          size="small"
                          severity="danger"
                          outlined
                          aria-label="Delete"
                          :loading="deletingQuoteId === q.quoteId"
                          @click="deleteQuote(q)"
                        />
                      </div>
                    </template>
                  </div>
                </div>

                <Divider />

                <div class="quote-new">
                  <div class="section-label">Add a quote</div>
                  <Textarea
                    v-model="newQuoteText"
                    auto-resize
                    rows="2"
                    placeholder="New quote..."
                    style="width: 100%;"
                    :maxlength="maxQuoteLength"
                  />
                  <div class="quote-new-actions">
                    <Button
                      label="Add quote"
                      icon="pi pi-plus"
                      size="small"
                      :loading="addingQuote"
                      :disabled="!newQuoteText.trim() || addingQuote"
                      @click="addQuote"
                    />
                  </div>
                </div>
              </template>
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
const { confirmDelete } = useConfirmAction()

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
type Gw2Guild = { id: string; name: string; tag: string | null }
type ConfigResponse = {
  guildId: string
  guildName: string
  config: Config
  channels: Channel[]
  roles: Role[]
  gw2GuildNames: Gw2Guild[]
}
type MyGw2GuildsResponse = { hasAccount: boolean; guilds: Gw2Guild[] }

type Field = { key: keyof Config; label: string; tip: string }
type BooleanKey = { [K in keyof Config]: Config[K] extends boolean ? K : never }[keyof Config]
type BooleanField = { key: BooleanKey; label: string; tip: string }

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

const toggleFields: BooleanField[] = [
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

const { selectedGuildId, ensureSelection } = useSelectedGuild()
const working = ref<Config | null>(null)
const original = ref<Config | null>(null)
const channels = ref<Channel[]>([])
const roles = ref<Role[]>([])
const saving = ref(false)
const searchName = ref('')
const searching = ref(false)
const searchResults = ref<Gw2Guild[]>([])
const searchAttempted = ref(false)
const lastSearchTerm = ref('')
const gw2NameCache = ref<Record<string, { name: string; tag: string | null }>>({})

type Quote = { quoteId: number; quote: string }
const maxQuoteLength = 1000
const quotes = ref<Quote[]>([])
const quotesPending = ref(false)
const newQuoteText = ref('')
const addingQuote = ref(false)
const editingQuoteId = ref<number | null>(null)
const editingQuoteText = ref('')
const quoteSaving = ref(false)
const deletingQuoteId = ref<number | null>(null)

const cacheGuild = (g: Gw2Guild) => {
  gw2NameCache.value[g.id] = { name: g.name, tag: g.tag }
}

const { data: myGuilds, pending: myGuildsPending } = await useAsyncData(
  'admin-my-gw2-guilds',
  () => api('/api/admin/gw2/my-guilds') as Promise<MyGw2GuildsResponse>
)

watch(myGuilds, (mg) => {
  if (!mg) return
  for (const g of mg.guilds) cacheGuild(g)
}, { immediate: true })

const parseSecondary = (raw: string | null | undefined): string[] =>
  (raw ?? '')
    .split(',')
    .map(s => s.trim())
    .filter(s => s.length > 0)

const secondaryIds = computed(() => parseSecondary(working.value?.gw2SecondaryMemberRoleIds ?? null))
const secondaryIdSet = computed(() => new Set(secondaryIds.value))

const labelFor = (id: string) => {
  const entry = gw2NameCache.value[id]
  if (!entry) return id
  return entry.tag ? `${entry.name} [${entry.tag}]` : entry.name
}

const primaryChip = computed(() => {
  const id = working.value?.gw2GuildMemberRoleId
  if (!id) return null
  return { id, label: labelFor(id) }
})

const secondaryChips = computed(() =>
  secondaryIds.value.map(id => ({ id, label: labelFor(id) }))
)

const setPrimary = (g: Gw2Guild) => {
  if (!working.value) return
  cacheGuild(g)
  working.value.gw2GuildMemberRoleId = g.id
  // remove from secondary if present
  if (secondaryIdSet.value.has(g.id)) {
    working.value.gw2SecondaryMemberRoleIds = secondaryIds.value.filter(id => id !== g.id).join(',') || null
  }
}

const clearPrimary = () => {
  if (!working.value) return
  working.value.gw2GuildMemberRoleId = null
}

const addSecondary = (g: Gw2Guild) => {
  if (!working.value) return
  if (secondaryIdSet.value.has(g.id)) return
  if (working.value.gw2GuildMemberRoleId === g.id) return
  cacheGuild(g)
  const next = [...secondaryIds.value, g.id]
  working.value.gw2SecondaryMemberRoleIds = next.join(',')
}

const removeSecondary = (id: string) => {
  if (!working.value) return
  const next = secondaryIds.value.filter(x => x !== id)
  working.value.gw2SecondaryMemberRoleIds = next.length ? next.join(',') : null
}

const runSearch = async () => {
  const term = searchName.value.trim()
  if (term.length < 3) return
  searching.value = true
  lastSearchTerm.value = term
  try {
    const results = await api(`/api/admin/gw2/search?name=${encodeURIComponent(term)}`) as Gw2Guild[]
    searchResults.value = results
    for (const g of results) cacheGuild(g)
  } catch {
    searchResults.value = []
  } finally {
    searchAttempted.value = true
    searching.value = false
  }
}

const { data: guilds, pending: guildsPending } = await useAsyncData(
  'admin-guilds',
  () => api('/api/admin/guilds') as Promise<GuildSummary[]>
)

watch(guilds, (gs) => {
  if (!gs?.length) return
  ensureSelection(gs.map(g => g.guildId), gs[0]?.guildId ?? null)
}, { immediate: true })

const configPending = ref(false)

const loadQuotes = async (guildId: string) => {
  quotesPending.value = true
  try {
    quotes.value = await api(`/api/admin/guilds/${guildId}/quotes`) as Quote[]
  } catch {
    quotes.value = []
    toast.add({ severity: 'error', summary: 'Load failed', detail: 'Failed to load quotes.', life: 4000 })
  } finally {
    quotesPending.value = false
  }
}

const addQuote = async () => {
  if (!selectedGuildId.value) {
    return
  }
  const text = newQuoteText.value.trim()
  if (!text) {
    return
  }
  addingQuote.value = true
  try {
    const created = await api(`/api/admin/guilds/${selectedGuildId.value}/quotes`, {
      method: 'POST',
      body: { quote: text },
    }) as Quote
    quotes.value = [...quotes.value, created]
    newQuoteText.value = ''
    toast.add({ severity: 'success', summary: 'Added', detail: 'Quote added.', life: 2500 })
  } catch (err: any) {
    toast.add({ severity: 'error', summary: 'Add failed', detail: err?.data ?? 'Could not add quote.', life: 4000 })
  } finally {
    addingQuote.value = false
  }
}

const startEditQuote = (q: Quote) => {
  editingQuoteId.value = q.quoteId
  editingQuoteText.value = q.quote
}

const cancelEditQuote = () => {
  editingQuoteId.value = null
  editingQuoteText.value = ''
}

const saveEditQuote = async (q: Quote) => {
  if (!selectedGuildId.value) {
    return
  }
  const text = editingQuoteText.value.trim()
  if (!text) {
    return
  }
  quoteSaving.value = true
  try {
    const updated = await api(`/api/admin/guilds/${selectedGuildId.value}/quotes/${q.quoteId}`, {
      method: 'PUT',
      body: { quote: text },
    }) as Quote
    quotes.value = quotes.value.map(x => x.quoteId === updated.quoteId ? updated : x)
    cancelEditQuote()
    toast.add({ severity: 'success', summary: 'Saved', detail: 'Quote updated.', life: 2500 })
  } catch (err: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: err?.data ?? 'Could not update quote.', life: 4000 })
  } finally {
    quoteSaving.value = false
  }
}

const deleteQuote = async (q: Quote) => {
  if (!selectedGuildId.value) {
    return
  }
  const ok = await confirmDelete({
    message: `Delete this quote?\n\n"${q.quote}"`,
  })
  if (!ok) {
    return
  }
  deletingQuoteId.value = q.quoteId
  try {
    await api(`/api/admin/guilds/${selectedGuildId.value}/quotes/${q.quoteId}`, {
      method: 'DELETE',
    })
    quotes.value = quotes.value.filter(x => x.quoteId !== q.quoteId)
    if (editingQuoteId.value === q.quoteId) {
      cancelEditQuote()
    }
    toast.add({ severity: 'success', summary: 'Deleted', detail: 'Quote removed.', life: 2500 })
  } catch (err: any) {
    toast.add({ severity: 'error', summary: 'Delete failed', detail: err?.data ?? 'Could not delete quote.', life: 4000 })
  } finally {
    deletingQuoteId.value = null
  }
}

const loadConfig = async (guildId: string) => {
  configPending.value = true
  try {
    const res = await api(`/api/admin/guilds/${guildId}/config`) as ConfigResponse
    channels.value = res.channels
    roles.value = res.roles
    for (const g of res.gw2GuildNames ?? []) cacheGuild(g)
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
  cancelEditQuote()
  newQuoteText.value = ''
  loadQuotes(id)
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

.gw2-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.gw2-current {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.gw2-current-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  min-height: 2rem;
}

.gw2-current-label {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
  font-weight: 500;
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  width: 5.5rem;
  flex-shrink: 0;
}

.gw2-current-value {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  align-items: center;
  flex: 1;
  min-width: 0;
}

.empty-hint {
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
  font-style: italic;
}

.section-label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  margin-bottom: 0.5rem;
}

.my-guilds {
  display: flex;
  flex-direction: column;
}

.my-guild-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.4rem 0;
  border-bottom: 1px solid var(--p-surface-border);
}

.my-guild-row:last-child {
  border-bottom: none;
}

.my-guild-name {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.my-guild-name > span:first-child {
  font-weight: 500;
  font-size: 0.9rem;
}

.my-guild-id {
  font-family: monospace;
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
  word-break: break-all;
}

.my-guild-tag {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
  font-weight: 400;
  margin-left: 0.25rem;
}

.my-guild-actions {
  display: flex;
  gap: 0.4rem;
  flex-shrink: 0;
}

.manual-entry summary {
  cursor: pointer;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
  user-select: none;
}

.manual-entry-body {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin-top: 0.5rem;
}

.search-results {
  margin-top: 0.75rem;
}

.quotes-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.quotes-list {
  display: flex;
  flex-direction: column;
}

.quote-row {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--p-surface-border);
}

.quote-row:last-child {
  border-bottom: none;
}

.quote-text {
  flex: 1;
  font-size: 0.9rem;
  white-space: pre-wrap;
  word-break: break-word;
}

.quote-actions {
  display: flex;
  gap: 0.4rem;
  flex-shrink: 0;
}

.quote-new {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.quote-new-actions {
  display: flex;
  justify-content: flex-end;
}
</style>
