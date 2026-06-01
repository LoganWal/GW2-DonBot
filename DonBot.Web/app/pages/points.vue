<template>
  <div>
    <div class="header-row">
      <h1 class="page-title" style="margin: 0;">Raffles & Points</h1>
      <Select
        v-model="selectedGuildId"
        :options="guildOptions ?? []"
        option-label="guildName"
        option-value="guildId"
        :placeholder="guildsPending ? 'Loading servers...' : 'Select a server'"
        :loading="guildsPending"
        :disabled="guildsPending"
        style="min-width: 260px;"
      />
      <Button icon="pi pi-refresh" severity="secondary" text aria-label="Refresh" :disabled="statePending || !selectedGuildId" @click="loadState" />
    </div>

    <template v-if="!selectedGuildId || (!guildsPending && !selectedGuildIsAvailable)">
      <Message severity="info" :closable="false">Select a server to view raffles and points.</Message>
    </template>

    <template v-else-if="statePending && !state">
      <ProgressSpinner />
    </template>

    <template v-else-if="state">
      <div class="stat-grid">
        <StatCard label="Available Points" :value="state.account?.availablePoints ?? 0" />
        <StatCard label="Total Earned" :value="state.account?.points ?? 0" />
      </div>

      <div class="action-strip">
        <Button
          label="Create Raffle"
          icon="pi pi-plus"
          class="accent-button"
          :disabled="!state.permissions.canCreateRaffle || hasRaffleType(0) || actionPending"
          @click="openCreate(0)"
        />
        <Button
          label="Create Event Raffle"
          icon="pi pi-calendar-plus"
          class="accent-button"
          :disabled="!state.permissions.canCreateEventRaffle || hasRaffleType(1) || actionPending"
          @click="openCreate(1)"
        />
        <Button
          v-if="canShowReopen(0)"
          label="Reopen Raffle"
          icon="pi pi-undo"
          severity="secondary"
          :disabled="!state.permissions.canReopenRaffle || hasRaffleType(0) || actionPending"
          @click="reopenRaffle(0)"
        />
        <Button
          v-if="canShowReopen(1)"
          label="Reopen Event"
          icon="pi pi-undo"
          severity="secondary"
          :disabled="!state.permissions.canReopenEventRaffle || hasRaffleType(1) || actionPending"
          @click="reopenRaffle(1)"
        />
      </div>

      <Message v-if="!state.raffles.length" severity="secondary" :closable="false">
        No active raffles for this server.
      </Message>

      <div v-else class="raffle-grid">
        <Card v-for="raffle in state.raffles" :key="raffle.id" class="raffle-card">
          <template #title>
            <div class="raffle-title">
              <span>{{ raffle.type }} Raffle</span>
              <Tag severity="success" value="ACTIVE" />
            </div>
          </template>
          <template #content>
            <div class="raffle-body">
              <p class="raffle-description">{{ raffle.description }}</p>

              <div class="mini-stats">
                <div>
                  <span>Your bid</span>
                  <strong>{{ formatPoints(raffle.userBid) }}</strong>
                </div>
                <div>
                  <span>Total spent</span>
                  <strong>{{ formatPoints(raffle.totalPoints) }}</strong>
                </div>
              </div>

              <div class="entry-row">
                <InputNumber
                  v-model="entryPoints[raffle.id]"
                  :min="1"
                  :max="Math.max(1, Math.floor(state.account?.availablePoints ?? 0))"
                  :input-id="`entry-points-${raffle.id}`"
                  show-buttons
                  button-layout="horizontal"
                  increment-button-icon="pi pi-plus"
                  decrement-button-icon="pi pi-minus"
                  style="width: 180px;"
                />
                <Button
                  label="Enter"
                  icon="pi pi-ticket"
                  class="accent-button"
                  :disabled="actionPending || !entryPoints[raffle.id] || !canEnter(raffle)"
                  @click="enterRaffle(raffle)"
                />
              </div>

              <div class="quick-row">
                <Button v-for="amount in quickAmounts" :key="amount" :label="`${amount}`" size="small" severity="secondary" outlined @click="entryPoints[raffle.id] = amount" />
                <Button label="Max" size="small" severity="secondary" outlined @click="entryPoints[raffle.id] = Math.max(1, Math.floor(state.account?.availablePoints ?? 0))" />
              </div>

              <div class="top-bidders">
                <div class="section-label">Top 5</div>
                <div v-if="!raffle.topBidders.length" class="empty-line">No bids yet.</div>
                <div v-for="(bidder, index) in raffle.topBidders" :key="bidder.discordId" class="bid-row">
                  <span class="bid-rank">#{{ index + 1 }}</span>
                  <span class="bid-name">{{ bidder.displayName }}</span>
                  <strong>{{ formatPoints(bidder.pointsSpent) }}</strong>
                </div>
              </div>

              <div class="raffle-actions">
                <Button
                  v-if="raffle.raffleType === 0"
                  label="Complete"
                  icon="pi pi-check"
                  severity="success"
                  :disabled="!state.permissions.canCompleteRaffle || actionPending"
                  @click="completeRaffle(raffle, 1)"
                />
                <template v-else>
                  <InputNumber v-model="eventWinnerCounts[raffle.id]" :min="1" show-buttons style="width: 150px;" />
                  <Button
                    label="Complete Event"
                    icon="pi pi-check"
                    severity="success"
                    :disabled="!state.permissions.canCompleteEventRaffle || actionPending"
                    @click="completeRaffle(raffle, eventWinnerCounts[raffle.id] ?? 1)"
                  />
                </template>
              </div>

              <div v-if="raffle.canEdit" class="edit-panel">
                <Textarea v-model="editMessages[raffle.id]" rows="4" auto-resize style="width: 100%;" />
                <Button label="Update Discord Message" icon="pi pi-save" severity="secondary" :disabled="actionPending" @click="updateRaffle(raffle)" />
              </div>
            </div>
          </template>
        </Card>
      </div>
    </template>

    <Dialog v-model:visible="createVisible" modal :header="createType === 1 ? 'Create Event Raffle' : 'Create Raffle'" style="width: min(560px, 92vw);">
      <div class="dialog-body">
        <Textarea v-model="createDescription" rows="5" auto-resize style="width: 100%;" />
        <div class="dialog-actions">
          <Button label="Cancel" severity="secondary" outlined @click="createVisible = false" />
          <Button label="Create" icon="pi pi-send" :disabled="!createDescription.trim() || actionPending" @click="createRaffle" />
        </div>
      </div>
    </Dialog>

    <Dialog v-model:visible="winnerVisible" modal :closable="winnerCountdown <= 0" class="winner-dialog" style="width: min(620px, 92vw);">
      <div v-if="winnerCountdown > 0" class="countdown-panel">
        <span>Drawing in</span>
        <strong>{{ winnerCountdown }}</strong>
      </div>
      <div v-else-if="winnerEvent" class="winner-panel">
        <div class="confetti">
          <span v-for="i in 28" :key="i" :style="confettiStyle(i)" />
        </div>
        <h2>{{ winnerEvent.type }} Raffle Winners</h2>
        <p>{{ winnerEvent.description }}</p>
        <div class="winner-list">
          <div v-for="(winner, index) in winnerEvent.winners" :key="winner.discordId" class="winner-row">
            <span>#{{ index + 1 }}</span>
            <strong>{{ winner.displayName }}</strong>
            <em>{{ formatPoints(winner.pointsSpent) }} points</em>
          </div>
        </div>
        <Button label="Close" icon="pi pi-times" severity="secondary" @click="winnerVisible = false" />
      </div>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

