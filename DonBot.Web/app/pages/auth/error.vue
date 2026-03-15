<template>
  <div style="display: flex; justify-content: center; align-items: center; min-height: 60vh;">
    <Card style="width: 400px; text-align: center;">
      <template #title>Authentication Error</template>
      <template #content>
        <Message severity="error" :closable="false" style="margin-bottom: 1rem;">
          {{ reason }}
        </Message>
        <NuxtLink to="/">
          <Button label="Return to Home" icon="pi pi-home" />
        </NuxtLink>
      </template>
    </Card>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const reason = computed(() => route.query.reason ?? 'Something went wrong during login.')
</script>
