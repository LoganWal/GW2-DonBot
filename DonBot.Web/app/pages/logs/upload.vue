<template>
  <div>
    <div style="display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem;">
      <Button icon="pi pi-arrow-left" severity="secondary" text @click="navigateTo('/logs')" />
      <h1 class="page-title" style="margin: 0;">Upload Logs</h1>
    </div>

    <div class="wingman-toggle">
      <ToggleSwitch v-model="uploadToWingman" inputId="wingman-toggle" />
      <label for="wingman-toggle" style="font-size: 0.875rem; cursor: pointer;">Submit to Wingman</label>
    </div>

    <div class="input-panels">
      <!-- URL paste panel -->
      <div class="input-panel">
        <h2 class="panel-title"><i class="pi pi-link" /> Paste URLs</h2>
        <p class="panel-hint">Paste dps.report or wvw.report URLs, one per line.</p>
        <Textarea
          v-model="urlInput"
          :rows="6"
          placeholder="https://dps.report/ABCD&#10;https://wvw.report/EFGH"
          style="width: 100%; font-size: 0.8rem; font-family: monospace; resize: vertical;"
          :disabled="submittingUrls"
        />
        <Button
          label="Process URLs"
          icon="pi pi-play"
          :loading="submittingUrls"
          :disabled="!urlInput.trim()"
          style="margin-top: 0.5rem; width: 100%;"
          @click="submitUrls"
        />
      </div>

      <!-- File upload panel -->
      <div class="input-panel">
        <h2 class="panel-title"><i class="pi pi-file" /> Upload .zevtc Files</h2>
        <p class="panel-hint">Drag and drop or click to select .zevtc combat log files.</p>
        <div
          class="drop-zone"
          :class="{ 'drop-zone--active': isDragging }"
          @dragover.prevent="isDragging = true"
          @dragleave.prevent="isDragging = false"
          @drop.prevent="onDrop"
          @click="fileInput?.click()"
        >
          <i class="pi pi-upload" style="font-size: 1.5rem; color: var(--p-text-muted-color);" />
          <p style="margin: 0.25rem 0 0; color: var(--p-text-muted-color); font-size: 0.85rem;">
            Drop .zevtc files here or click to browse
          </p>
          <p style="margin: 0.15rem 0 0; font-size: 0.75rem; color: var(--p-text-muted-color);">Multiple files supported</p>
          <input ref="fileInput" type="file" multiple accept=".zevtc" style="display: none;" @change="onFileInputChange" />
        </div>
      </div>
    </div>

    <!-- Active uploads -->
    <div v-if="uploads.length > 0" style="margin-top: 1.5rem;">
      <div class="uploads-header" @click="uploadsExpanded = !uploadsExpanded">
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <i :class="uploadsExpanded ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" style="font-size: 0.75rem; color: var(--p-text-muted-color);" />
          <span style="font-size: 0.95rem; font-weight: 600;">Uploads</span>
          <span style="font-size: 0.8rem; color: var(--p-text-muted-color);">
            {{ uploads.length }} total
            <template v-if="uploadsDoneCount > 0">&nbsp;&middot;&nbsp;{{ uploadsDoneCount }} complete</template>
            <template v-if="uploadsFailedCount > 0">&nbsp;&middot;&nbsp;<span style="color: var(--p-red-400);">{{ uploadsFailedCount }} failed</span></template>
          </span>
        </div>
      </div>

      <div v-if="uploadsExpanded" style="display: flex; flex-direction: column; gap: 0.75rem; margin-top: 0.75rem;">
        <div v-for="upload in pagedUploads" :key="upload.localId" class="upload-card">
          <div class="upload-card__header">
            <span class="upload-card__name">{{ upload.fileName }}</span>
            <Tag :severity="statusSeverity(upload.status)" :value="statusLabel(upload.status)" />
          </div>

          <div class="upload-card__stages">
            <template v-for="stage in visibleStages(upload.sourceType)" :key="stage.key">
              <div class="stage-step" :class="stageClass(upload, stage.key)">
                <i :class="stageIcon(upload, stage.key)" />
                <span>{{ stage.label }}</span>
              </div>
              <i class="pi pi-angle-right stage-sep" />
            </template>
          </div>

          <div v-if="upload.message && !['complete', 'failed'].includes(upload.status)" class="upload-card__message">
            {{ upload.message }}
          </div>

          <div v-if="upload.status === 'complete'" class="upload-card__links">
            <a v-if="upload.dpsReportUrl" :href="upload.dpsReportUrl" target="_blank" rel="noopener" class="upload-link">
              <i class="pi pi-external-link" /> dps.report
            </a>
            <a v-if="upload.fightLogId" :href="`/logs/${upload.fightLogId}`" class="upload-link">
              <i class="pi pi-chart-bar" /> View log
            </a>
          </div>

          <div v-if="upload.status === 'failed'" class="upload-card__error">
            {{ upload.message }}
          </div>
        </div>

        <Paginator
          v-if="sortedUploads.length > uploadsPageSize"
          :rows="uploadsPageSize"
          :total-records="sortedUploads.length"
          :first="(uploadsPage - 1) * uploadsPageSize"
          @page="e => { uploadsPage = e.page + 1 }"
        />
      </div>
    </div>

    <!-- History -->
    <div v-if="historyTotal > 0 || history.length > 0" style="margin-top: 2rem;">
      <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.75rem; flex-wrap: wrap; gap: 0.5rem;">
        <h2 style="font-size: 1.1rem; margin: 0;">Previous Uploads <span style="font-size: 0.8rem; font-weight: 400; color: var(--p-text-muted-color);">(last 24 hours)</span></h2>
        <Button
          label="Submit All to Wingman"
          icon="pi pi-send"
          size="small"
          severity="secondary"
          :loading="submittingBulkWingman"
          @click="submitBulkToWingman"
        />
      </div>
      <DataTable :value="history" size="small" striped-rows>
        <Column header="Source">
          <template #body="{ data }">
            <Tag :value="data.sourceType === 'url' ? 'URL' : 'File'" severity="secondary" />
          </template>
        </Column>
        <Column header="Name" field="fileName" />
        <Column header="Date">
          <template #body="{ data }">{{ new Date(data.createdAt).toLocaleString() }}</template>
        </Column>
        <Column header="Links">
          <template #body="{ data }">
            <div style="display: flex; gap: 0.75rem; align-items: center;">
              <a v-if="data.dpsReportUrl" :href="data.dpsReportUrl" target="_blank" rel="noopener" class="upload-link">dps.report</a>
              <a v-if="data.fightLogId" :href="`/logs/${data.fightLogId}`" class="upload-link">View log</a>
            </div>
          </template>
        </Column>
        <Column header="">
          <template #body="{ data }">
            <Button
              v-if="data.dpsReportUrl"
              v-tooltip.left="'Submit to Wingman'"
              icon="pi pi-send"
              text
              size="small"
              severity="secondary"
              :loading="wingmanPending.has(data.logUploadId)"
              @click="submitOneToWingman(data.logUploadId)"
            />
          </template>
        </Column>
      </DataTable>
      <Paginator
        v-if="historyTotal > historyPageSize"
        :rows="historyPageSize"
        :total-records="historyTotal"
        :first="(historyPage - 1) * historyPageSize"
        style="margin-top: 0.5rem;"
        @page="e => { historyPage = e.page + 1 }"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const config = useRuntimeConfig()
