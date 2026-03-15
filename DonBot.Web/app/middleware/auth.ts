export default defineNuxtRouteMiddleware(async (to) => {
  if (to.path === '/' || to.path.startsWith('/auth')) return

  const { user, fetchMe } = useAuth()
  if (!user.value) {
    await fetchMe()
  }
  if (!user.value) {
    return navigateTo('/')
  }
})