usePageTitle()

type GuildOption = { guildId: string; guildName: string }
type RaffleBid = { discordId: string; displayName: string; pointsSpent: number }
type Raffle = {
  id: number
  raffleType: number
  type: string
  description: string
  isActive: boolean
  canEdit: boolean
  userBid: number
  totalPoints: number
  topBidders: RaffleBid[]
}
type RaffleState = {
  guildId: string
  guildName: string
  account: { points: number; availablePoints: number } | null
  raffles: Raffle[]
  permissions: {
    canEnterRaffle: boolean
    canEnterEventRaffle: boolean
    canCreateRaffle: boolean
    canCreateEventRaffle: boolean
    canCompleteRaffle: boolean
    canCompleteEventRaffle: boolean
    canReopenRaffle: boolean
    canReopenEventRaffle: boolean
  }
  availability: {
    hasPreviousRaffle: boolean
    hasPreviousEventRaffle: boolean
  }
}
type WinnerEvent = {
  raffleId: number
  raffleType: number
  type: string
  description: string
  winners: RaffleBid[]
}

const api = useApi()
const apiBase = useRuntimeConfig().public.apiBase as string
const toast = useToast()
const { selectedGuildId, ensureSelection } = useSelectedGuild()

const { data: guildOptions, pending: guildsPending } = useLazyAsyncData(
  'raffle-guilds',
  () => api('/api/raffles/guilds') as Promise<GuildOption[]>,
  { default: () => [] }
)

