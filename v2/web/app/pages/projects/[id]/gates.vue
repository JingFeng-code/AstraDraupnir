<script setup lang="ts">
import type { HumanGate } from '~/types/api'
import { gateEvidence } from '~/lib/evidenceRules'
import { clean, STATUS_TONE } from '~/lib/engineering'
import { canApproveGates } from '~/utils/permissions'
import { toApiError, type ApiError } from '~/utils/errors'

const route = useRoute()
const api = useApi()
const ws = useWorkspace()
const auth = useAuth()
const id = computed(() => String(route.params.id))
const admin = computed(() => canApproveGates(auth.state.value.user))
const gates = computed(() => ws.data.value.gates.filter(g => g.projectId === id.value))
const selectedId = ref('')
const selected = computed(() => gates.value.find(g => g.id === selectedId.value))
const selectedItem = computed(() => selected.value?.workItemId ? ws.byId.value.items.get(selected.value.workItemId) : undefined)
const trace = computed(() => selectedItem.value ? ws.traceOf(selectedItem.value) : null)
const ev = computed(() => selected.value ? gateEvidence(selected.value, trace.value) : null)
function canApprove(gate: HumanGate) { const item = gate.workItemId ? ws.byId.value.items.get(gate.workItemId) : undefined; return gateEvidence(gate, item ? ws.traceOf(item) : null).canApprove }
const loading = computed(() => !ws.data.value.ready || ws.busy.value)
const error = computed(() => ws.error.value)
const actionError = ref<string | null>(null)
const actionDetails = ref<string[]>([])
const dialogOpen = ref(false)
const pending = ref<HumanGate | null>(null)
const decision = ref<'approve' | 'reject'>('approve')
const note = ref('')
const needsManualReason = computed(() => !!pending.value && !pending.value.workItemId)
const exceptionReason = ref('')
const ack = ref(false)
const typed = ref('')
const createOpen = ref(false)
const gateTitle = ref('')
const gateItem = ref('')
const creating = ref(false)

watchEffect(() => {
  if (!gates.value.some(g => g.id === selectedId.value)) selectedId.value = gates.value.find(g => g.status === 'Pending')?.id ?? gates.value[0]?.id ?? ''
})
onMounted(() => { if (!ws.data.value.ready) void ws.load(true) })
function ask(gate: HumanGate, choice: 'approve' | 'reject') {
  actionError.value = null
  pending.value = gate
  decision.value = choice
  note.value = ''
  dialogOpen.value = true
}
async function confirmDecision() {
  if (!pending.value) return
  if (decision.value === 'approve' && !pending.value.workItemId && note.value.trim().length < 20) {
    actionError.value = '沒有對應工作項目的 Gate 必須寫明至少 20 字的人工判斷理由。'
    actionDetails.value = []
    return
  }
  try {
    await ws.decide(pending.value, decision.value === 'approve', note.value.trim())
    dialogOpen.value = false
    await ws.load(true)
  } catch (caught) {
    const apiError = toApiError(caught)
    actionError.value = apiError.detail
    actionDetails.value = Object.values(apiError.fields).flat()
    dialogOpen.value = false
  }
}
async function exceptionDecision() {
  if (!selected.value || exceptionReason.value.trim().length < 20 || !ack.value || typed.value !== '例外') return
  try {
    await ws.decide(selected.value, true, `【人工例外決定：缺少或未通過必要證據】${exceptionReason.value.trim()}`)
    exceptionReason.value = ''
    ack.value = false
    typed.value = ''
    await ws.load(true)
  } catch (caught) {
    const apiError = toApiError(caught)
    actionError.value = apiError.detail
    actionDetails.value = Object.values(apiError.fields).flat()
  }
}
async function createGate() {
  if (creating.value || !gateTitle.value.trim()) return
  creating.value = true
  actionError.value = null
  try {
    await api.post(`/api/projects/${id.value}/gates`, { title: gateTitle.value.trim(), workItemId: gateItem.value || null })
    gateTitle.value = ''
    gateItem.value = ''
    createOpen.value = false
    ws.toast.value = 'Human Gate 已建立'
    await ws.load(true)
  } catch (caught) {
    actionError.value = toApiError(caught).detail
  } finally {
    creating.value = false
  }
}
</script>

