import type { DifficultyFilter, SuccessFilter } from './useLogFilters'

type FightLogListQuery = {
  page: number
  pageSize: number
  fightTypes?: number[]
  characters?: string[]
  playstyles?: string[]
  successFilter?: SuccessFilter
  difficultyFilter?: DifficultyFilter
  startDateTime?: string
  endDateTime?: string
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
  characters: string[]
  playstyles: string[]
  sortMode?: string
}) => {
  const query: Record<string, string | number> = {}
  if (filters.page > 1) {
    query.page = filters.page
  }
  if (filters.fightTypes.length) {
    query.fightTypes = filters.fightTypes.join(',')
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
  return query
}

export const buildFightLogListUrl = (query: FightLogListQuery) => {
  const params = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  })

  appendNumberList(params, 'fightTypes', query.fightTypes)
  appendStringList(params, 'characters', query.characters)
  appendStringList(params, 'playstyles', query.playstyles)
  appendOptional(params, 'startDateTime', query.startDateTime)
  appendOptional(params, 'endDateTime', query.endDateTime)
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