const api = useApi()
const toast = useToast()
const fileInput = ref<HTMLInputElement | null>(null)
const isDragging = ref(false)
const urlInput = ref('')
const submittingUrls = ref(false)
const uploadToWingman = ref(true)
const wingmanPending = ref(new Set<number>())
const submittingBulkWingman = ref(false)

interface UploadEntry {
  localId: number
  fileName: string
  sourceType: 'file' | 'url'
  status: string
  message: string
  dpsReportUrl: string | null
  fightLogId: number | null
  stageReached: string
}

const FILE_STAGES = [
  { key: 'stored', label: 'Stored' },
  { key: 'parsing', label: 'Parsing' },
  { key: 'uploading', label: 'Uploading' },
  { key: 'saving', label: 'Saving' },
  { key: 'complete', label: 'Done' },
]

const URL_STAGES = [
  { key: 'parsing', label: 'Fetching' },
  { key: 'saving', label: 'Saving' },
  { key: 'complete', label: 'Done' },
]

const STAGE_ORDER = ['stored', 'parsing', 'uploading', 'saving', 'complete']

let nextLocalId = 0
const uploads = ref<UploadEntry[]>([])
const uploadsExpanded = ref(true)
const uploadsPage = ref(1)
const uploadsPageSize = 10

const historyPage = ref(1)
const historyPageSize = 20

const { data: historyData, refresh: refreshHistory } = await useAsyncData(
  'upload-history',
  () => api(`/api/upload/history?page=${historyPage.value}&pageSize=${historyPageSize}`) as Promise<{ total: number; page: number; pageSize: number; items: any[] }>,
  { watch: [historyPage] }
)
const history = computed(() => historyData.value?.items ?? [])
const historyTotal = computed(() => historyData.value?.total ?? 0)

