const TITLES: Record<string, string> = {
  '/dashboard':   'Dashboard',
  '/logs':        'Fight Logs',
  '/stats':       'My Stats',
  '/leaderboard': 'Leaderboard',
  '/points':      'Points & Raffles',
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
