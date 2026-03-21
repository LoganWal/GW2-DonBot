<template>
  <header class="app-topbar">
    <div class="topbar-start">
      <Button
        icon="pi pi-bars"
        text
        severity="secondary"
        class="topbar-hamburger"
        aria-label="Toggle navigation"
        @click="toggleMobile"
      />
      <span class="topbar-title">{{ pageTitle }}</span>
    </div>
    <div class="topbar-end">
      <Button
        :icon="dark ? 'pi pi-sun' : 'pi pi-moon'"
        text
        severity="secondary"
        aria-label="Toggle theme"
        @click="toggleTheme"
      />
      <template v-if="user">
        <div class="topbar-divider" />
        <Avatar
          :label="user.username.charAt(0).toUpperCase()"
          shape="circle"
          class="user-avatar"
        />
        <span class="user-name">{{ user.username }}</span>
        <Button label="Logout" severity="secondary" size="small" outlined @click="logout" />
      </template>
      <Button
        v-else
        label="Login with Discord"
        icon="pi pi-sign-in"
        @click="navigateToDiscord"
      />
    </div>
  </header>
</template>

<script setup lang="ts">
const config = useRuntimeConfig()
const { user, logout } = useAuth()
const { toggleMobile } = useSidebar()
const pageTitle = usePageTitle()

useHead(() => ({ title: `${pageTitle.value} · DonBot` }))
const { dark, toggle: toggleTheme } = useTheme()

const navigateToDiscord = () => {
  window.location.href = `${config.public.apiBase}/auth/discord`
}
</script>

<style scoped>
.app-topbar {
  position: sticky;
  top: 0;
  z-index: 50;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1.5rem;
  height: 60px;
  background: var(--p-surface-ground);
  border-bottom: 1px solid var(--p-surface-border);
  gap: 1rem;
  isolation: isolate;
}

.topbar-start {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.topbar-end {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.topbar-hamburger {
  display: none;
}

.topbar-title {
  font-size: 1.05rem;
  font-weight: 600;
  color: var(--p-text-color);
}

.topbar-divider {
  width: 1px;
  height: 1.5rem;
  background: var(--p-surface-border);
  margin: 0 0.25rem;
}

.user-avatar {
  width: 2rem;
  height: 2rem;
  font-size: 0.8rem;
  font-weight: 600;
  background: var(--p-primary-color);
  color: #fff;
}

.user-name {
  font-size: 0.9rem;
  font-weight: 500;
  color: var(--p-text-color);
}

@media (max-width: 768px) {
  .topbar-hamburger {
    display: inline-flex;
  }
}

@media (max-width: 480px) {
  .user-name {
    display: none;
  }
}
</style>
