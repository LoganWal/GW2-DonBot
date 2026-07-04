const integerFormatter = new Intl.NumberFormat('en')
const compactNumberFormatter = new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 })

export const formatInteger = (value: number | null | undefined) =>
  integerFormatter.format(Math.round(Number(value) || 0))

export const formatDecimal = (value: number | null | undefined, digits = 2) =>
  (Number(value) || 0).toFixed(digits)

export const formatPercent = (value: number | null | undefined, digits = 2) =>
  `${formatDecimal(value, digits)}%`

export const formatSeconds = (valueMs: number | null | undefined, digits = 1) =>
  `${formatDecimal((Number(valueMs) || 0) / 1000, digits)}s`

export const formatMilliseconds = (valueMs: number | null | undefined, long = false) => {
  const seconds = Math.floor((Number(valueMs) || 0) / 1000)
  if (long) {
    const minutes = Math.floor(seconds / 60)
    const hours = Math.floor(minutes / 60)
    if (hours > 0) {
      return `${hours}h ${minutes % 60}m ${seconds % 60}s`
    }
    return `${minutes}m ${seconds % 60}s`
  }
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

export const formatDate = (value: string | number | Date) =>
  new Date(value).toLocaleDateString()

export const formatShortDate = (value: string | number | Date) =>
  new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })

export const formatDateTime = (value: string | number | Date) =>
  new Date(value).toLocaleString()

export const formatPoints = (value: number | null | undefined) =>
  Math.floor(Number(value) || 0).toLocaleString()

export const formatPointValue = (value: number | null | undefined) =>
  Number(value ?? 0).toLocaleString(undefined, { maximumFractionDigits: 3 })

export const formatCompactNumber = (value: number | null | undefined) => {
  const n = Number(value) || 0
  if (Math.abs(n) >= 10_000) {
    return compactNumberFormatter.format(n)
  }
  return integerFormatter.format(n)
}
