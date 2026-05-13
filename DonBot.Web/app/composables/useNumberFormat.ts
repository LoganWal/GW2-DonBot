const compactFormatter = new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 })

export const formatCompact = (n: number) => {
  if (Math.abs(n) >= 10_000) {
    return compactFormatter.format(n)
  }
  return n.toLocaleString()
}
