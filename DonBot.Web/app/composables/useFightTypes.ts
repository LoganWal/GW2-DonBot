export const FIGHT_NAMES: Record<number, string> = {
  0: 'WvW',
  1: 'Vale Guardian', 2: 'Gorseval', 3: 'Sabetha', 53: 'Spirit Woods',
  4: 'Slothasor', 5: 'Trio', 6: 'Matthias',
  7: 'Escort', 8: 'Keep Construct', 9: 'Twisted Castle', 10: 'Xera',
  11: 'Cairn', 12: 'Mursaat Overseer', 13: 'Samarog', 14: 'Deimos',
  15: 'Soulless Horror', 16: 'River of Souls', 17: 'Broken King', 18: 'Eater of Souls', 19: 'Voice in the Void', 20: 'Dhuum',
  21: 'Conjured Amalgamate', 22: 'Twin Largos', 23: 'Qadim',
  24: 'Cardinal Adina', 25: 'Cardinal Sabir', 26: 'Qadim the Peerless',
  44: 'Greer', 45: 'Decima', 46: 'Ura',
  55: 'Kela',
  27: 'Aetherblade Hideout', 28: 'Xunlai Jade Junkyard', 29: 'Kaineng Overlook', 30: 'Harvest Temple',
  31: "Old Lion's Court",
  32: 'Cosmic Observatory', 33: 'Temple of Febe',
  47: 'Icebrood Construct', 48: 'Fraenir', 49: 'Voice of the Fallen', 50: 'Whisper of Jormag', 51: 'Boneskinner',
  34: 'MAMA', 35: 'Siax', 36: 'Ensolyss', 37: 'Skorvald', 38: 'Artsariiv', 39: 'Arkk',
  40: 'Ai (Ele)', 41: 'Ai (Dark)', 42: 'Ai (Both)', 43: 'Kanaxai', 52: 'Eparch', 54: 'Shadow of the Dragon',
  32766: 'Golem',
}

const GROUPS: { label: string; values: number[] }[] = [
  { label: 'Wing 1', values: [1, 53, 2, 3] },
  { label: 'Wing 2', values: [4, 5, 6] },
  { label: 'Wing 3', values: [7, 8, 9, 10] },
  { label: 'Wing 4', values: [11, 12, 13, 14] },
  { label: 'Wing 5', values: [15, 16, 17, 18, 19, 20] },
  { label: 'Wing 6', values: [21, 22, 23] },
  { label: 'Wing 7', values: [24, 25, 26] },
  { label: 'Wing 8', values: [44, 45, 46] },
  { label: 'Wing 9', values: [55] },
  { label: 'EoD Strikes', values: [27, 28, 29, 30] },
  { label: 'Core Strikes', values: [31] },
  { label: 'SotO Strikes', values: [32, 33] },
  { label: 'Icebrood Strikes', values: [47, 48, 49, 50, 51] },
  { label: 'Fractals', values: [34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 52, 54] },
  { label: 'Golem', values: [32766] },
]

const FIGHT_TYPE_TO_GROUP: Record<number, string> = {}
for (const g of GROUPS)
{
  for (const v of g.values)
  {
    FIGHT_TYPE_TO_GROUP[v] = g.label
  }
}

export const fightName = (type: number) => FIGHT_NAMES[type] ?? 'Unknown'
export const fightGroup = (type: number) => FIGHT_TYPE_TO_GROUP[type] ?? 'Other'

/** Grouped options for Select / MultiSelect (WvW first, then raid wings, strikes, fractals) */
export const fightTypeGroupedOptions = [
  { label: 'WvW', items: [{ label: 'WvW', value: 0, group: 'WvW' }] },
  ...GROUPS.map(g => ({
    label: g.label,
    items: g.values.map(v => ({ label: FIGHT_NAMES[v]!, value: v, group: g.label })),
  })),
]

/** Group an array of objects that have a fightType number field */
export function groupByFightType<T extends { fightType: number }>(items: T[]): { label: string; items: T[] }[] {
  const order = ['Wing 1', 'Wing 2', 'Wing 3', 'Wing 4', 'Wing 5', 'Wing 6', 'Wing 7', 'Wing 8', 'Wing 9',
    'EoD Strikes', 'Core Strikes', 'SotO Strikes', 'Icebrood Strikes', 'Fractals', 'Golem', 'Other']
  const map = new Map<string, T[]>()
  for (const item of items)
  {
    const g = fightGroup(item.fightType)
    if (!map.has(g)) map.set(g, [])
    map.get(g)!.push(item)
  }
  return order.filter(g => map.has(g)).map(g => ({ label: g, items: map.get(g)! }))
}
