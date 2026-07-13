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
            <ServerSelect
              input-id="guild-select"
              v-model="selectedGuildId"
              :options="guilds"
              option-label="name"
              placeholder="Select a server"
              :select-style="{ width: 'min(320px, 100%)' }"
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
              <Column header="Responses">
                <template #body="{ data }">
                  <div v-if="isSignupEvent(data.eventType)" class="response-chip-list">
                    <Tag
                      v-for="(option, index) in data.responseOptions"
                      :key="`${data.scheduledEventId}-${index}`"
                      :value="`${option.emoji} ${option.label}`"
                      :icon="option.allowedRoleIds?.length ? 'pi pi-lock' : undefined"
                      :title="responseRoleSummary(option)"
                      severity="secondary"
                    />
                  </div>
                  <span v-else class="muted-sub">-</span>
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

    <Dialog v-model:visible="dialogVisible" :header="editingId ? 'Edit scheduled event' : 'New scheduled event'" modal :style="{ width: '720px', maxWidth: '94vw' }">
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
          <InputNumber id="ev-repeat" v-model="form.repeatIntervalDays" :min="minRepeatIntervalDays" :max="maxRepeatIntervalDays" show-buttons fluid />
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
        <div v-if="isSignupEvent(form.eventType)" class="field response-field">
          <div class="response-options-title">
            <label>Response buttons</label>
            <Button
              label="Add"
              icon="pi pi-plus"
              size="small"
              severity="secondary"
              outlined
              :disabled="form.responseOptions.length >= maxResponseOptions"
              @click="addResponseOption"
            />
          </div>
          <div class="response-option-list">
            <div v-for="(option, index) in form.responseOptions" :key="index" class="response-option-item">
              <div class="response-option-row">
                <InputText
                  v-model="option.emoji"
                  class="response-emoji-input"
                  :maxlength="maxResponseEmojiLength"
                  :aria-label="`Emoji for response ${index + 1}`"
                  :invalid="!!option.emoji.trim() && !isValidResponseEmoji(option.emoji)"
                />
                <Button
                  icon="pi pi-face-smile"
                  size="small"
                  severity="secondary"
                  outlined
                  :aria-label="`Pick emoji for response ${index + 1}`"
                  @click="openEmojiPicker($event, index)"
                />
                <InputText
                  v-model="option.label"
                  class="response-label-input"
                  :maxlength="maxResponseLabelLength"
                  :aria-label="`Label for response ${index + 1}`"
                />
                <Button
                  icon="pi pi-trash"
                  size="small"
                  severity="danger"
                  outlined
                  :disabled="form.responseOptions.length <= 1"
                  :aria-label="`Remove response ${index + 1}`"
                  @click="removeResponseOption(index)"
                />
                <label class="notify-toggle" :for="`notify-response-${index}`">
                  <ToggleSwitch v-model="option.notify" :input-id="`notify-response-${index}`" />
                  <span>Notify</span>
                </label>
              </div>
              <small v-if="!!option.emoji.trim() && !isValidResponseEmoji(option.emoji)" class="field-error">
                Use a Unicode emoji or a server emoji like &lt;:name:id&gt;.
              </small>
              <div class="response-role-restriction">
                <label class="role-limit-toggle" :for="`limit-response-${index}`">
                  <ToggleSwitch v-model="option.restrictToRoles" :input-id="`limit-response-${index}`" />
                  <span>Limit to roles</span>
                </label>
                <MultiSelect
                  v-if="option.restrictToRoles"
                  v-model="option.allowedRoleIds"
                  :options="context?.roles ?? []"
                  option-label="name"
                  option-value="id"
                  placeholder="Choose allowed roles"
                  display="chip"
                  filter
                  class="response-role-select"
                  :invalid="option.allowedRoleIds.length === 0"
                />
                <small v-if="option.restrictToRoles && option.allowedRoleIds.length === 0" class="field-error">
                  Choose at least one role, or turn off the restriction to allow anyone.
                </small>
              </div>
            </div>
          </div>
          <div class="muted-sub">{{ form.responseOptions.length }}/{{ maxResponseOptions }} buttons</div>
          <Divider />
          <div class="field notification-time-field">
            <label for="ev-notify-before">Notify before event (minutes)</label>
            <InputNumber
              id="ev-notify-before"
              v-model="form.notificationMinutesBeforeStart"
              :min="minNotificationMinutesBeforeStart"
              :max="maxNotificationMinutesBeforeStart"
              show-buttons
              fluid
            />
          </div>
        </div>
        <Popover ref="emojiPopoverRef">
          <div class="emoji-picker-grid">
            <Button
              v-for="emoji in commonResponseEmojis"
              :key="emoji"
              :label="emoji"
              text
              rounded
              :aria-label="`Insert ${emoji}`"
              @click="insertResponseEmoji(emoji)"
            />
          </div>
        </Popover>
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
type ResponseOption = { label: string; emoji: string; notify?: boolean; allowedRoleIds?: string[] }
type FormResponseOption = {
  label: string
  emoji: string
  notify: boolean
  allowedRoleIds: string[]
  restrictToRoles: boolean
}
type ScheduledEventTypeMetadata = { eventType: number; name: string; supportsResponseOptions: boolean }
type SchedulingMetadata = {
  maxMessageLength: number
  minRepeatIntervalDays: number
  maxRepeatIntervalDays: number
  defaultNotificationMinutesBeforeStart: number
  minNotificationMinutesBeforeStart: number
  maxNotificationMinutesBeforeStart: number
  maxResponseOptions: number
  maxResponseOptionLabelLength: number
  maxResponseOptionEmojiLength: number
  eventTypes: ScheduledEventTypeMetadata[]
  defaultSignupResponseOptions: ResponseOption[]
}
type Context = {
  guildId: string
  guildName: string
  channels: Channel[]
  roles: Role[]
  defaultSignupResponseOptions: ResponseOption[]
  scheduling?: SchedulingMetadata
}
type ScheduledEvent = {
  scheduledEventId: number
  eventType: number
  channelId: string
  day: number
  hour: number
  repeatIntervalDays: number
  message: string
  responseOptions: ResponseOption[]
  utcEventTime: string
  notificationMinutesBeforeStart: number
}

