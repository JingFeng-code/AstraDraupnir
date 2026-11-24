export type ThemeName = 'light' | 'dark'
const KEY = 'astra-theme'

/** Theme and sidebar preferences. Dark is the default (the approved Direction D); the choice is kept per browser. */
export function useTheme() {
  const theme = useState<ThemeName>('astra-theme', () => 'dark')
  const collapsed = useState<boolean>('astra-sidebar-collapsed', () => false)

  function init() {
    if (import.meta.server) return
    try {
      const saved = localStorage.getItem(KEY)
      theme.value = saved === 'light' || saved === 'dark' ? saved : 'dark'
      collapsed.value = localStorage.getItem('astra-sidebar') === 'collapsed'
    } catch { /* storage can be blocked; keep defaults */ }
    document.documentElement.dataset.theme = theme.value
  }
  function setTheme(next: ThemeName) {
    theme.value = next
    document.documentElement.dataset.theme = next
    try { localStorage.setItem(KEY, next) } catch { /* ignore */ }
  }
  function toggle() { setTheme(theme.value === 'dark' ? 'light' : 'dark') }
  function setCollapsed(value: boolean) {
    collapsed.value = value
    try { localStorage.setItem('astra-sidebar', value ? 'collapsed' : 'open') } catch { /* ignore */ }
  }
  return { theme, collapsed, init, setTheme, toggle, setCollapsed }
}
