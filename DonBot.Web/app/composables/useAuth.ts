export const useAuth = () => {
  const user = useState<{ discordId: string; username: string } | null>('user', () => null)
  const config = useRuntimeConfig()

  const fetchMe = async () => {
    try {
      const data = await $fetch<{ discordId: string; username: string }>('/auth/me', {
        baseURL: config.public.apiBase,
        credentials: 'include'
      })
      user.value = data
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