type FormState = {
  eventType: number
  channelId: string | null
  postDay: number   // local day of week (0=Sun)
  postHour: number  // local hour 0-23
  localFireDate: Date | null
  repeatIntervalDays: number
  message: string
  responseOptions: FormResponseOption[]
  notificationMinutesBeforeStart: number
}

const schedulingMetadata = computed(() => context.value?.scheduling)
const maxMessageLength = computed(() => schedulingMetadata.value?.maxMessageLength ?? 256)
const minRepeatIntervalDays = computed(() => schedulingMetadata.value?.minRepeatIntervalDays ?? 1)
const maxRepeatIntervalDays = computed(() => schedulingMetadata.value?.maxRepeatIntervalDays ?? 365)
const maxResponseOptions = computed(() => schedulingMetadata.value?.maxResponseOptions ?? 10)
const maxResponseLabelLength = computed(() => schedulingMetadata.value?.maxResponseOptionLabelLength ?? 80)
const maxResponseEmojiLength = computed(() => schedulingMetadata.value?.maxResponseOptionEmojiLength ?? 64)
const defaultNotificationMinutesBeforeStart = computed(() => schedulingMetadata.value?.defaultNotificationMinutesBeforeStart ?? 15)
const minNotificationMinutesBeforeStart = computed(() => schedulingMetadata.value?.minNotificationMinutesBeforeStart ?? 1)
const maxNotificationMinutesBeforeStart = computed(() => schedulingMetadata.value?.maxNotificationMinutesBeforeStart ?? 10080)

const commonResponseEmojis = [
  '✅', '❌', '🛠️', '⏰', '⚔️', '🛡️', '💚', '💛', '🔥', '❓',
  '👍', '👎', '🙌', '👀', '🎯', '📣', '⭐', '🌙', '☀️', '🍕',
]

const fallbackEventTypeOptions = [
  { value: 0, label: 'Raid signup', supportsResponseOptions: true },
  { value: 1, label: 'WvW leaderboard', supportsResponseOptions: false },
  { value: 2, label: 'PvE leaderboard', supportsResponseOptions: false },
]

const eventTypeOptions = computed(() =>
  schedulingMetadata.value?.eventTypes?.length
    ? schedulingMetadata.value.eventTypes.map(type => ({
        value: type.eventType,
        label: type.name.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, c => c.toUpperCase()),
        supportsResponseOptions: type.supportsResponseOptions,
      }))
    : fallbackEventTypeOptions
)

const eventTypeLabel = (t: number) => eventTypeOptions.value.find(o => o.value === t)?.label ?? `Type ${t}`

