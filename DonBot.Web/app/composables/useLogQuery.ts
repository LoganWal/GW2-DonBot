import type { DifficultyFilter, SuccessFilter } from './useLogFilters'

type FightLogListQuery = {
  page: number
  pageSize: number
  fightTypes?: number[]
  guildIds?: string[]
  characters?: string[]
  playstyles?: string[]
  successFilter?: SuccessFilter
  difficultyFilter?: DifficultyFilter
  startDateTime?: string
  endDateTime?: string
  minDurationSeconds?: number | null
  maxDurationSeconds?: number | null
  minFightPercent?: number | null
  maxFightPercent?: number | null
  sortField?: string
  sortOrder?: 'asc' | 'desc'
}

type ProgressionQuery = {
  fightType: number
  playstyles: string[]
  startDateTime?: string
  successFilter?: SuccessFilter
  difficultyFilter?: DifficultyFilter
}

export const parseNumberListQuery = (value: unknown) =>
  value == null || value === ''
    ? []
    : String(value).split(',').map(Number).filter(n => Number.isFinite(n))

export const parseStringListQuery = (value: unknown) =>
  value == null || value === ''
    ? []
    : String(value).split(',').filter(Boolean)

export const buildLogsRouteQuery = (filters: {
  page: number
  fightTypes: number[]
  guildIds: string[]
  characters: string[]
  playstyles: string[]
  sortMode?: string
  successFilter?: SuccessFilter
  difficultyFilter?: DifficultyFilter
  startDateTime?: string
  endDateTime?: string
  minDurationSeconds?: number | null
  maxDurationSeconds?: number | null
  minFightPercent?: number | null
  maxFightPercent?: number | null
  sortField?: string
  sortOrder?: 'asc' | 'desc'
}) => {
  const query: Record<string, string | number> = {}
  if (filters.page > 1) {
    query.page = filters.page
  }
  if (filters.fightTypes.length) {
    query.fightTypes = filters.fightTypes.join(',')
  }
  if (filters.guildIds.length) {
    query.guildIds = filters.guildIds.join(',')
  }
  if (filters.characters.length) {
    query.characters = filters.characters.join(',')
  }
  if (filters.playstyles.length) {
    query.playstyles = filters.playstyles.join(',')
  }
  if (filters.sortMode === 'category') {
    query.sort = 'category'
  }
  if (filters.successFilter && filters.successFilter !== 'all') query.result = filters.successFilter
  if (filters.difficultyFilter !== undefined && filters.difficultyFilter !== null) query.mode = filters.difficultyFilter
  if (filters.startDateTime) query.from = filters.startDateTime
  if (filters.endDateTime) query.to = filters.endDateTime
  if (filters.minDurationSeconds != null) query.minDuration = filters.minDurationSeconds
  if (filters.maxDurationSeconds != null) query.maxDuration = filters.maxDurationSeconds
  if (filters.minFightPercent != null) query.minPercent = filters.minFightPercent
  if (filters.maxFightPercent != null) query.maxPercent = filters.maxFightPercent
  if (filters.sortField && filters.sortField !== 'fightStart') query.sortField = filters.sortField
  if (filters.sortOrder === 'asc') query.sortOrder = filters.sortOrder
  return query
}

export const buildFightLogListUrl = (query: FightLogListQuery) => {
  const params = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  })

  appendNumberList(params, 'fightTypes', query.fightTypes)
  appendStringList(params, 'guildIds', query.guildIds)
  appendStringList(params, 'characters', query.characters)
  appendStringList(params, 'playstyles', query.playstyles)
  appendOptional(params, 'startDateTime', query.startDateTime)
  appendOptional(params, 'endDateTime', query.endDateTime)
  appendNumber(params, 'minDurationSeconds', query.minDurationSeconds)
  appendNumber(params, 'maxDurationSeconds', query.maxDurationSeconds)
  appendNumber(params, 'minFightPercent', query.minFightPercent)
  appendNumber(params, 'maxFightPercent', query.maxFightPercent)
  appendOptional(params, 'sortField', query.sortField)
  appendOptional(params, 'sortOrder', query.sortOrder)
  appendSuccessFilter(params, query.successFilter)
  appendDifficultyFilter(params, query.difficultyFilter)

  return `/api/logs?${params.toString()}`
}

export const buildProgressionUrl = (query: ProgressionQuery) => {
  const params = new URLSearchParams({
    fightType: String(query.fightType),
    playstyles: query.playstyles.join(','),
  })

  appendOptional(params, 'startDateTime', query.startDateTime)
  appendSuccessFilter(params, query.successFilter)
  appendDifficultyFilter(params, query.difficultyFilter)

  return `/api/stats/progression?${params.toString()}`
}

const appendNumberList = (params: URLSearchParams, key: string, values?: number[]) => {
  if (values?.length) {
    params.set(key, values.join(','))
  }
}

const appendStringList = (params: URLSearchParams, key: string, values?: string[]) => {
  if (values?.length) {
    params.set(key, values.join(','))
  }
}

const appendOptional = (params: URLSearchParams, key: string, value?: string) => {
  if (value) {
    params.set(key, value)
  }
}

const appendNumber = (params: URLSearchParams, key: string, value?: number | null) => {
  if (value !== undefined && value !== null) {
    params.set(key, String(value))
  }
}

const appendSuccessFilter = (params: URLSearchParams, successFilter?: SuccessFilter) => {
  if (successFilter === 'kills') {
    params.set('isSuccess', 'true')
  } else if (successFilter === 'wipes') {
    params.set('isSuccess', 'false')
  }
}

const appendDifficultyFilter = (params: URLSearchParams, difficultyFilter?: DifficultyFilter) => {
  if (difficultyFilter !== undefined && difficultyFilter !== null) {
    params.set('fightMode', String(difficultyFilter))
  }
}