const uploadSortWeight = (u: UploadEntry) => {
  if (u.status === 'failed') return STAGE_ORDER.length + 1
  if (u.status === 'complete') return STAGE_ORDER.length
  return STAGE_ORDER.indexOf(u.stageReached)
}

const sortedUploads = computed(() =>
  [...uploads.value].sort((a, b) => uploadSortWeight(a) - uploadSortWeight(b))
)

const pagedUploads = computed(() => {
  const start = (uploadsPage.value - 1) * uploadsPageSize
  return sortedUploads.value.slice(start, start + uploadsPageSize)
})

const uploadsDoneCount = computed(() => uploads.value.filter(u => u.status === 'complete').length)
const uploadsFailedCount = computed(() => uploads.value.filter(u => u.status === 'failed').length)

const visibleStages = (sourceType: 'file' | 'url') =>
  sourceType === 'url' ? URL_STAGES : FILE_STAGES

const submitUrls = async () => {
  const lines = urlInput.value.split('\n').map(l => l.trim()).filter(Boolean)
  if (lines.length === 0) return
  submittingUrls.value = true
  try {
    const created: { logUploadId: number; fileName: string; sourceType: 'url' }[] = await $fetch('/api/upload/urls', {
      baseURL: config.public.apiBase,
      method: 'POST',
      body: { urls: lines, wingman: uploadToWingman.value },
      credentials: 'include'
    })
    urlInput.value = ''
    for (const item of created) addUploadEntry(item)
  } catch (err) {
    console.error('URL submit failed', err)
  } finally {
    submittingUrls.value = false
  }
}

const onDrop = (e: DragEvent) => {
  isDragging.value = false
  handleFiles(Array.from(e.dataTransfer?.files ?? []))
}

const onFileInputChange = (e: Event) => {
  handleFiles(Array.from((e.target as HTMLInputElement).files ?? []))
  if (fileInput.value) fileInput.value.value = ''
}

const handleFiles = async (files: File[]) => {
  const valid = files.filter(f => f.name.toLowerCase().endsWith('.zevtc'))
  if (valid.length === 0) return

  const formData = new FormData()
  for (const f of valid) formData.append('file', f, f.name)

  try {
    const created: { logUploadId: number; fileName: string; sourceType: 'file' }[] = await $fetch(`/api/upload/files?wingman=${uploadToWingman.value}`, {
      baseURL: config.public.apiBase,
      method: 'POST',
      body: formData,
      credentials: 'include'
    })
    for (const item of created) addUploadEntry(item)
  } catch (err) {
    console.error('File upload failed', err)
  }
}

const addUploadEntry = (item: { logUploadId: number; fileName: string; sourceType: 'file' | 'url' }) => {
  const localId = nextLocalId++
  uploads.value.unshift({
    localId,
    fileName: item.fileName,
    sourceType: item.sourceType,
    status: item.sourceType === 'file' ? 'stored' : 'pending',
    message: '',
    dpsReportUrl: null,
    fightLogId: null,
    stageReached: item.sourceType === 'file' ? 'stored' : 'pending'
  })
  uploadsPage.value = 1
  subscribeToProgress(item.logUploadId, localId)
}

const subscribeToProgress = (uploadId: number, localId: number) => {
  const url = `${config.public.apiBase}/api/upload/stream/${uploadId}`
  const es = new EventSource(url, { withCredentials: true })

  es.onmessage = (e) => {
    try {
      const data = JSON.parse(e.data)
      const entry = uploads.value.find(u => u.localId === localId)
      if (!entry) return

      entry.status = data.stage
      entry.message = data.message ?? ''
      if (data.dpsReportUrl) entry.dpsReportUrl = data.dpsReportUrl
      if (data.fightLogId) entry.fightLogId = data.fightLogId

      const idx = STAGE_ORDER.indexOf(data.stage)
      const reachedIdx = STAGE_ORDER.indexOf(entry.stageReached)
      if (idx > reachedIdx) entry.stageReached = data.stage

      if (data.stage === 'complete' || data.stage === 'failed') {
        es.close()
        if (data.stage === 'complete') refreshHistory()
      }
    } catch {}
  }

  es.onerror = () => es.close()
}

