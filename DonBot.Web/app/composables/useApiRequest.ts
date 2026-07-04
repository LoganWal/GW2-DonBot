export type ApiResult<T> = {
  data: T
  failed: boolean
}

export const useApiRequest = () => {
  const api = useApi()

  const request = <T>(url: string, options?: any) =>
    api(url, options) as Promise<T>

  const safeRequest = async <T>(url: string, fallback: T, failureLabel = 'API request'): Promise<ApiResult<T>> => {
    try {
      return { data: await request<T>(url), failed: false }
    } catch (error) {
      console.warn(`${failureLabel} failed: ${url}`, error)
      return { data: fallback, failed: true }
    }
  }

  return { request, safeRequest }
}