const isSignupEvent = (eventType: number) =>
  eventTypeOptions.value.find(option => option.value === eventType)?.supportsResponseOptions ?? eventType === 0

const defaultResponseOptions = (): FormResponseOption[] => {
  const defaults = schedulingMetadata.value?.defaultSignupResponseOptions?.length
    ? schedulingMetadata.value.defaultSignupResponseOptions
    : context.value?.defaultSignupResponseOptions
  if (defaults?.length) {
    return normalizeResponseOptions(defaults)
  }

  return normalizeResponseOptions([
    { label: 'Join', emoji: '✅' },
    { label: "Can't Join", emoji: '❌' },
    { label: 'Can Fill', emoji: '🛠️' },
  ])
}

const normalizeResponseOptions = (options: Array<ResponseOption | FormResponseOption>): FormResponseOption[] => options
  .slice(0, maxResponseOptions.value)
  .map(o => {
    const allowedRoleIds = [...new Set((o.allowedRoleIds ?? []).map(roleId => roleId.trim()).filter(Boolean))]
    return {
      label: o.label.trim(),
      emoji: o.emoji.trim(),
      notify: !!o.notify,
      allowedRoleIds,
      restrictToRoles: 'restrictToRoles' in o ? o.restrictToRoles : allowedRoleIds.length > 0,
    }
  })

const responseOptionsMatch = (left: Array<ResponseOption | FormResponseOption>, right: Array<ResponseOption | FormResponseOption>) => {
  const a = normalizeResponseOptions(left)
  const b = normalizeResponseOptions(right)
  return a.length === b.length && a.every((option, index) =>
    option.label === b[index]?.label
    && option.emoji === b[index]?.emoji
    && option.notify === !!b[index]?.notify
    && option.restrictToRoles === b[index]?.restrictToRoles
    && option.allowedRoleIds.length === b[index]?.allowedRoleIds.length
    && option.allowedRoleIds.every(roleId => b[index]?.allowedRoleIds.includes(roleId)))
}

const hasDuplicateResponseOptions = (options: Array<ResponseOption | FormResponseOption>) => {
  const keys = normalizeResponseOptions(options)
    .filter(o => o.label.length > 0 && o.emoji.length > 0)
    .map(o => `${o.emoji} ${o.label}`.toLocaleLowerCase())
  return new Set(keys).size !== keys.length
}

const customEmojiPattern = /^<a?:[A-Za-z0-9_]{2,32}:\d{17,20}>$/
const unicodeEmojiPattern = /^\p{Extended_Pictographic}(?:[\uFE0E\uFE0F]|\p{Emoji_Modifier}|\u200D\p{Extended_Pictographic})*$/u

const isValidResponseEmoji = (emoji: string) => {
  const value = emoji.trim()
  return customEmojiPattern.test(value) || unicodeEmojiPattern.test(value)
}

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

// Convert stored UTC weekday and hour to local editing values.
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

const { selectedGuildId, ensureSelection } = useGuildSelection()

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

const responseRoleSummary = (option: ResponseOption) => {
  if (!option.allowedRoleIds?.length) return 'Anyone can use this response'
  const roleNames = option.allowedRoleIds.map(id => context.value?.roles.find(role => role.id === id)?.name ?? id)
  return `Limited to: ${roleNames.join(', ')}`
}

// Sort "Posts at" by local weekday and hour.
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
    responseOptions: defaultResponseOptions(),
    notificationMinutesBeforeStart: defaultNotificationMinutesBeforeStart.value,
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
    responseOptions: e.responseOptions?.length ? normalizeResponseOptions(e.responseOptions) : defaultResponseOptions(),
    notificationMinutesBeforeStart: e.notificationMinutesBeforeStart || defaultNotificationMinutesBeforeStart.value,
  }
  roleMentionPicker.value = null
  dialogVisible.value = true
}