const submitOneToWingman = async (logUploadId: number) => {
  wingmanPending.value = new Set([...wingmanPending.value, logUploadId])
  try {
    await $fetch(`/api/upload/wingman/${logUploadId}`, {
      baseURL: config.public.apiBase,
      method: 'POST',
      credentials: 'include'
    })
    toast.add({ severity: 'success', summary: 'Submitted to Wingman', detail: 'Log queued for import.', life: 3000 })
  } catch (err) {
    console.error('Wingman submit failed', err)
    toast.add({ severity: 'error', summary: 'Wingman submission failed', detail: 'Could not submit log to Wingman.', life: 4000 })
  } finally {
    const next = new Set(wingmanPending.value)
    next.delete(logUploadId)
    wingmanPending.value = next
  }
}

const submitBulkToWingman = async () => {
  submittingBulkWingman.value = true
  try {
    const result = await $fetch<{ submitted: number }>('/api/upload/wingman/bulk', {
      baseURL: config.public.apiBase,
      method: 'POST',
      credentials: 'include'
    })
    toast.add({
      severity: 'success',
      summary: 'Submitted to Wingman',
      detail: result.submitted === 0 ? 'No logs to submit.' : `${result.submitted} log${result.submitted === 1 ? '' : 's'} queued for import.`,
      life: 3000
    })
  } catch (err) {
    console.error('Bulk wingman submit failed', err)
    toast.add({ severity: 'error', summary: 'Wingman submission failed', detail: 'Could not submit logs to Wingman.', life: 4000 })
  } finally {
    submittingBulkWingman.value = false
  }
}

const stageClass = (upload: UploadEntry, key: string) => {
  if (upload.status === 'failed') return 'stage-step--pending'
  const keyIdx = STAGE_ORDER.indexOf(key)
  const reachedIdx = STAGE_ORDER.indexOf(upload.stageReached)
  if (keyIdx < reachedIdx) return 'stage-step--done'
  if (key === upload.stageReached && upload.status !== 'complete') return 'stage-step--active'
  if (key === 'complete' && upload.status === 'complete') return 'stage-step--done'
  return 'stage-step--pending'
}

const stageIcon = (upload: UploadEntry, key: string) => {
  const cls = stageClass(upload, key)
  if (cls === 'stage-step--done') return 'pi pi-check-circle'
  if (cls === 'stage-step--active') return 'pi pi-spin pi-spinner'
  return 'pi pi-circle'
}

const statusSeverity = (status: string) => {
  if (status === 'complete') return 'success'
  if (status === 'failed') return 'danger'
  return 'info'
}

const statusLabel = (status: string) => ({
  stored: 'Stored', parsing: 'Parsing', uploading: 'Uploading',
  saving: 'Saving', complete: 'Complete', failed: 'Failed', pending: 'Pending'
}[status] ?? status)
</script>

<style scoped>
.wingman-toggle {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.input-panels {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

@media (max-width: 640px) {
  .input-panels { grid-template-columns: 1fr; }
}

.input-panel {
  padding: 1rem;
  background: var(--p-surface-card);
  border: 1px solid var(--p-surface-border);
  border-radius: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.panel-title {
  font-size: 0.95rem;
  font-weight: 600;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.panel-hint {
  margin: 0;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}

.drop-zone {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.2rem;
  padding: 1.5rem 1rem;
  border: 2px dashed var(--p-surface-border);
  border-radius: 0.5rem;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s;
  flex: 1;
}

.drop-zone:hover,
.drop-zone--active {
  border-color: var(--p-primary-color);
  background: color-mix(in srgb, var(--p-primary-color) 8%, transparent);
}

.uploads-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  cursor: pointer;
  padding: 0.5rem 0;
  user-select: none;
}

.uploads-header:hover span {
  color: var(--p-text-color);
}

.upload-card {
  padding: 1rem;
  background: var(--p-surface-card);
  border: 1px solid var(--p-surface-border);
  border-radius: 0.75rem;
}

.upload-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.75rem;
  gap: 0.5rem;
}

.upload-card__name {
  font-weight: 600;
  font-size: 0.9rem;
  word-break: break-all;
}

.upload-card__stages {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
}

.stage-step {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}

.stage-step--done { color: var(--p-green-500); }
.stage-step--active { color: var(--p-primary-color); font-weight: 600; }

.stage-sep {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
  opacity: 0.4;
}

.upload-card__message {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
  margin-top: 0.25rem;
}

.upload-card__error {
  font-size: 0.8rem;
  color: var(--p-red-400);
  margin-top: 0.25rem;
}

.upload-card__links {
  display: flex;
  gap: 1rem;
  margin-top: 0.5rem;
}

.upload-link {
  font-size: 0.85rem;
  color: var(--p-primary-color);
  text-decoration: none;
  display: flex;
  align-items: center;
  gap: 0.3rem;
}

.upload-link:hover { text-decoration: underline; }
</style>
