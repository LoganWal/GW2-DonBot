<template>
  <div>
    <h1 class="page-title">Scheduled Events</h1>

    <ProgressSpinner v-if="guildsPending" style="width: 2rem; height: 2rem;" />

    <Card v-else-if="!guilds?.length">
      <template #content>
        <p style="margin: 0; color: var(--p-text-muted-color);">
          You don't have access to manage scheduled events in any server. Ask a server administrator to assign you a manager role on the admin page.
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

      <ProgressSpinner v-if="(contextPending || eventsPending) && selectedGuildId" style="width: 2rem; height: 2rem;" />

      <template v-else-if="context">
        <Card style="margin-bottom: 1.5rem;">
          <template #title>
            <div class="card-title-row">
              <span>Events</span>
              <Button label="New event" icon="pi pi-plus" size="small" @click="startCreate" />
            </div>
          </template>
          <template #content>
            <div v-if="!events.length" class="empty-hint">No scheduled events yet.</div>
            <DataTable
              v-else
              :value="eventsForTable"
              data-key="scheduledEventId"
              responsive-layout="scroll"
              sort-field="utcEventTime"
              :sort-order="1"
              removable-sort
            >
              <Column header="Type" sort-field="eventType" :sortable="true">
                <template #body="{ data }">{{ eventTypeLabel(data.eventType) }}</template>
              </Column>
              <Column header="Channel" sort-field="channelId" :sortable="true">
                <template #body="{ data }"># {{ channelName(data.channelId) }}</template>
              </Column>
              <Column header="Posts at" sort-field="postSortKey" :sortable="true">
                <template #body="{ data }">
                  {{ formatPostTime(data.day, data.hour) }}
                  <div class="muted-sub">Every {{ data.repeatIntervalDays }}d</div>
                </template>
              </Column>
              <Column header="Event time" sort-field="utcEventTime" :sortable="true">
                <template #body="{ data }">{{ formatNext(data.utcEventTime) }}</template>
              </Column>
              <Column header="Message" field="message" :sortable="true">
                <template #body="{ data }">
                  <span class="muted-sub" v-if="!data.message">-</span>
                  <span v-else>{{ data.message }}</span>
                </template>
              </Column>
              <Column header="" style="width: 140px;">
                <template #body="{ data }">
                  <div style="display: flex; gap: 0.4rem;">
                    <Button icon="pi pi-pencil" size="small" severity="secondary" outlined aria-label="Edit" @click="startEdit(data)" />
                    <Button icon="pi pi-trash" size="small" severity="danger" outlined aria-label="Delete" :loading="deletingId === data.scheduledEventId" @click="onDelete(data)" />
                  </div>
                </template>
              </Column>
            </DataTable>
          </template>
        </Card>
      </template>
    </template>

    <Dialog v-model:visible="dialogVisible" :header="editingId ? 'Edit scheduled event' : 'New scheduled event'" modal :style="{ width: '560px' }">
      <div v-if="form" class="form-grid">
        <div class="field">
          <label for="ev-type">Type</label>
          <Select
            id="ev-type"
            v-model="form.eventType"
            :options="eventTypeOptions"
            option-label="label"
            option-value="value"
            placeholder="Choose a type"
          />
        </div>
        <div class="field">
          <label for="ev-channel">Channel</label>
          <Select
            id="ev-channel"
            v-model="form.channelId"
            :options="context?.channels ?? []"
            option-label="name"
            option-value="id"
            placeholder="Choose a channel"
            filter
          />
        </div>
        <div class="field">
          <label>
            Post message at
            <i class="pi pi-info-circle field-info" v-tooltip.top="'When DonBot posts the signup message to your channel (weekly, in your local time).'" />
          </label>
          <div class="field-row-2">
            <Select
              v-model="form.postDay"
              :options="dayOptions"
              option-label="label"
              option-value="value"
              placeholder="Day"
            />
            <Select
              v-model="form.postHour"
              :options="hourOptions"
              option-label="label"
              option-value="value"
              placeholder="Hour"
            />
          </div>
        </div>
        <div class="field">
          <label for="ev-when">
            Event happens at
            <i class="pi pi-info-circle field-info" v-tooltip.top="'The timestamp embedded in the posted message so members know when the event itself happens.'" />
          </label>
          <DatePicker
            id="ev-when"
            v-model="form.localFireDate"
            show-time
            hour-format="24"
            :min-date="minPickerDate"
            show-icon
            fluid
            date-format="yy-mm-dd"
          />
        </div>
        <div class="field">
          <label for="ev-repeat">Repeat every (days)</label>
          <InputNumber id="ev-repeat" v-model="form.repeatIntervalDays" :min="1" :max="365" show-buttons fluid />
        </div>
        <div class="field">
          <label for="ev-message">Message (optional)</label>
          <Textarea
            id="ev-message"
            ref="messageTextareaRef"
            v-model="form.message"
            auto-resize
            rows="2"
            :maxlength="maxMessageLength"
          />
          <Select
            v-model="roleMentionPicker"
            :options="context?.roles ?? []"
            option-label="name"
            option-value="id"
            placeholder="Insert role mention…"
            filter
            show-clear
            style="margin-top: 0.4rem;"
            @change="onInsertRoleMention"
          />
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" :disabled="saving" @click="dialogVisible = false" />
        <Button :label="editingId ? 'Save' : 'Create'" icon="pi pi-check" :loading="saving" :disabled="!canSave" @click="save" />
      </template>
    </Dialog>
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
type Context = { guildId: string; guildName: string; channels: Channel[]; roles: Role[] }
type ScheduledEvent = {
  scheduledEventId: number
  eventType: number
  channelId: string
  day: number
  hour: number
  repeatIntervalDays: number
  message: string
  utcEventTime: string
}

