export const useSidebar = () => {
  const collapsed = useState<boolean>('sidebar-collapsed', () => false)
  const mobileOpen = useState<boolean>('sidebar-mobile-open', () => false)

  const toggle = () => { collapsed.value = !collapsed.value }
  const toggleMobile = () => { mobileOpen.value = !mobileOpen.value }
  const closeMobile = () => { mobileOpen.value = false }

  return { collapsed, mobileOpen, toggle, toggleMobile, closeMobile }
}
