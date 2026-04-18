export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()
  const appConfig = (window as any).__APP_CONFIG__
  if (appConfig?.apiBase) {
    config.public.apiBase = appConfig.apiBase
  }
})