const availableGuildIds = computed(() => (guildOptions.value ?? []).map(g => g.guildId))
const selectedGuildIsAvailable = computed(() => !!selectedGuildId.value && availableGuildIds.value.includes(selectedGuildId.value))

watch(guildOptions, (opts) => {
  if (!opts?.length) return
  ensureSelection(availableGuildIds.value, opts[0]?.guildId ?? null)
}, { immediate: true })

const state = ref<RaffleState | null>(null)
const statePending = ref(false)
const actionPending = ref(false)
const entryPoints = reactive<Record<number, number>>({})
const editMessages = reactive<Record<number, string>>({})
const eventWinnerCounts = reactive<Record<number, number>>({})
const quickAmounts = [1, 50, 100, 1000]

let source: EventSource | null = null
let countdownTimer: ReturnType<typeof setInterval> | null = null

const createVisible = ref(false)
const createType = ref(0)
const createDescription = ref('')
const winnerVisible = ref(false)
const winnerCountdown = ref(0)
const winnerEvent = ref<WinnerEvent | null>(null)

const loadState = async () => {
  if (!selectedGuildId.value) {
    state.value = null
    return
  }
  if (!availableGuildIds.value.includes(selectedGuildId.value)) {
    state.value = null
    return
  }
  statePending.value = true
  try {
    applyState(await api(`/api/raffles/${selectedGuildId.value}`) as RaffleState)
  } catch (e: any) {
    showErrorToast('Load failed', apiErrorMessage(e, 'Failed to load raffles.'))
  } finally {
    statePending.value = false
  }
}

const applyState = (next: RaffleState) => {
  state.value = next
  for (const raffle of next.raffles) {
    entryPoints[raffle.id] ??= 1
    editMessages[raffle.id] = editMessages[raffle.id] ?? raffle.description
    eventWinnerCounts[raffle.id] ??= 1
  }
}

const closeStream = () => {
  if (source) {
    source.close()
    source = null
  }
}

const openStream = () => {
  closeStream()
  if (!selectedGuildId.value || typeof EventSource === 'undefined') {
    return
  }
  source = new EventSource(`${apiBase}/api/raffles/${selectedGuildId.value}/stream`, { withCredentials: true })
  source.addEventListener('state', (e: MessageEvent) => {
    try {
      applyState(JSON.parse(e.data) as RaffleState)
    } catch (err) {
      console.warn('raffles: failed to parse state event', err)
    }
  })
  source.addEventListener('completed', (e: MessageEvent) => {
    try {
      startWinnerCountdown(JSON.parse(e.data) as WinnerEvent)
    } catch (err) {
      console.warn('raffles: failed to parse completed event', err)
    }
  })
  source.onerror = () => {}
}

