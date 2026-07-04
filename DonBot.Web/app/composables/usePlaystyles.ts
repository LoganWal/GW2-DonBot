export type PlaystyleOption = {
  label: string
  value: string
}

export type PlaystyleBreakdownRow = {
  key: string
  label?: string
  count: number
  percent: number
}

export const pvePlaystyleOptions: PlaystyleOption[] = [
  { label: 'DPS', value: 'dps' },
  { label: 'Boon DPS', value: 'boon-dps' },
  { label: 'Boon Healer', value: 'boon-healer' },
  { label: 'Mechanic', value: 'mechanic' },
]

export const wvwPlaystyleOptions: PlaystyleOption[] = [
  { label: 'DPS', value: 'dps' },
  { label: 'Support DPS', value: 'support-dps' },
  { label: 'Support', value: 'support' },
  { label: 'Heal Support', value: 'heal-support' },
]

export const playstyleGroupedOptions = [
  { label: 'WvW', items: wvwPlaystyleOptions },
  { label: 'PvE', items: pvePlaystyleOptions },
]

export const playstyleLabels: Record<string, string> = {
  dps: 'DPS',
  'boon-dps': 'Boon DPS',
  'boon-healer': 'Boon Healer',
  mechanic: 'Mechanic',
  'support-dps': 'Support DPS',
  support: 'Support',
  'heal-support': 'Heal Support',
}

export const playstyleOptionsForFightType = (fightType: number | null | undefined) =>
  fightType === 0 ? wvwPlaystyleOptions : pvePlaystyleOptions

export const playstyleValuesForFightType = (fightType: number | null | undefined) =>
  playstyleOptionsForFightType(fightType).map(option => option.value)

export const playstyleLabelFromKey = (key: string) =>
  playstyleLabels[key] ?? key

export const playstyleKeyFromRow = (row: any) => {
  const breakdown = row?.playstyleBreakdown ?? []
  if (breakdown.length === 1) {
    return breakdown[0].key as string
  }
  const raw = String(row?.playstyle ?? '')
  if (playstyleLabels[raw]) {
    return raw
  }
  const match = Object.entries(playstyleLabels).find(([, label]) => label === raw)
  return match?.[0] ?? raw
}

export const playstyleLabel = (row: any) => {
  const raw = String(row?.playstyle ?? '')
  if (raw === 'Mixed') {
    return raw
  }
  return playstyleLabelFromKey(playstyleKeyFromRow(row)) || raw
}

export const playstyleTooltip = (row: any) => {
  const breakdown = row?.playstyleBreakdown ?? []
  if (breakdown.length <= 1) {
    return null
  }
  return breakdown.map((r: any) => `${r.count} ${r.label}`).join('\n')
}

export const playstyleSeverity = (value: string | Record<string, any>) => {
  const key = typeof value === 'string' ? value : playstyleKeyFromRow(value)
  if (typeof value !== 'string' && String(value?.playstyle ?? '') === 'Mixed') {
    return 'secondary'
  }
  switch (key) {
    case 'boon-dps':
    case 'support-dps':
      return 'success'
    case 'boon-healer':
    case 'heal-support':
      return 'info'
    case 'mechanic':
      return 'contrast'
    case 'support':
      return 'warn'
    default:
      return 'secondary'
  }
}

export const normalizePlaystyleRows = (rows?: PlaystyleBreakdownRow[]) =>
  (rows ?? [])
    .map(row => ({
      key: row.key,
      label: row.label ?? playstyleLabelFromKey(row.key),
      count: Number(row.count ?? 0),
      percent: Number(row.percent ?? 0),
    }))
    .filter(row => row.count > 0)

export const playstyleWidth = (percent: number) =>
  `${Math.max(6, Math.min(100, percent))}%`

export const playstyleFillClass = (key: string) => {
  switch (key) {
    case 'boon-dps':
    case 'support-dps':
      return 'fill-teal'
    case 'boon-healer':
    case 'heal-support':
      return 'fill-green'
    case 'mechanic':
    case 'support':
      return 'fill-amber'
    default:
      return 'fill-blue'
  }
}
