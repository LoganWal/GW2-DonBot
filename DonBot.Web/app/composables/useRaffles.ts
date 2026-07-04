import type { Ref } from 'vue'
import type { GuildOption, Raffle, RaffleState, RaffleType, WinnerEvent } from '~/types/api'

export const RaffleTypes = {
  Standard: 0,
  Event: 1,
} as const satisfies Record<string, RaffleType>

export const raffleHistoryPanes: { type: RaffleType; label: string }[] = [
  { type: RaffleTypes.Standard, label: 'Raffle' },
  { type: RaffleTypes.Event, label: 'Event Raffle' },
]

export const raffleTypeLabel = (type: RaffleType) =>
  type === RaffleTypes.Event ? 'Event raffle' : 'Raffle'

export const isEventRaffle = (type: RaffleType) =>
  type === RaffleTypes.Event

export const useRaffles = (options: {
  selectedGuildId: Ref<string | null>
  guildOptions: Ref<GuildOption[] | null | undefined>
  guildsPending: Ref<boolean>
  refreshPointHistory: () => Promise<unknown>
  onWinnerEvent: (event: WinnerEvent) => void
}) => {
  const api = useApi()
  const apiBase = useRuntimeConfig().public.apiBase as string
  const toast = useToast()
  const { openEventSource } = useEventSource()
  const { pending: actionPending, runAction } = useApiAction(message => {
    toast.add({ severity: 'error', summary: 'Action failed', detail: message, life: 5000 })
  })

  const state = ref<RaffleState | null>(null)
  const statePending = ref(false)
  const entryPoints = reactive<Record<number, number>>({})
  const editMessages = reactive<Record<number, string>>({})
  const eventWinnerCounts = reactive<Record<number, number>>({})
  const quickAmounts = [1, 50, 100, 1000]
  let stream: { close: () => void } | null = null

  const availableGuildIds = computed(() => (options.guildOptions.value ?? []).map(g => g.guildId))
  const selectedGuildIsAvailable = computed(() =>
    !!options.selectedGuildId.value && availableGuildIds.value.includes(options.selectedGuildId.value)
  )

  const applyState = (next: RaffleState) => {
    state.value = next
    for (const raffle of next.raffles) {
      entryPoints[raffle.id] ??= 1
      editMessages[raffle.id] = editMessages[raffle.id] ?? raffle.description
      eventWinnerCounts[raffle.id] ??= 1
    }
  }

  const loadState = async (expectedGuildId = options.selectedGuildId.value) => {
    if (!expectedGuildId || !availableGuildIds.value.includes(expectedGuildId)) {
      state.value = null
      return false
    }

    statePending.value = true
    try {
      const nextState = await api(`/api/raffles/${expectedGuildId}`) as RaffleState
      if (options.selectedGuildId.value !== expectedGuildId) {
        return false
      }
      applyState(nextState)
      return true
    } catch (error: any) {
      if (options.selectedGuildId.value !== expectedGuildId) {
        return false
      }
      toast.add({
        severity: 'error',
        summary: 'Load failed',
        detail: apiErrorMessage(error, 'Failed to load raffles.'),
        life: 5000,
      })
      return false
    } finally {
      if (options.selectedGuildId.value === expectedGuildId) {
        statePending.value = false
      }
    }
  }

  const refreshAll = async () => {
    await Promise.all([loadState(), options.refreshPointHistory()])
  }

  const closeStream = () => {
    stream?.close()
    stream = null
  }

  const openStream = (expectedGuildId = options.selectedGuildId.value) => {
    closeStream()
    if (!expectedGuildId || options.selectedGuildId.value !== expectedGuildId) {
      return
    }

    stream = openEventSource(`${apiBase}/api/raffles/${expectedGuildId}/stream`, {
      jsonHandlers: {
        state: payload => {
          if (options.selectedGuildId.value === expectedGuildId) {
            applyState(payload as RaffleState)
          }
        },
        completed: payload => {
          if (options.selectedGuildId.value === expectedGuildId) {
            options.onWinnerEvent(payload as WinnerEvent)
          }
        },
      },
      onParseError: (error, eventName) => {
        console.warn(`raffles: failed to parse ${eventName} event`, error)
      },
    })
  }

  watch([options.selectedGuildId, options.guildOptions, options.guildsPending], async ([id, opts, pending]) => {
    closeStream()
    state.value = null
    if (!id || pending || !opts?.some(g => g.guildId === id)) {
      return
    }

    const loaded = await loadState(id)
    if (loaded && options.selectedGuildId.value === id) {
      openStream(id)
    }
  }, { immediate: true })

  const hasRaffleType = (type: RaffleType) =>
    state.value?.raffles.some(r => r.raffleType === type) ?? false

  const lastRaffle = (type: RaffleType) =>
    state.value?.lastRaffles.find(r => r.raffleType === type) ?? null

  const canShowReopen = (type: RaffleType) => {
    if (!state.value || hasRaffleType(type)) {
      return false
    }
    return isEventRaffle(type)
      ? state.value.availability.hasPreviousEventRaffle
      : state.value.availability.hasPreviousRaffle
  }

  const canReopen = (type: RaffleType) => {
    if (!state.value) {
      return false
    }
    return isEventRaffle(type)
      ? state.value.permissions.canReopenEventRaffle
      : state.value.permissions.canReopenRaffle
  }

  const canEnter = (raffle: Raffle) => {
    if (!state.value) {
      return false
    }
    return isEventRaffle(raffle.raffleType)
      ? state.value.permissions.canEnterEventRaffle
      : state.value.permissions.canEnterRaffle
  }

  const runRaffleAction = async <T>(fn: () => Promise<T>, fallback: string) => {
    if (!options.selectedGuildId.value) {
      return null
    }
    return await runAction(fn, fallback)
  }

  const createRaffle = async (raffleType: RaffleType, description: string) =>
    await runRaffleAction(async () => {
      await api(`/api/raffles/${options.selectedGuildId.value}/create`, {
        method: 'POST',
        body: { raffleType, description },
      })
      await loadState()
      toast.add({
        severity: 'success',
        summary: 'Created',
        detail: isEventRaffle(raffleType) ? 'Event raffle created.' : 'Raffle created.',
        life: 2500,
      })
      return true
    }, 'Failed to create raffle.') === true

  const reopenRaffle = async (raffleType: RaffleType) => {
    await runRaffleAction(async () => {
      await api(`/api/raffles/${options.selectedGuildId.value}/reopen`, {
        method: 'POST',
        body: { raffleType },
      })
      await loadState()
      toast.add({
        severity: 'success',
        summary: 'Reopened',
        detail: isEventRaffle(raffleType) ? 'Event raffle reopened.' : 'Raffle reopened.',
        life: 2500,
      })
    }, 'Failed to reopen raffle.')
  }

  const enterRaffle = async (raffle: Raffle) => {
    await runRaffleAction(async () => {
      await api(`/api/raffles/${options.selectedGuildId.value}/enter`, {
        method: 'POST',
        body: { raffleId: raffle.id, points: entryPoints[raffle.id] ?? 1 },
      })
      await loadState()
      toast.add({ severity: 'success', summary: 'Entered', detail: 'Points added to the raffle.', life: 2500 })
    }, 'Failed to enter raffle.')
  }

  const completeRaffle = async (raffle: Raffle, winnersCount: number) => {
    await runRaffleAction(async () => {
      const completed = await api(`/api/raffles/${options.selectedGuildId.value}/complete`, {
        method: 'POST',
        body: { raffleType: raffle.raffleType, winnersCount },
      }) as WinnerEvent
      options.onWinnerEvent(completed)
      await loadState()
      toast.add({ severity: 'success', summary: 'Completed', detail: 'Raffle completed.', life: 2500 })
    }, 'Failed to complete raffle.')
  }

  const updateRaffle = async (raffle: Raffle, description: string) => {
    await runRaffleAction(async () => {
      await api(`/api/raffles/${options.selectedGuildId.value}/${raffle.id}`, {
        method: 'PUT',
        body: { raffleType: raffle.raffleType, description },
      })
      await loadState()
      toast.add({ severity: 'success', summary: 'Updated', detail: 'Discord message updated.', life: 2500 })
    }, 'Failed to update raffle message.')
  }

  return {
    state,
    statePending,
    actionPending,
    entryPoints,
    editMessages,
    eventWinnerCounts,
    quickAmounts,
    availableGuildIds,
    selectedGuildIsAvailable,
    raffleHistoryPanes,
    refreshAll,
    loadState,
    hasRaffleType,
    lastRaffle,
    canShowReopen,
    canReopen,
    canEnter,
    createRaffle,
    reopenRaffle,
    enterRaffle,
    completeRaffle,
    updateRaffle,
  }
}
