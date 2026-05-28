import { updatePrimaryPalette } from '@primevue/themes'

export type ThemeAppearance = 'dark' | 'light' | 'system'
export type ThemeAccent = 'blue' | 'emerald' | 'violet' | 'rose' | 'amber'

type StoredTheme = {
  appearance?: ThemeAppearance
  accent?: ThemeAccent
}

const STORAGE_KEY = 'donbot-theme'

const appearanceOptions: { label: string; value: ThemeAppearance; icon: string }[] = [
  { label: 'Dark', value: 'dark', icon: 'pi pi-moon' },
  { label: 'Light', value: 'light', icon: 'pi pi-sun' },
  { label: 'System', value: 'system', icon: 'pi pi-desktop' }
]

const accentOptions: { label: string; value: ThemeAccent; swatch: string; palette: Record<number, string> }[] = [
  { label: 'Blue', value: 'blue', swatch: '#3b82f6', palette: buildPalette('blue') },
  { label: 'Emerald', value: 'emerald', swatch: '#10b981', palette: buildPalette('emerald') },
  { label: 'Violet', value: 'violet', swatch: '#8b5cf6', palette: buildPalette('violet') },
  { label: 'Rose', value: 'rose', swatch: '#f43f5e', palette: buildPalette('rose') },
  { label: 'Amber', value: 'amber', swatch: '#f59e0b', palette: buildPalette('amber') }
]

export const useTheme = () => {
  const appearance = useState<ThemeAppearance>('theme-appearance', () => 'dark')
  const accent = useState<ThemeAccent>('theme-accent', () => 'blue')
  const systemDark = useState<boolean>('theme-system-dark', () => true)
  const initialized = useState<boolean>('theme-initialized', () => false)

  const dark = computed(() => appearance.value === 'system' ? systemDark.value : appearance.value === 'dark')
  const selectedAccent = computed(() => accentOptions.find(t => t.value === accent.value) ?? accentOptions[0])

  const persist = () => {
    if (import.meta.client) {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ appearance: appearance.value, accent: accent.value }))
    }
  }

  const apply = () => {
    if (!import.meta.client) {
      return
    }

    document.documentElement.classList.toggle('dark', dark.value)
    document.documentElement.dataset.themeAccent = accent.value
    updatePrimaryPalette(selectedAccent.value.palette)
  }

  const setAppearance = (value: ThemeAppearance) => {
    appearance.value = value
    apply()
    persist()
  }

  const setAccent = (value: ThemeAccent) => {
    accent.value = value
    apply()
    persist()
  }

  const toggle = () => {
    setAppearance(dark.value ? 'light' : 'dark')
  }

  const initialize = () => {
    if (!import.meta.client || initialized.value) {
      return
    }

    const media = window.matchMedia('(prefers-color-scheme: dark)')
    systemDark.value = media.matches

    const stored = parseStoredTheme(window.localStorage.getItem(STORAGE_KEY))
    if (stored?.appearance && isThemeAppearance(stored.appearance)) {
      appearance.value = stored.appearance
    }
    if (stored?.accent && isThemeAccent(stored.accent)) {
      accent.value = stored.accent
    }

    apply()
    initialized.value = true

    media.addEventListener('change', event => {
      systemDark.value = event.matches
      if (appearance.value === 'system') {
        apply()
      }
    })
  }

  return {
    accent,
    accentOptions,
    appearance,
    appearanceOptions,
    dark,
    selectedAccent,
    initialize,
    setAccent,
    setAppearance,
    toggle
  }
}

function buildPalette(color: ThemeAccent) {
  return {
    50: `{${color}.50}`,
    100: `{${color}.100}`,
    200: `{${color}.200}`,
    300: `{${color}.300}`,
    400: `{${color}.400}`,
    500: `{${color}.500}`,
    600: `{${color}.600}`,
    700: `{${color}.700}`,
    800: `{${color}.800}`,
    900: `{${color}.900}`,
    950: `{${color}.950}`
  }
}

function parseStoredTheme(value: string | null): StoredTheme | null {
  if (!value) {
    return null
  }

  try {
    return JSON.parse(value) as StoredTheme
  } catch {
    return null
  }
}

function isThemeAppearance(value: string): value is ThemeAppearance {
  return appearanceOptions.some(option => option.value === value)
}

function isThemeAccent(value: string): value is ThemeAccent {
  return accentOptions.some(option => option.value === value)
}