watch([selectedGuildId, guildOptions, guildsPending], async ([id, opts, pending]) => {
  closeStream()
  state.value = null
  if (!id || pending || !opts?.some(g => g.guildId === id)) {
    return
  }
  await loadState()
  openStream()
}, { immediate: true })

onUnmounted(() => {
  closeStream()
  if (countdownTimer) {
    clearInterval(countdownTimer)
  }
})

const hasRaffleType = (type: number) => state.value?.raffles.some(r => r.raffleType === type) ?? false

const canShowReopen = (type: number) => {
  if (!state.value || hasRaffleType(type)) {
    return false
  }
  return type === 1
    ? state.value.availability.hasPreviousEventRaffle
    : state.value.availability.hasPreviousRaffle
}

const canEnter = (raffle: Raffle) => {
  if (!state.value) {
    return false
  }
  return raffle.raffleType === 1
    ? state.value.permissions.canEnterEventRaffle
    : state.value.permissions.canEnterRaffle
}

const openCreate = (type: number) => {
  createType.value = type
  createDescription.value = ''
  createVisible.value = true
}

const createRaffle = async () => {
  await runAction(async () => {
    await api(`/api/raffles/${selectedGuildId.value}/create`, {
      method: 'POST',
      body: { raffleType: createType.value, description: createDescription.value },
    })
    createVisible.value = false
    await loadState()
    toast.add({
      severity: 'success',
      summary: 'Created',
      detail: createType.value === 1 ? 'Event raffle created.' : 'Raffle created.',
      life: 2500,
    })
  }, 'Failed to create raffle.')
}

const reopenRaffle = async (raffleType: number) => {
  await runAction(async () => {
    await api(`/api/raffles/${selectedGuildId.value}/reopen`, {
      method: 'POST',
      body: { raffleType },
    })
    await loadState()
    toast.add({
      severity: 'success',
      summary: 'Reopened',
      detail: raffleType === 1 ? 'Event raffle reopened.' : 'Raffle reopened.',
      life: 2500,
    })
  }, 'Failed to reopen raffle.')
}

const enterRaffle = async (raffle: Raffle) => {
  await runAction(async () => {
    await api(`/api/raffles/${selectedGuildId.value}/enter`, {
      method: 'POST',
      body: { raffleId: raffle.id, points: entryPoints[raffle.id] ?? 1 },
    })
    await loadState()
    toast.add({ severity: 'success', summary: 'Entered', detail: 'Points added to the raffle.', life: 2500 })
  }, 'Failed to enter raffle.')
}

const completeRaffle = async (raffle: Raffle, winnersCount: number) => {
  await runAction(async () => {
    await api(`/api/raffles/${selectedGuildId.value}/complete`, {
      method: 'POST',
      body: { raffleType: raffle.raffleType, winnersCount },
    })
    await loadState()
    toast.add({ severity: 'success', summary: 'Completed', detail: 'Raffle completed.', life: 2500 })
  }, 'Failed to complete raffle.')
}

const updateRaffle = async (raffle: Raffle) => {
  await runAction(async () => {
    await api(`/api/raffles/${selectedGuildId.value}/${raffle.id}`, {
      method: 'PUT',
      body: { raffleType: raffle.raffleType, description: editMessages[raffle.id] },
    })
    await loadState()
    toast.add({ severity: 'success', summary: 'Updated', detail: 'Discord message updated.', life: 2500 })
  }, 'Failed to update raffle message.')
}

const runAction = async (fn: () => Promise<void>, fallback: string) => {
  if (!selectedGuildId.value) return
  actionPending.value = true
  try {
    await fn()
  } catch (e: any) {
    showErrorToast('Action failed', apiErrorMessage(e, fallback))
  } finally {
    actionPending.value = false
  }
}

function showErrorToast(summary: string, detail: string) {
  toast.add({ severity: 'error', summary, detail, life: 5000 })
}

