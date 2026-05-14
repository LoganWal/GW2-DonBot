export default defineNuxtRouteMiddleware(async (to) => {
  if (to.path === '/' || to.path.startsWith('/auth')) return

  const { user, fetchMe } = useAuth()
  if (!user.value) {
    await fetchMe()
  }
  if (!user.value) {
    const config = useRuntimeConfig()
    const returnTo = encodeURIComponent(to.fullPath)
    return navigateTo(`${config.public.apiBase}/auth/discord?returnTo=${returnTo}`, { external: true })
  }
})
