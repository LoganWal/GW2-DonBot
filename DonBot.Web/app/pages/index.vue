<template>
  <div style="display: flex; justify-content: center; align-items: center; min-height: 60vh;">
    <Card style="width: 400px; text-align: center;">
      <template #title>DonBot</template>
      <template #subtitle>GW2 Fight Logs & Stats</template>
      <template #content>
        <div v-if="user">
          <p style="margin-bottom: 1rem;">Welcome back, <strong>{{ user.username }}</strong>!</p>
          <NuxtLink to="/dashboard">
            <Button label="Go to Dashboard" icon="pi pi-home" />
          </NuxtLink>
        </div>
        <Button
          v-else
          label="Login with Discord"
          icon="pi pi-sign-in"
          @click="navigateToDiscord"
        />
      </template>
    </Card>
  </div>
</template>

<script setup lang="ts">
const config = useRuntimeConfig()
const { user, fetchMe } = useAuth()

onMounted(() => {
  fetchMe()
})

const navigateToDiscord = () => {
  window.location.href = `${config.public.apiBase}/auth/discord`
}
</script>
