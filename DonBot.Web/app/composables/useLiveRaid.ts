export interface LiveRaidReport {
  reportId: number
  guildId: string
  fightsStart: string
  fightsEnd: string | null
  isOpen: boolean
  fightLogIds: number[]
}

export interface LiveRaidGuild {
  guildId: string
  guildName: string
}

export const useLiveRaid = (guildId: Ref<string | null>) => {
  const report = ref<LiveRaidReport | null>(null)
  const pending = ref(false)
  const error = ref<string | null>(null)
  const reloadKey = ref(0)

  const { openEventSource } = useEventSource()
  let stream: { close: () => void } | null = null

  const apiBase = useRuntimeConfig().public.apiBase as string

  const refresh = async (expectedGuildId = guildId.value) => {
    if (!expectedGuildId) {
      report.value = null
      return
    }
    pending.value = true
    error.value = null
    try {
      const nextReport = await $fetch<LiveRaidReport>(`${apiBase}/api/live-raid/${expectedGuildId}`, {
        credentials: 'include',
      })
      if (guildId.value !== expectedGuildId) {
        return false
      }
      report.value = nextReport
      return true
    } catch (e: any) {
      if (guildId.value !== expectedGuildId) {
        return false
      }
      if (e?.statusCode === 404 || e?.status === 404) {
        report.value = null
      } else {
        error.value = e?.message ?? 'Failed to load raid.'
      }
      return false
    } finally {
      if (guildId.value === expectedGuildId) {
        pending.value = false
      }
    }
  }

  const closeStream = () => {
    stream?.close()
    stream = null
  }

  const openStream = (expectedGuildId = guildId.value) => {
    closeStream()
    if (!expectedGuildId || guildId.value !== expectedGuildId) {
      return
    }
    stream = openEventSource(`${apiBase}/api/live-raid/${expectedGuildId}/stream`, {
      jsonHandlers: {
        'fight-added': payload => {
          if (guildId.value !== expectedGuildId) {
            return
          }
          const fightLogId = Number((payload as { fightLogId: number }).fightLogId)
          if (!Number.isFinite(fightLogId)) {
            return
          }
          if (!report.value) {
            return
          }
          if (!report.value.fightLogIds.includes(fightLogId)) {
            report.value = {
              ...report.value,
              fightLogIds: [...report.value.fightLogIds, fightLogId],
            }
            reloadKey.value++
          }
        },
        closed: payload => {
          if (guildId.value !== expectedGuildId) {
            return
          }
          const fightsEnd = (payload as { fightsEnd: string }).fightsEnd
          if (report.value) {
            report.value = { ...report.value, fightsEnd, isOpen: false }
          }
        },
      },
      handlers: {
        'report-changed': () => {
          refresh(expectedGuildId).then(refreshed => {
            if (refreshed && guildId.value === expectedGuildId) {
              reloadKey.value++
            }
          })
        },
      },
      onParseError: (err, eventName) => {
        console.warn(`live-raid: failed to parse ${eventName} payload`, err)
      },
    })
  }

  watch(guildId, async (id) => {
    closeStream()
    if (!id) {
      report.value = null
      return
    }
    const refreshed = await refresh(id)
    if (refreshed && guildId.value === id) {
      openStream(id)
    }
  }, { immediate: true })

  onUnmounted(() => {
    closeStream()
  })

  return { report, pending, error, reloadKey, refresh }
}