type FormState = {
  eventType: number
  channelId: string | null
  postDay: number   // local day of week (0=Sun)
  postHour: number  // local hour 0-23
  localFireDate: Date | null
  repeatIntervalDays: number
  message: string
}

const maxMessageLength = 256

const eventTypeOptions = [
  { value: 0, label: 'Raid signup' },
  { value: 4, label: 'WvW raid signup' },
  { value: 1, label: 'WvW leaderboard' },
  { value: 2, label: 'PvE leaderboard' },
]

const eventTypeLabel = (t: number) => eventTypeOptions.find(o => o.value === t)?.label ?? `Type ${t}`

const minPickerDate = new Date()

const dayOptions = [
  { value: 0, label: 'Sunday' },
  { value: 1, label: 'Monday' },
  { value: 2, label: 'Tuesday' },
  { value: 3, label: 'Wednesday' },
  { value: 4, label: 'Thursday' },
  { value: 5, label: 'Friday' },
  { value: 6, label: 'Saturday' },
]

const hourOptions = Array.from({ length: 24 }, (_, h) => ({
  value: h,
  label: new Date(2024, 0, 1, h, 0).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' }),
}))

// Stored Day/Hour are UTC weekday/hour. Convert to local for editing and back.
const utcDayHourToLocal = (utcDay: number, utcHour: number): { day: number; hour: number } => {
  const now = new Date()
  const candidate = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), utcHour, 0, 0))
  while (candidate.getUTCDay() !== utcDay) {
    candidate.setUTCDate(candidate.getUTCDate() + 1)
  }
  return { day: candidate.getDay(), hour: candidate.getHours() }
}

const localDayHourToUtc = (localDay: number, localHour: number): { day: number; hour: number } => {
  const now = new Date()
  const candidate = new Date(now.getFullYear(), now.getMonth(), now.getDate(), localHour, 0, 0)
  while (candidate.getDay() !== localDay) {
    candidate.setDate(candidate.getDate() + 1)
  }
  return { day: candidate.getUTCDay(), hour: candidate.getUTCHours() }
}

const { selectedGuildId, ensureSelection } = useSelectedGuild()

const { data: guilds, pending: guildsPending } = await useAsyncData(
  'scheduling-guilds',
  () => api('/api/scheduling/guilds') as Promise<GuildSummary[]>
)

watch(guilds, (gs) => {
  if (!gs?.length) return
  ensureSelection(gs.map(g => g.guildId), gs[0]?.guildId ?? null)
}, { immediate: true })

const context = ref<Context | null>(null)
const events = ref<ScheduledEvent[]>([])
const contextPending = ref(false)
const eventsPending = ref(false)
const deletingId = ref<number | null>(null)

const channelName = (id: string) => context.value?.channels.find(c => c.id === id)?.name ?? id

// Sort key for "Posts at": minutes-from-Sunday in local time so the column sorts
// the way the user reads it.
const eventsForTable = computed(() => events.value.map(e => {
  const local = utcDayHourToLocal(e.day, e.hour)
  return { ...e, postSortKey: local.day * 1440 + local.hour * 60 }
}))

const loadContext = async (guildId: string) => {
  contextPending.value = true
  try {
    context.value = await api(`/api/scheduling/guilds/${guildId}/context`) as Context
  } catch {
    context.value = null
    toast.add({ severity: 'error', summary: 'Load failed', detail: 'Failed to load server context.', life: 4000 })
  } finally {
    contextPending.value = false
  }
}

const loadEvents = async (guildId: string) => {
  eventsPending.value = true
  try {
    events.value = await api(`/api/scheduling/guilds/${guildId}/events`) as ScheduledEvent[]
  } catch {
    events.value = []
    toast.add({ severity: 'error', summary: 'Load failed', detail: 'Failed to load scheduled events.', life: 4000 })
  } finally {
    eventsPending.value = false
  }
}

watch(selectedGuildId, async (id) => {
  if (!id) return
  await Promise.all([loadContext(id), loadEvents(id)])
}, { immediate: true })

const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const form = ref<FormState | null>(null)
const saving = ref(false)

