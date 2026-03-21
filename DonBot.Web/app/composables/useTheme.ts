export const useTheme = () => {
  const dark = useState<boolean>('theme-dark', () => true)

  const toggle = () => {
    dark.value = !dark.value
    document.documentElement.classList.toggle('dark', dark.value)
  }

  return { dark, toggle }
}
