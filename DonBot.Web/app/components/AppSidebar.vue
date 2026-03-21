<template>
  <aside :class="['app-sidebar', { collapsed, 'mobile-open': mobileOpen }]">
    <div class="sidebar-logo">
      <NuxtLink to="/" class="logo-link" @click="closeMobile">
        <div class="logo-icon-wrap">
          <img v-if="!logoImgError" :src="logoImgSrc" alt="DonBot" class="logo-img" @error="logoImgError = true" />
          <i v-else class="pi pi-bolt" style="color: #fff; font-size: 0.9rem;" />
        </div>
        <span class="logo-text">DonBot</span>
      </NuxtLink>
    </div>

    <nav class="sidebar-nav">
      <NuxtLink
        v-for="item in navItems"
        :key="item.to"
        v-tooltip.right="collapsed ? item.label : null"
        :to="item.to"
        :class="['nav-item', { active: isActive(item.to) }]"
        @click="closeMobile"
      >
        <i :class="['pi', item.icon, 'nav-icon']" />
        <span class="nav-label">{{ item.label }}</span>
        <span v-if="isActive(item.to)" class="active-indicator" />
      </NuxtLink>
    </nav>

    <div class="sidebar-footer">
      <Button
        :icon="collapsed ? 'pi pi-chevron-right' : 'pi pi-chevron-left'"
        text
        severity="secondary"
        size="small"
        @click="toggle"
        aria-label="Toggle sidebar"
      />
    </div>
  </aside>
</template>

<script setup lang="ts">
const { collapsed, mobileOpen, toggle, closeMobile } = useSidebar()

const logoImgSrc = '/donbot.png'
const logoImgError = ref(false)
const route = useRoute()

const navItems = [
  { label: 'Dashboard',      to: '/dashboard',   icon: 'pi-home' },
  { label: 'Fight Logs',    to: '/logs',        icon: 'pi-list' },
  { label: 'My Stats',      to: '/stats',       icon: 'pi-chart-bar' },
  { label: 'Personal Bests', to: '/bests',       icon: 'pi-crown' },
  { label: 'Progression',   to: '/progression', icon: 'pi-chart-line' },
  { label: 'Leaderboard',   to: '/leaderboard', icon: 'pi-trophy' },
  { label: 'Points',        to: '/points',      icon: 'pi-star' },
]

const isActive = (to: string) =>
  route.path === to || route.path.startsWith(to + '/')
</script>

<style scoped>
.app-sidebar {
  position: fixed;
  top: 0;
  left: 0;
  height: 100vh;
  width: 240px;
  background: var(--p-surface-card);
  border-right: 1px solid var(--p-surface-border);
  display: flex;
  flex-direction: column;
  transition: width 0.2s ease;
  z-index: 100;
  overflow: hidden;
}

.app-sidebar.collapsed {
  width: 64px;
}

/* Logo */
.sidebar-logo {
  height: 60px;
  display: flex;
  align-items: center;
  padding: 0 1rem;
  border-bottom: 1px solid var(--p-surface-border);
  flex-shrink: 0;
}

.logo-link {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  text-decoration: none;
  overflow: hidden;
  white-space: nowrap;
}

.logo-icon-wrap {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: var(--p-primary-color);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
}

.logo-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 8px;
}

.logo-text {
  font-size: 1.05rem;
  font-weight: 700;
  color: var(--p-text-color);
  letter-spacing: -0.01em;
}

.app-sidebar.collapsed .logo-text {
  opacity: 0;
  width: 0;
}

/* Nav */
.sidebar-nav {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 0.75rem 0.5rem;
  overflow-y: auto;
  overflow-x: hidden;
}

.nav-item {
  position: relative;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.6rem 0.75rem;
  border-radius: 8px;
  color: var(--p-text-muted-color);
  text-decoration: none;
  font-size: 0.875rem;
  font-weight: 500;
  transition: color 0.15s, background 0.15s;
  white-space: nowrap;
  overflow: hidden;
}

.nav-icon {
  font-size: 1rem;
  flex-shrink: 0;
  width: 1rem;
  text-align: center;
  transition: color 0.15s;
}

.nav-label {
  transition: opacity 0.15s;
}

.app-sidebar.collapsed .nav-label {
  opacity: 0;
  width: 0;
}

.nav-item:hover {
  color: var(--p-text-color);
  background: var(--p-surface-hover);
}

.nav-item.active {
  color: var(--p-primary-color);
  background: color-mix(in srgb, var(--p-primary-color) 12%, transparent);
  font-weight: 600;
}

.nav-item.active .nav-icon {
  color: var(--p-primary-color);
}

.active-indicator {
  position: absolute;
  right: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 60%;
  border-radius: 3px 0 0 3px;
  background: var(--p-primary-color);
}

/* Footer */
.sidebar-footer {
  padding: 0.75rem 0.5rem;
  border-top: 1px solid var(--p-surface-border);
  display: flex;
  justify-content: flex-end;
}

.app-sidebar.collapsed .sidebar-footer {
  justify-content: center;
}

/* Mobile */
@media (max-width: 768px) {
  .app-sidebar {
    width: 240px !important;
    transform: translateX(-100%);
    transition: transform 0.25s ease;
    box-shadow: 4px 0 24px rgba(0, 0, 0, 0.3);
  }

  .app-sidebar.mobile-open {
    transform: translateX(0);
  }

  .sidebar-footer {
    display: none;
  }
}
</style>
