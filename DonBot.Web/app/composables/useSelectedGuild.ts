const STORAGE_KEY = 'donbot.selectedGuildId'

/** Keeps the selected server in `?guild=` and localStorage. */
export const useSelectedGuild = () => {
  const route = useRoute()
  const router = useRouter()

  const initial = (() => {
    const fromQuery = route.query.guild
    if (typeof fromQuery === 'string' && fromQuery.length > 0) {
      return fromQuery
    }
    if (import.meta.client) {
      try {
        return window.localStorage.getItem(STORAGE_KEY)
      } catch {
        return null
      }
    }
    return null
  })()

  const selectedGuildId = ref<string | null>(initial)

  watch(selectedGuildId, (id) => {
    if (import.meta.client) {
      try {
        if (id) {
          window.localStorage.setItem(STORAGE_KEY, id)
        }
      } catch {
        // ignore storage failures (private browsing, quota, etc.)
      }
    }
    if (id && route.query.guild !== id) {
      router.replace({ query: { ...route.query, guild: id } })
    }
  })

  /** Keeps a saved selection only while it is still available. */
  const ensureSelection = (availableIds: string[], fallback: string | null) => {
    const current = selectedGuildId.value
    if (current && availableIds.includes(current)) {
      return
    }
    selectedGuildId.value = fallback
  }

  return { selectedGuildId, ensureSelection }
}
