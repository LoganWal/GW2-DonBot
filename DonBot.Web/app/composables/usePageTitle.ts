const TITLES: Record<string, string> = {
  '/dashboard':   'Dashboard',
  '/logs':        'Fight Logs',
  '/stats':       'My Stats',
  '/bests':       'Personal Bests',
  '/progression': 'Progression',
  '/leaderboard': 'Leaderboard',
  '/points':      'Points & Raffles',
  '/live-raid':   'Live Raid',
}

export const usePageTitle = () => {
  const route = useRoute()
  return computed(() => {
    for (const [prefix, label] of Object.entries(TITLES))
    {
      if (route.path === prefix || route.path.startsWith(prefix + '/'))
      {
        return label
      }
    }
    return 'DonBot'
  })
}