const canSave = computed(() => {
  if (!form.value) return false
  const normalizedResponseOptions = normalizeResponseOptions(form.value.responseOptions)
  const responseOptionsValid = !isSignupEvent(form.value.eventType)
    || (normalizedResponseOptions.length >= 1
      && normalizedResponseOptions.length <= maxResponseOptions.value
      && normalizedResponseOptions.every(o =>
        o.label.trim().length > 0
        && o.label.trim().length <= maxResponseLabelLength.value
        && o.emoji.trim().length > 0
        && o.emoji.trim().length <= maxResponseEmojiLength.value
        && isValidResponseEmoji(o.emoji)
        && (!o.restrictToRoles || o.allowedRoleIds.length > 0))
      && !hasDuplicateResponseOptions(normalizedResponseOptions))

  return !!form.value.channelId
    && !!form.value.localFireDate
    && form.value.localFireDate.getTime() > Date.now()
    && form.value.repeatIntervalDays >= minRepeatIntervalDays.value
    && form.value.repeatIntervalDays <= maxRepeatIntervalDays.value
    && form.value.notificationMinutesBeforeStart >= minNotificationMinutesBeforeStart.value
    && form.value.notificationMinutesBeforeStart <= maxNotificationMinutesBeforeStart.value
    && responseOptionsValid
})

watch(() => form.value?.eventType, (newType, oldType) => {
  if (!form.value || newType === undefined || oldType === undefined) return
  if (!isSignupEvent(newType)) return

  const shouldReplace = !form.value.responseOptions.length
    || (isSignupEvent(oldType) && responseOptionsMatch(form.value.responseOptions, defaultResponseOptions()))

  if (shouldReplace) {
    form.value.responseOptions = defaultResponseOptions()
  }
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
const emojiPopoverRef = ref<any>(null)
const emojiTargetIndex = ref<number | null>(null)

const addResponseOption = () => {
  if (!form.value || form.value.responseOptions.length >= maxResponseOptions.value) return
  form.value.responseOptions.push({
    label: '',
    emoji: '✅',
    notify: false,
    allowedRoleIds: [],
    restrictToRoles: false,
  })
}

const removeResponseOption = (index: number) => {
  if (!form.value || form.value.responseOptions.length <= 1) return
  form.value.responseOptions.splice(index, 1)
}

const openEmojiPicker = (event: MouseEvent, index: number) => {
  emojiTargetIndex.value = index
  emojiPopoverRef.value?.show(event)
}

const insertResponseEmoji = (emoji: string) => {
  if (form.value && emojiTargetIndex.value !== null && form.value.responseOptions[emojiTargetIndex.value]) {
    form.value.responseOptions[emojiTargetIndex.value].emoji = emoji
  }
  emojiPopoverRef.value?.hide()
}

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
    if (next.length > maxMessageLength.value) {
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
    if (next.length <= maxMessageLength.value) {
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
    responseOptions: isSignupEvent(form.value.eventType)
      ? normalizeResponseOptions(form.value.responseOptions).map(option => ({
          label: option.label,
          emoji: option.emoji,
          notify: option.notify,
          allowedRoleIds: option.restrictToRoles ? option.allowedRoleIds : [],
        }))
      : [],
    notificationMinutesBeforeStart: form.value.notificationMinutesBeforeStart,
  }
  try {
    if (editingId.value) {
      await api(`/api/scheduling/guilds/${selectedGuildId.value}/events/${editingId.value}`, {
        method: 'PUT',
        body,
      })
      await loadEvents(selectedGuildId.value)
      toast.add({ severity: 'success', summary: 'Saved', detail: 'Scheduled event updated.', life: 2500 })
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

.response-chip-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  max-width: 320px;
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

.response-field {
  gap: 0.5rem;
}

.response-options-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.response-option-list {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.response-option-item {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.65rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-border-radius-md);
}

.response-option-row {
  display: grid;
  grid-template-columns: 4.5rem 2.5rem minmax(0, 1fr) 2.5rem auto;
  gap: 0.45rem;
  align-items: center;
}

.response-emoji-input {
  text-align: center;
}

.response-label-input {
  min-width: 0;
}

.notify-toggle {
  justify-content: center;
  white-space: nowrap;
}

.response-role-restriction {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.role-limit-toggle {
  align-self: flex-start;
}

.response-role-select {
  width: 100%;
}

.notification-time-field {
  max-width: 18rem;
}

.emoji-picker-grid {
  display: grid;
  grid-template-columns: repeat(5, 2.4rem);
  gap: 0.2rem;
}

.field-error {
  color: var(--p-red-500);
  font-size: 0.75rem;
}

@media (max-width: 640px) {
  .guild-picker,
  .response-options-title {
    align-items: stretch;
    flex-direction: column;
  }

  .response-option-row {
    grid-template-columns: 4.5rem 2.5rem 1fr 2.5rem;
  }

  .notify-toggle {
    grid-column: 1 / -1;
    justify-content: flex-start;
  }
}

</style>
