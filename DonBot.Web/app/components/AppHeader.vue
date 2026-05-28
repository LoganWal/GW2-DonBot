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
        icon="pi pi-palette"
        text
        severity="secondary"
        aria-label="Choose theme"
        aria-haspopup="true"
        aria-controls="theme-menu"
        @click="toggleThemeMenu"
      />
      <Popover id="theme-menu" ref="themePopover" class="theme-popover">
        <div class="theme-menu">
          <div class="theme-row">
            <span class="theme-menu-label">Appearance</span>
            <SelectButton
              :model-value="appearance"
              :options="appearanceOptions"
              option-label="label"
              option-value="value"
              :allow-empty="false"
              size="small"
              @update:model-value="setAppearance"
            >
              <template #option="{ option }">
                <i :class="option.icon" aria-hidden="true" />
                <span>{{ option.label }}</span>
              </template>
            </SelectButton>
          </div>
          <div class="theme-row">
            <span class="theme-menu-label">Accent</span>
            <div class="theme-swatches" role="radiogroup" aria-label="Accent theme">
              <button
                v-for="option in accentOptions"
                :key="option.value"
                type="button"
                class="theme-swatch"
                :class="{ selected: accent === option.value }"
                :style="{ '--swatch-color': option.swatch }"
                role="radio"
                :aria-checked="accent === option.value"
                :aria-label="option.label"
                :title="option.label"
                @click="setAccent(option.value)"
              />
            </div>
          </div>
        </div>
      </Popover>
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
const themePopover = ref()

useHead(() => ({ title: `${pageTitle.value} · DonBot` }))
const {
  accent,
  accentOptions,
  appearance,
  appearanceOptions,
  initialize: initializeTheme,
  setAccent,
  setAppearance
} = useTheme()

onMounted(() => initializeTheme())

const toggleThemeMenu = (event: Event) => {
  themePopover.value?.toggle(event)
}

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
  background: rgba(15, 15, 20, 0.2);
  backdrop-filter: blur(8px);
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

.theme-menu {
  display: grid;
  gap: 1rem;
  min-width: min(22rem, calc(100vw - 2rem));
}

.theme-row {
  display: grid;
  gap: 0.5rem;
}

.theme-menu-label {
  color: var(--p-text-muted-color);
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.theme-swatches {
  display: flex;
  gap: 0.5rem;
}

.theme-swatch {
  position: relative;
  width: 2rem;
  height: 2rem;
  border: 1px solid var(--p-surface-border);
  border-radius: 999px;
  background: var(--swatch-color);
  cursor: pointer;
}

.theme-swatch.selected {
  box-shadow: 0 0 0 2px var(--p-surface-ground), 0 0 0 4px var(--p-primary-color);
}

.theme-swatch:focus-visible {
  outline: 2px solid var(--p-primary-color);
  outline-offset: 3px;
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
