<template>
  <div :class="['layout-wrapper', { 'sidebar-collapsed': collapsed }]">
    <Teleport to="body">
      <div v-if="mobileOpen" class="sidebar-backdrop" @click="closeMobile" />
    </Teleport>

    <Toast position="bottom-right" />
    <AppSidebar />

    <div class="layout-body">
      <AppHeader />
      <main class="layout-content">
        <slot />
      </main>
    </div>

    <Teleport to="body">
      <Transition name="cookie-slide">
        <div v-if="showCookieBanner" class="cookie-banner">
          <span class="cookie-text">{{ cookieText }}</span>
          <div style="display: flex; gap: 0.5rem; flex-shrink: 0;">
            <Button size="small" label="I Accept (I have no choice)" severity="secondary" @click="showCookieBanner = false" />
            <Button size="small" :label="declineLabel" severity="danger" :disabled="declined" @click="onDecline" />
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
const { collapsed, mobileOpen, closeMobile } = useSidebar()
const { user } = useAuth()

const showCookieBanner = ref(false)
const declined = ref(false)
const declineLabel = ref('I Decline')
const cookieText = ref('We use cookies. Actually, we use your soul. By continuing to use this site you agree to sell your soul, your GW2 account, and any future GW2 accounts to DonBot LLC (not a real company). You have no choice. GDPR does not apply here because we said so.')

watch(user, (u) => {
  if (u?.showCookieBanner) showCookieBanner.value = true
}, { immediate: true })

const onDecline = () => {
  declined.value = true
  declineLabel.value = 'Processing decline...'
  setTimeout(() => {
    cookieText.value = 'Decline registered. Consent revoked. Notifying EU authorities... just kidding. You already accepted. Enjoy your cookies.'
    declineLabel.value = 'I Decline (too late)'
    setTimeout(() => { showCookieBanner.value = false }, 3000)
  }, 800)
}
</script>

<style scoped>
.cookie-banner {
  position: fixed;
  bottom: 1.5rem;
  left: 50%;
  transform: translateX(-50%);
  z-index: 200;
  display: flex;
  align-items: center;
  gap: 1rem;
  max-width: 700px;
  width: calc(100% - 3rem);
  background: rgba(15, 15, 20, 0.95);
  backdrop-filter: blur(8px);
  border: 1px solid var(--p-primary-color);
  border-radius: 0.75rem;
  padding: 0.75rem 1.25rem;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.8);
}

.cookie-text {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
  line-height: 1.4;
}

/* noinspection CssUnusedSymbol */
.cookie-slide-enter-active,
/* noinspection CssUnusedSymbol */
.cookie-slide-leave-active {
  transition: opacity 0.3s, transform 0.3s;
}

/* noinspection CssUnusedSymbol */
.cookie-slide-enter-from,
/* noinspection CssUnusedSymbol */
.cookie-slide-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(1rem);
}
</style>
