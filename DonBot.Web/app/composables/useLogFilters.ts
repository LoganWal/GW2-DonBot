export type SuccessFilter = 'all' | 'kills' | 'wipes'
export type DifficultyFilter = number | null

export const successFilterOptions: { label: string; value: SuccessFilter; severity?: string }[] = [
  { label: 'All', value: 'all', severity: 'primary' },
  { label: 'Kills', value: 'kills', severity: 'success' },
  { label: 'Wipes', value: 'wipes', severity: 'danger' },
]

export const difficultyFilterOptions: { label: string; value: DifficultyFilter; severity?: string }[] = [
  { label: 'All modes', value: null, severity: 'primary' },
  { label: 'NM', value: 0, severity: 'primary' },
  { label: 'CM', value: 1, severity: 'primary' },
  { label: 'LCM', value: 2, severity: 'primary' },
]
