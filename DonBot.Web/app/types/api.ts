export type ServerOption = {
  guildId: string
  guildName?: string | null
  name?: string | null
  iconUrl?: string | null
}

export type GuildOption = {
  guildId: string
  guildName: string
}

export type AdminGuildSummary = {
  guildId: string
  name: string
  iconUrl: string | null
}

export type RaffleType = 0 | 1

export type RaffleBid = {
  discordId: string
  displayName: string
  pointsSpent: number
}

export type RaffleHistoryBid = RaffleBid & {
  isWinner: boolean
}

export type Raffle = {
  id: number
  raffleType: RaffleType
  type: string
  description: string
  isActive: boolean
  canEdit: boolean
  userBid: number
  totalPoints: number
  topBidders: RaffleBid[]
}

export type RaffleHistory = {
  id: number
  raffleType: RaffleType
  type: string
  description: string
  totalPoints: number
  winners: RaffleHistoryBid[]
  bids: RaffleHistoryBid[]
}

export type RaffleState = {
  guildId: string
  guildName: string
  account: { points: number; availablePoints: number } | null
  raffles: Raffle[]
  lastRaffles: RaffleHistory[]
  permissions: {
    canEnterRaffle: boolean
    canEnterEventRaffle: boolean
    canCreateRaffle: boolean
    canCreateEventRaffle: boolean
    canCompleteRaffle: boolean
    canCompleteEventRaffle: boolean
    canReopenRaffle: boolean
    canReopenEventRaffle: boolean
  }
  availability: {
    hasPreviousRaffle: boolean
    hasPreviousEventRaffle: boolean
  }
}

export type WinnerEvent = {
  raffleId: number
  raffleType: RaffleType
  type: string
  description: string
  drawAtUtc?: string
  winners: RaffleBid[]
}

export type PointComponent = {
  metric: string
  metricLabel: string
  metricValue: number
  percentileValue: number
  basePoints: number
  multiplier: number
  points: number
  reason: string
}

export type PointHistoryResponse = {
  account: { points: number; availablePoints: number } | null
  summary: {
    totalEarned: number
    availablePoints: number
    spentPoints: number
    earnedLast30Days: number
    awardedLogs: number
    lastAwardAt: string | null
  }
  byComponent: { metric: string; metricLabel: string; points: number; count: number }[]
  byFightType: { fightType: number; points: number; count: number }[]
  recent: {
    fightLogId: number
    playerFightLogId: number
    accountName: string
    fightType: number
    fightStart: string
    url: string
    totalPoints: number
    components: PointComponent[]
  }[]
}
