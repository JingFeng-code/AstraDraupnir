<script setup lang="ts">
import type { HumanGate } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { formatDateTime } from '~/utils/format'
import { gateStatusLabels } from '~/utils/labels'
import { canApproveGates } from '~/utils/permissions'

const route = useRoute()
const api = useApi()
const auth = useAuth()
const admin = computed(() => canApproveGates(auth.state.value.user))
const id = computed(() => String(route.params.id))

const gates = ref<HumanGate[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const actionError = ref<string | null>(null)
const actionDetails = ref<string[]>([])

const dialogOpen = ref(false)
const pendingGate = ref<HumanGate | null>(null)
const decision = ref<'approve' | 'reject'>('approve')
const note = ref('')
const submitting = ref(false)

async function load() {
  loading.value = true
  error.value = null
  try {
    gates.value = await api.get<HumanGate[]>(`/api/projects/${id.value}/gates`)
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

function ask(gate: HumanGate, choice: 'approve' | 'reject') {
  actionError.value = null
  actionDetails.value = []
  pendingGate.value = gate
  decision.value = choice
  note.value = ''
  dialogOpen.value = true
}

async function confirmDecision() {
  if (!pendingGate.value || submitting.value) return
  submitting.value = true
  actionError.value = null
  try {
    await api.post(`/api/gates/${pendingGate.value.id}/decision`, {
      approved: decision.value === 'approve',
      note: note.value.trim() || null,
    })
    dialogOpen.value = false
    await load()
  } catch (caught) {
    const apiError = toApiError(caught)
    actionError.value = apiError.detail
    actionDetails.value = Object.values(apiError.fields).flat()
    dialogOpen.value = false
  } finally {
    submitting.value = false
  }
}

watch(id, load, { immediate: true })
</script>

<template>
  <div class="site-container py-6">
    <h2 class="text-xl font-bold text-ink">Human Gate</h2>
    <p class="text-sm text-ink-muted">只有已登入的管理者可以批准或駁回；決定後不可重複變更。</p>

    <AppNotice v-if="actionError" class="mt-4" :message="actionError" :details="actionDetails" data-testid="gate-error" />

    <AppLoading v-if="loading" />
    <AppNotice v-else-if="error" class="mt-4" :message="error.detail" />
    <AppEmpty v-else-if="!gates.length" class="mt-4" title="還沒有 Human Gate" description="匯入需要人工確認的 Supervisor 任務後，Gate 會出現在這裡。" />
    <div v-else class="table-wrap panel mt-4">
      <table class="w-full text-left text-sm">
        <thead class="bg-surface-muted text-xs text-ink-subtle">
          <tr>
            <th class="px-4 py-2 font-medium">標題</th>
            <th class="px-4 py-2 font-medium">狀態</th>
            <th class="px-4 py-2 font-medium">來源</th>
            <th class="px-4 py-2 font-medium">決定時間</th>
            <th class="px-4 py-2 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="gate in gates" :key="gate.id" class="border-t border-line" :data-testid="`gate-row-${gate.id}`">
            <td class="px-4 py-2">
              <p class="font-medium text-ink">{{ gate.title }}</p>
              <p v-if="gate.note" class="text-xs text-ink-muted">備註：{{ gate.note }}</p>
            </td>
            <td class="px-4 py-2"><StatusBadge :value="gateStatusLabels[gate.status]" /></td>
            <td class="px-4 py-2 font-mono text-xs text-ink-muted">{{ gate.externalRef ?? '手動建立' }}</td>
            <td class="px-4 py-2 text-xs text-ink-muted">{{ formatDateTime(gate.decidedAt) }}</td>
            <td class="px-4 py-2">
              <div v-if="gate.status === 'Pending' && admin" class="flex flex-wrap gap-2">
                <button type="button" class="btn btn-primary" :data-testid="`gate-approve-${gate.id}`" @click="ask(gate, 'approve')">批准</button>
                <button type="button" class="btn btn-danger" :data-testid="`gate-reject-${gate.id}`" @click="ask(gate, 'reject')">駁回</button>
              </div>
              <span v-else-if="gate.status !== 'Pending'" class="text-xs text-ink-subtle">已決定</span>
              <span v-else class="text-xs text-ink-subtle">需要 Admin</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <ConfirmDialog
      v-model:open="dialogOpen"
      :title="decision === 'approve' ? '批准這個 Human Gate？' : '駁回這個 Human Gate？'"
      :description="pendingGate ? `「${pendingGate.title}」的決定會立刻生效，且不能重複變更。` : ''"
      :confirm-label="decision === 'approve' ? '確認批准' : '確認駁回'"
      :tone="decision === 'approve' ? 'primary' : 'danger'"
      :busy="submitting"
      @confirm="confirmDecision"
    >
      <label for="gate-note" class="field-label mt-4">備註（選填）</label>
      <textarea id="gate-note" v-model="note" rows="3" class="field-input" data-testid="gate-note" />
    </ConfirmDialog>
  </div>
</template>
