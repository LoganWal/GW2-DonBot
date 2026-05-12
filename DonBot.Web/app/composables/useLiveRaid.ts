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

  let source: EventSource | null = null

  const apiBase = useRuntimeConfig().public.apiBase as string

  const refresh = async () => {
    if (!guildId.value) {
      report.value = null
      return
    }
    pending.value = true
    error.value = null
    try {
      report.value = await $fetch<LiveRaidReport>(`${apiBase}/api/live-raid/${guildId.value}`, {
        credentials: 'include',
      })
    } catch (e: any) {
      if (e?.statusCode === 404 || e?.status === 404) {
        report.value = null
      } else {
        error.value = e?.message ?? 'Failed to load raid.'
      }
    } finally {
      pending.value = false
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
    if (!guildId.value) {
      return
    }
    if (typeof EventSource === 'undefined') {
      return
    }
    source = new EventSource(`${apiBase}/api/live-raid/${guildId.value}/stream`, { withCredentials: true })

    source.addEventListener('fight-added', (e: MessageEvent) => {
      try {
        const payload = JSON.parse(e.data) as { fightLogId: number }
        if (!report.value) {
          return
        }
        if (!report.value.fightLogIds.includes(payload.fightLogId)) {
          report.value = {
            ...report.value,
            fightLogIds: [...report.value.fightLogIds, payload.fightLogId],
          }
          reloadKey.value++
        }
      } catch (err) {
        console.warn('live-raid: failed to parse fight-added payload', err)
      }
    })

    source.addEventListener('report-changed', () => {
      refresh().then(() => { reloadKey.value++ })
    })

    source.addEventListener('closed', (e: MessageEvent) => {
      try {
        const payload = JSON.parse(e.data) as { fightsEnd: string }
        if (report.value) {
          report.value = { ...report.value, fightsEnd: payload.fightsEnd, isOpen: false }
        }
      } catch (err) {
        console.warn('live-raid: failed to parse closed payload', err)
      }
    })

    source.onerror = () => {
      // EventSource auto-reconnects; if it errors persistently the browser keeps trying.
    }
  }

  watch(guildId, async (id) => {
    closeStream()
    if (!id) {
      report.value = null
      return
    }
    await refresh()
    openStream()
  }, { immediate: true })

  onUnmounted(() => {
    closeStream()
  })

  return { report, pending, error, reloadKey, refresh }
}
