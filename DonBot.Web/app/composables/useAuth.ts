export const useAuth = () => {
  const user = useState<{ discordId: string; username: string; showCookieBanner: boolean } | null>('user', () => null)
  const inFlight = useState<Promise<void> | null>('user-fetch-inflight', () => null)
  const config = useRuntimeConfig()

  const fetchMe = async () => {
    if (inFlight.value) {
      return inFlight.value
    }
    const p = (async () => {
      try {
        user.value = await $fetch<{ discordId: string; username: string; showCookieBanner: boolean }>('/auth/me', {
          baseURL: config.public.apiBase,
          credentials: 'include'
        })
      } catch {
        user.value = null
      } finally {
        inFlight.value = null
      }
    })()
    inFlight.value = p
    return p
  }

  const logout = async () => {
    await $fetch('/auth/logout', {
      method: 'POST',
      baseURL: config.public.apiBase,
      credentials: 'include'
    })
    user.value = null
    await navigateTo('/')
  }

  return { user, fetchMe, logout }
}
