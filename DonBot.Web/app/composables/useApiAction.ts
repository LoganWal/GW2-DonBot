export const apiErrorMessage = (error: any, fallback: string) => {
  if (error?.data?.error) {
    return error.data.error
  }
  if (error?.statusCode === 403 || error?.response?.status === 403) {
    return 'You do not have access to that action for this server.'
  }
  return error?.message ?? fallback
}

export const useApiAction = (onError?: (message: string) => void) => {
  const pending = ref(false)

  const runAction = async <T>(fn: () => Promise<T>, fallback: string) => {
    pending.value = true
    try {
      return await fn()
    } catch (error: any) {
      const message = apiErrorMessage(error, fallback)
      if (onError) {
        onError(message)
      }
      return null
    } finally {
      pending.value = false
    }
  }

  return { pending, runAction }
}