function apiErrorMessage(e: any, fallback: string) {
  if (e?.data?.error) {
    return e.data.error
  }
  if (e?.statusCode === 403 || e?.response?.status === 403) {
    return 'You do not have access to that action for this server.'
  }
  return e?.message ?? fallback
}

const startWinnerCountdown = (event: WinnerEvent) => {
  winnerEvent.value = event
  winnerCountdown.value = 5
  winnerVisible.value = true
  if (countdownTimer) {
    clearInterval(countdownTimer)
  }
  countdownTimer = setInterval(() => {
    winnerCountdown.value--
    if (winnerCountdown.value <= 0 && countdownTimer) {
      clearInterval(countdownTimer)
      countdownTimer = null
    }
  }, 1000)
}

const formatPoints = (value: number) => Math.floor(value).toLocaleString()

const confettiStyle = (i: number) => ({
  left: `${(i * 37) % 100}%`,
  animationDelay: `${(i % 8) * 0.12}s`,
  background: ['#3b82f6', '#22c55e', '#f59e0b', '#ef4444', '#a855f7'][i % 5],
})
</script>

<style scoped>
.header-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 1.5rem;
}

:global(.p-toast) {
  z-index: 2400 !important;
}

.accent-button:not(:disabled) {
  background: var(--p-primary-color);
  border-color: var(--p-primary-color);
  color: var(--p-primary-contrast-color, #ffffff);
}

.accent-button:not(:disabled):hover {
  background: color-mix(in srgb, var(--p-primary-color) 88%, black);
  border-color: color-mix(in srgb, var(--p-primary-color) 88%, black);
}

.action-strip {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}

.raffle-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(340px, 1fr));
  gap: 1rem;
}

.raffle-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.raffle-body {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.raffle-description {
  white-space: pre-wrap;
  color: var(--p-text-color);
}

.mini-stats {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.mini-stats > div {
  border: 1px solid var(--p-surface-border);
  border-radius: 8px;
  padding: 0.75rem;
  background: var(--p-surface-ground);
}

.mini-stats span,
.section-label,
.empty-line {
  display: block;
  font-size: 0.78rem;
  color: var(--p-text-muted-color);
}

.mini-stats strong {
  display: block;
  font-size: 1.35rem;
}

.entry-row,
.quick-row,
.raffle-actions,
.dialog-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.top-bidders {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.bid-row,
.winner-row {
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 0.75rem;
  align-items: center;
  border-bottom: 1px solid var(--p-surface-border);
  padding-bottom: 0.45rem;
}

.bid-rank {
  color: var(--p-text-muted-color);
  font-size: 0.8rem;
  width: 2.25rem;
}

.bid-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.edit-panel,
.dialog-body,
.winner-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.countdown-panel {
  display: grid;
  place-items: center;
  min-height: 260px;
  gap: 0.5rem;
}

.countdown-panel span {
  color: var(--p-text-muted-color);
  font-size: 1rem;
}

.countdown-panel strong {
  font-size: 6rem;
  line-height: 1;
}

.winner-panel {
  position: relative;
  align-items: center;
  text-align: center;
  overflow: hidden;
  padding: 1rem 0;
}

.winner-list {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.winner-row {
  grid-template-columns: auto 1fr auto;
  text-align: left;
  padding: 0.65rem 0;
}

.winner-row em {
  font-style: normal;
  color: var(--p-text-muted-color);
}

.confetti {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
}

.confetti span {
  position: absolute;
  top: -20px;
  width: 8px;
  height: 14px;
  border-radius: 2px;
  animation: fall 2.6s linear infinite;
}

@keyframes fall {
  0% {
    transform: translateY(-20px) rotate(0deg);
    opacity: 1;
  }
  100% {
    transform: translateY(360px) rotate(500deg);
    opacity: 0;
  }
}

@media (max-width: 700px) {
  .raffle-grid {
    grid-template-columns: 1fr;
  }

  .mini-stats {
    grid-template-columns: 1fr;
  }
}
</style>
