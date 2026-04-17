export const useAuth = () => {
  const user = useState<{ discordId: string; username: string; showCookieBanner: boolean } | null>('user', () => null)
  const config = useRuntimeConfig()

  const fetchMe = async () => {
    try {
      user.value = await $fetch<{ discordId: string; username: string; showCookieBanner: boolean }>('/auth/me', {
        baseURL: config.public.apiBase,
        credentials: 'include'
      })
    } catch {
      user.value = null
    }
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