<template>
  <div>
    <div style="display: flex; flex-wrap: wrap; justify-content: space-between; align-items: end; gap: 12px">
      <div><h1 class="ui-h1">Human Gate</h1><p class="ui-lead">待決定與已決定的 Gate。必要證據不齊全時不能直接批准；人工例外會被明確記錄。</p></div>
      <button v-if="admin" type="button" class="ui-btn" data-testid="new-gate" @click="createOpen = !createOpen">{{ createOpen ? '收起表單' : '建立 Gate' }}</button>
    </div>
    <p class="ui-meta" style="margin-top: 10px">決定只記錄在 Draupnir，不會同步回 Supervisor。只有 Admin 可以批准或駁回。</p>

    <form v-if="admin && createOpen" class="ui-panel ui-panel-b ui-stack" style="margin-top: 18px" @submit.prevent="createGate">
      <label class="ui-stack" style="gap: 6px"><span class="ui-meta">標題</span><input v-model="gateTitle" required class="ui-input" data-testid="gate-title"></label>
      <label class="ui-stack" style="gap: 6px"><span class="ui-meta">對應工作項目（選填）</span><select v-model="gateItem" class="ui-input"><option value="">沒有對應工作項目（人工判斷）</option><option v-for="item in ws.data.value.items.filter(i => i.projectId === id)" :key="item.id" :value="item.id">{{ item.code }} {{ item.title }}</option></select></label>
      <div style="display: flex; justify-content: flex-end; gap: 8px"><button type="button" class="ui-btn is-quiet" @click="createOpen = false">取消</button><button type="submit" class="ui-btn is-primary" :disabled="creating" data-testid="gate-submit">{{ creating ? '建立中…' : '建立 Gate' }}</button></div>
    </form>

    <p v-if="actionError" class="ui-callout is-fault" style="margin-top: 16px" data-testid="gate-error">{{ actionError }}<span v-if="actionDetails.length" class="ui-meta" style="display: block">{{ actionDetails.join('；') }}</span></p>
    <AppLoading v-if="loading" />
    <div v-else-if="error" class="ui-callout is-fault" style="margin-top: 20px"><p>{{ error }}</p><button type="button" class="ui-btn is-quiet" @click="ws.load(true)">重新載入</button></div>
    <AppEmpty v-else-if="!gates.length" class="ui-panel" style="margin-top: 20px" title="還沒有 Human Gate" description="匯入含 requiresHuman 的 Supervisor 任務，或建立一個 Gate。" />
    <div v-else class="ui-split is-2r" style="margin-top: 20px">
      <section class="ui-panel" aria-labelledby="gate-list-h">
        <div class="ui-panel-h"><h2 id="gate-list-h" class="ui-h2">Gate {{ gates.length }}</h2><span class="ui-source">本機決定，未同步 Supervisor</span></div>
        <div class="ui-table" role="region" aria-label="Human Gate 列表" tabindex="0" style="overflow-x: auto">
          <table class="ui-table"><thead><tr><th>標題</th><th>狀態</th><th>操作</th></tr></thead><tbody>
            <tr v-for="gate in gates" :key="gate.id" :class="{ 'is-on': gate.id === selectedId }" :data-testid="`gate-row-${gate.id}`" @click="selectedId = gate.id">
              <td class="ui-wide"><p class="ui-row-t">{{ clean(gate.title) }}</p><p class="ui-row-m">{{ gate.externalRef ?? '手動建立' }}</p></td>
              <td><UiPill :tone="gate.status === 'Pending' ? 'needs' : gate.status === 'Approved' ? 'proven' : 'fault'" :label="gate.status === 'Pending' ? '待決定' : gate.status === 'Approved' ? '已批准' : '已駁回'" /></td>
              <td>
                <div v-if="gate.status === 'Pending' && admin" style="display: flex; flex-wrap: wrap; gap: 6px">
                  <button v-if="canApprove(gate)" type="button" class="ui-btn is-primary" :data-testid="`gate-approve-${gate.id}`" @click.stop="ask(gate, 'approve')">批准</button>
                  <button type="button" class="ui-btn is-danger" :data-testid="`gate-reject-${gate.id}`" @click.stop="ask(gate, 'reject')">駁回</button>
                  <span v-if="!canApprove(gate)" class="ui-meta">需先補證據</span>
                </div>
                <span v-else-if="gate.status !== 'Pending'" class="ui-meta">已決定</span>
                <span v-else class="ui-meta">需要 Admin</span>
              </td>
            </tr>
          </tbody></table>
        </div>
      </section>

      <section class="ui-panel" aria-labelledby="gate-detail-h">
        <div v-if="selected" class="ui-panel-b" style="padding-top: 22px">
          <p class="ui-meta">{{ selected.externalRef ?? '手動建立' }}</p>
          <h2 id="gate-detail-h" class="ui-h2">{{ clean(selected.title) }}</h2>
          <p v-if="selectedItem" class="ui-meta" style="margin-top: 4px">工作項目 <NuxtLink :to="`/work/${selectedItem.id}`" style="text-decoration: underline">{{ selectedItem.code }}</NuxtLink> · {{ selectedItem.status }}</p>
          <div v-if="ev" class="ui-callout" :class="ev.state === 'satisfied' ? 'is-ok' : ev.state === 'manual' ? '' : 'is-fault'" style="margin-top: 16px">
            <p class="ui-h3" style="display: flex; gap: 8px; align-items: center"><UiMark :tone="ev.state === 'satisfied' ? 'proven' : ev.state === 'manual' ? 'idle' : 'fault'" :size="16" />{{ ev.headline }}</p>
            <ul class="ui-check"><li v-for="check in ev.checks" :key="check.label"><UiMark :tone="check.ok ? 'proven' : 'fault'" :size="14" /><span>{{ check.label }}，{{ check.detail }}</span></li></ul>
            <p class="ui-meta">判斷規則由 Draupnir 推導，不是 Supervisor 的定義。</p>
          </div>

          <template v-if="selected.status === 'Pending' && admin && ev">
            <div v-if="ev.canApprove" style="display: flex; flex-wrap: wrap; gap: 8px; margin-top: 18px"><button type="button" class="ui-btn is-primary" :data-testid="`gate-approve-${selected.id}`" @click="ask(selected, 'approve')">批准</button><button type="button" class="ui-btn is-danger" :data-testid="`gate-reject-${selected.id}`" @click="ask(selected, 'reject')">駁回</button></div>
            <div v-else class="ui-stack" style="margin-top: 18px">
              <p class="ui-callout is-need">不能直接核准：{{ ev.headline }}。請先補齊證據，或使用下方的人工例外。</p>
              <button type="button" class="ui-btn is-danger" @click="ask(selected, 'reject')">駁回</button>
              <div class="ui-panel" style="box-shadow: none; background: var(--c-sunken); padding: 16px">
                <h3 class="ui-h3">人工例外決定</h3><p class="ui-meta">這不是驗證通過，而是由你承擔風險的例外，會被記錄成例外。</p>
                <label class="ui-stack" style="gap: 6px; margin-top: 10px"><span class="ui-meta">例外原因（至少 20 字）</span><textarea v-model="exceptionReason" class="ui-field" style="min-height: 80px" /></label>
                <label style="display: flex; gap: 8px; margin-top: 10px"><input v-model="ack" type="checkbox"> 我知道必要證據缺少或未通過</label>
                <label class="ui-stack" style="gap: 6px; margin-top: 10px"><span class="ui-meta">請輸入「例外」確認</span><input v-model="typed" class="ui-input" autocomplete="off"></label>
                <button type="button" class="ui-btn is-danger" style="margin-top: 10px" :disabled="exceptionReason.trim().length < 20 || !ack || typed !== '例外'" @click="exceptionDecision">以人工例外放行</button>
              </div>
            </div>
          </template>
          <p v-else-if="selected.status === 'Pending' && !admin" class="ui-callout is-need" style="margin-top: 18px">只有 Admin 可以決定這個 Gate。</p>
          <p v-else class="ui-callout is-ok" style="margin-top: 18px">已{{ selected.status === 'Approved' ? '批准' : '駁回' }}<span v-if="selected.note" class="ui-meta" style="display: block">{{ selected.note }}</span></p>
          <p class="ui-meta" style="margin-top: 18px">核准或駁回只會記錄在 Draupnir，系統不會自動同步到 Supervisor。</p>
        </div>
      </section>
    </div>

    <ConfirmDialog v-model:open="dialogOpen" :title="decision === 'approve' ? '批准這個 Human Gate？' : '駁回這個 Human Gate？'" :description="pending ? `「${pending.title}」的決定會立刻生效，且不能重複變更。` : ''" :confirm-label="decision === 'approve' ? '確認批准' : '確認駁回'" :tone="decision === 'approve' ? 'primary' : 'danger'" @confirm="confirmDecision">
      <label for="gate-note" class="ui-meta" style="display: block; margin-top: 14px">{{ needsManualReason && decision === 'approve' ? '人工判斷理由（至少 20 字，必填）' : '備註（選填）' }}</label><textarea id="gate-note" v-model="note" class="ui-field" style="min-height: 80px" data-testid="gate-note" />
    </ConfirmDialog>
  </div>
</template>