const startCreate = () => {
  editingId.value = null
  // Default: next Monday at 19:00 local
  const def = new Date()
  def.setHours(19, 0, 0, 0)
  while (def.getDay() !== 1 || def.getTime() <= Date.now()) {
    def.setDate(def.getDate() + 1)
  }
  form.value = {
    eventType: 0,
    channelId: null,
    postDay: 1,
    postHour: 18,
    localFireDate: def,
    repeatIntervalDays: 7,
    message: '',
  }
  roleMentionPicker.value = null
  dialogVisible.value = true
}

const startEdit = (e: ScheduledEvent) => {
  editingId.value = e.scheduledEventId
  const post = utcDayHourToLocal(e.day, e.hour)
  form.value = {
    eventType: e.eventType,
    channelId: e.channelId,
    postDay: post.day,
    postHour: post.hour,
    localFireDate: new Date(e.utcEventTime),
    repeatIntervalDays: e.repeatIntervalDays,
    message: e.message ?? '',
  }
  roleMentionPicker.value = null
  dialogVisible.value = true
}

const canSave = computed(() => {
  if (!form.value) return false
  return !!form.value.channelId
    && !!form.value.localFireDate
    && form.value.localFireDate.getTime() > Date.now()
    && form.value.repeatIntervalDays >= 1 && form.value.repeatIntervalDays <= 365
})

const formatNext = (iso: string) => {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

const formatPostTime = (utcDay: number, utcHour: number) => {
  const { day, hour } = utcDayHourToLocal(utcDay, utcHour)
  const sample = new Date(2024, 0, 1, hour, 0)
  return `${dayOptions.find(o => o.value === day)?.label ?? day} ${sample.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })}`
}

const messageTextareaRef = ref<any>(null)
const roleMentionPicker = ref<string | null>(null)

const onInsertRoleMention = () => {
  const roleId = roleMentionPicker.value
  if (!roleId || !form.value) {
    roleMentionPicker.value = null
    return
  }
  const mention = `<@&${roleId}>`
  const textarea: HTMLTextAreaElement | null = messageTextareaRef.value?.$el ?? null
  const current = form.value.message ?? ''
  if (textarea && typeof textarea.selectionStart === 'number') {
    const start = textarea.selectionStart
    const end = textarea.selectionEnd ?? start
    const next = current.slice(0, start) + mention + current.slice(end)
    if (next.length > maxMessageLength) {
      roleMentionPicker.value = null
      return
    }
    form.value.message = next
    nextTick(() => {
      textarea.focus()
      const caret = start + mention.length
      textarea.setSelectionRange(caret, caret)
    })
  } else {
    const next = (current ? current + ' ' : '') + mention
    if (next.length <= maxMessageLength) {
      form.value.message = next
    }
  }
  roleMentionPicker.value = null
}

const save = async () => {
  if (!selectedGuildId.value || !form.value || !form.value.channelId || !form.value.localFireDate) return
  saving.value = true
  const { day: utcDay, hour: utcHour } = localDayHourToUtc(form.value.postDay, form.value.postHour)
  const body = {
    eventType: form.value.eventType,
    channelId: form.value.channelId,
    day: utcDay,
    hour: utcHour,
    utcEventTime: form.value.localFireDate.toISOString(),
    repeatIntervalDays: form.value.repeatIntervalDays,
    message: form.value.message?.trim() || null,
  }
  try {
    if (editingId.value) {
      const updated = await api(`/api/scheduling/guilds/${selectedGuildId.value}/events/${editingId.value}`, {
        method: 'PUT',
        body,
      }) as ScheduledEvent
      // Replaced row gets a new id (delete + insert on the server), so refetch.
      await loadEvents(selectedGuildId.value)
      toast.add({ severity: 'success', summary: 'Saved', detail: 'Scheduled event updated.', life: 2500 })
      void updated
    } else {
      const created = await api(`/api/scheduling/guilds/${selectedGuildId.value}/events`, {
        method: 'POST',
        body,
      }) as ScheduledEvent
      events.value = [...events.value, created]
      toast.add({ severity: 'success', summary: 'Created', detail: 'Scheduled event added.', life: 2500 })
    }
    dialogVisible.value = false
  } catch (err: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: err?.data ?? 'Could not save event.', life: 5000 })
  } finally {
    saving.value = false
  }
}

const onDelete = async (e: ScheduledEvent) => {
  if (!selectedGuildId.value) return
  const ok = await confirmDelete({
    message: `Delete the ${eventTypeLabel(e.eventType)} event in #${channelName(e.channelId)}?`,
  })
  if (!ok) return
  deletingId.value = e.scheduledEventId
  try {
    await api(`/api/scheduling/guilds/${selectedGuildId.value}/events/${e.scheduledEventId}`, { method: 'DELETE' })
    events.value = events.value.filter(x => x.scheduledEventId !== e.scheduledEventId)
    toast.add({ severity: 'success', summary: 'Deleted', detail: 'Scheduled event removed.', life: 2500 })
  } catch (err: any) {
    toast.add({ severity: 'error', summary: 'Delete failed', detail: err?.data ?? 'Could not delete event.', life: 5000 })
  } finally {
    deletingId.value = null
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

.card-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.empty-hint {
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
  font-style: italic;
}

.muted-sub {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
}

.form-grid {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
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

.field-row-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}

</style>
