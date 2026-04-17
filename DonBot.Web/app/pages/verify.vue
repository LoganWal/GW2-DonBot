<template>
  <div>
    <h1 class="page-title">GW2 Account Verification</h1>

    <div class="verify-grid">
      <!-- Linked accounts -->
      <Card>
        <template #title>Linked Accounts</template>
        <template #content>
          <ProgressSpinner v-if="accountsPending" style="width: 2rem; height: 2rem;" />
          <div v-else-if="!accounts?.length" style="color: var(--p-text-muted-color); font-size: 0.9rem;">
            No GW2 accounts linked yet.
          </div>
          <div v-else style="display: flex; flex-direction: column; gap: 0.5rem;">
            <div v-for="acc in accounts" :key="acc.guildWarsAccountId" class="account-row">
              <span class="account-name">{{ acc.guildWarsAccountName }}</span>
              <Button
                icon="pi pi-trash"
                severity="danger"
                text
                size="small"
                :loading="removing === acc.guildWarsAccountId"
                @click="removeAccount(acc.guildWarsAccountId)"
              />
            </div>
          </div>
        </template>
      </Card>

      <!-- Add account -->
      <Card>
        <template #title>Link a GW2 Account</template>
        <template #content>
          <p style="font-size: 0.875rem; color: var(--p-text-muted-color); margin-bottom: 1rem; line-height: 1.5;">
            Create an API key at
            <a href="https://account.arena.net/applications" target="_blank" rel="noopener" style="color: var(--p-primary-color);">account.arena.net/applications</a>
            with at least the <strong>account</strong> permission, then paste it below.
          </p>
          <div style="display: flex; flex-direction: column; gap: 0.75rem;">
            <InputText
              v-model="apiKey"
              placeholder="Paste your GW2 API key"
              style="width: 100%; font-family: monospace; font-size: 0.8rem;"
              :disabled="verifying"
            />
            <Button
              label="Verify & Link"
              icon="pi pi-check"
              :loading="verifying"
              :disabled="!apiKey.trim()"
              @click="verify"
            />
          </div>
          <Message v-if="errorMsg" severity="error" :closable="true" style="margin-top: 0.75rem;" @close="errorMsg = ''">
            {{ errorMsg }}
          </Message>
          <Message v-if="successMsg" severity="success" :closable="true" style="margin-top: 0.75rem;" @close="successMsg = ''">
            {{ successMsg }}
          </Message>
        </template>
      </Card>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const api = useApi()

const apiKey = ref('')
const verifying = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const removing = ref<string | null>(null)

const { data: accounts, pending: accountsPending, refresh: refreshAccounts } = await useAsyncData(
  'gw2-accounts',
  () => api('/api/account/gw2') as Promise<{ guildWarsAccountId: string; guildWarsAccountName: string }[]>
)

const verify = async () => {
  errorMsg.value = ''
  successMsg.value = ''
  verifying.value = true
  try {
    const result = await api('/api/account/verify', {
      method: 'POST',
      body: { apiKey: apiKey.value.trim() },
    }) as { accountName: string; isNew: boolean }
    successMsg.value = result.isNew
      ? `Linked ${result.accountName} successfully.`
      : `Updated ${result.accountName} successfully.`
    apiKey.value = ''
    await refreshAccounts()
  } catch {
    errorMsg.value = 'Invalid API key or verification failed. Check the key and try again.'
  } finally {
    verifying.value = false
  }
}

const removeAccount = async (accountId: string) => {
  removing.value = accountId
  try {
    await api(`/api/account/gw2/${accountId}`, { method: 'DELETE' })
    await refreshAccounts()
  } finally {
    removing.value = null
  }
}
</script>

<style scoped>
.verify-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 1.5rem;
}

.account-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.4rem 0;
  border-bottom: 1px solid var(--p-surface-border);
}

.account-row:last-child {
  border-bottom: none;
}

.account-name {
  font-size: 0.9rem;
  font-family: monospace;
}
</style>
