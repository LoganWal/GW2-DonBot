const STORAGE_KEY = 'donbot.selectedGuildId'

/**
 * Tracks the user's currently selected server across page refreshes.
 * Reads (priority): `?guild=` query param, then localStorage. Persists changes
 * to both. The URL query keeps the selection shareable; localStorage carries
 * it between unrelated pages.
 */
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

  /**
   * Picks an initial selection from the available options when the dropdown
   * loads. If the persisted/url id is in `availableIds`, keep it; otherwise
   * fall back to `fallback` (typically the first option).
   */
  const ensureSelection = (availableIds: string[], fallback: string | null) => {
    const current = selectedGuildId.value
    if (current && availableIds.includes(current)) {
      return
    }
    selectedGuildId.value = fallback
  }

  return { selectedGuildId, ensureSelection }
}
