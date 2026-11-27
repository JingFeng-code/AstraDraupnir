<script setup lang="ts">
import type { User } from '~/types/api'
import { toApiError, type ApiError } from '~/utils/errors'
import { canManageUsers } from '~/utils/permissions'

interface AuditEntry {
  id: string
  actorUserId: string | null
  action: string
  targetType: string
  targetId: string | null
  detail: string
  createdAt: string
}

const auth = useAuth()
const api = useApi()
const entries = ref<AuditEntry[]>([])
const users = ref<User[]>([])
const loading = ref(true)
const error = ref<ApiError | null>(null)
const admin = computed(() => canManageUsers(auth.state.value.user))

const actor = ref('')
const action = ref('')
const from = ref('')
const to = ref('')
const search = ref('')
const page = ref(1)
const pageSize = 10

const absoluteTime = (iso: string) => new Date(iso).toLocaleString('zh-TW', {
  year: 'numeric', month: '2-digit', day: '2-digit',
  hour: '2-digit', minute: '2-digit', hour12: false,
})
const actorLabel = (id: string | null) => id ? (users.value.find(u => u.id === id)?.email ?? id) : '未登入（未知帳號）'
const actions = computed(() => [...new Set(entries.value.map(e => e.action))].sort())

const filtered = computed(() => {
  const term = search.value.trim().toLowerCase()
  const fromTime = from.value ? new Date(`${from.value}T00:00:00`).getTime() : null
  const toTime = to.value ? new Date(`${to.value}T23:59:59`).getTime() : null
  return entries.value.filter((entry) => {
    if (actor.value && entry.actorUserId !== actor.value) return false
    if (action.value && entry.action !== action.value) return false
    const at = new Date(entry.createdAt).getTime()
    if (fromTime !== null && at < fromTime) return false
    if (toTime !== null && at > toTime) return false
    if (!term) return true
    return [entry.action, entry.targetType, entry.detail, actorLabel(entry.actorUserId)]
      .join(' ').toLowerCase().includes(term)
  })
})
const pages = computed(() => Math.max(1, Math.ceil(filtered.value.length / pageSize)))
const visible = computed(() => filtered.value.slice((page.value - 1) * pageSize, page.value * pageSize))
const hasFilters = computed(() => !!(actor.value || action.value || from.value || to.value || search.value))

watch([actor, action, from, to, search], () => { page.value = 1 })
watch(pages, (count) => { if (page.value > count) page.value = count })

function resetFilters() {
  actor.value = ''
  action.value = ''
  from.value = ''
  to.value = ''
  search.value = ''
  page.value = 1
}

async function load() {
  if (!admin.value) { loading.value = false; return }
  loading.value = true
  error.value = null
  try {
    const [log, list] = await Promise.all([
      api.get<AuditEntry[]>('/api/audit'),
      api.get<User[]>('/api/users'),
    ])
    entries.value = log
    users.value = list
  } catch (caught) {
    error.value = toApiError(caught)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  if (!auth.state.value.ready) await auth.fetchMe()
  await load()
})
</script>

<template>
  <div>
    <h1 class="ui-h1">稽核紀錄</h1>
    <p class="ui-lead">誰在什麼時候做了什麼。紀錄由 Draupnir API 提供，不是從 Supervisor 匯入的快照。</p>

    <AppNotice v-if="!admin" class="audit-notice" tone="info" message="沒有權限：只有 Admin 可以檢視稽核紀錄。" />

    <template v-else>
      <AppLoading v-if="loading" label="正在讀取稽核紀錄…" />
      <AppNotice v-else-if="error" :message="error.detail" :details="Object.values(error.fields).flat()">
        <button type="button" class="btn btn-secondary" @click="load">重試</button>
      </AppNotice>

      <template v-else>
        <div class="ui-callout audit-source">
          <p class="ui-h3">API 只回傳最近 200 筆</p>
          <p class="ui-meta">共 {{ entries.length }} 筆可查；篩選與分頁在本機進行。時間以絕對時間顯示。</p>
        </div>

        <section class="ui-panel audit-panel">
          <div class="ui-panel-h">
            <h2 class="ui-h2">篩選</h2>
            <button v-if="hasFilters" type="button" class="btn btn-secondary" @click="resetFilters">清除篩選</button>
          </div>
          <div class="ui-panel-b audit-filters">
            <div>
              <label for="audit-actor" class="field-label">操作者</label>
              <select id="audit-actor" v-model="actor" class="field-input">
                <option value="">全部操作者</option>
                <option v-for="u in users" :key="u.id" :value="u.id">{{ u.email }}</option>
              </select>
            </div>
            <div>
              <label for="audit-action" class="field-label">動作</label>
              <select id="audit-action" v-model="action" class="field-input">
                <option value="">全部動作</option>
                <option v-for="a in actions" :key="a" :value="a">{{ a }}</option>
              </select>
            </div>
            <div>
              <label for="audit-from" class="field-label">起始日期</label>
              <input id="audit-from" v-model="from" type="date" class="field-input">
            </div>
            <div>
              <label for="audit-to" class="field-label">結束日期</label>
              <input id="audit-to" v-model="to" type="date" class="field-input">
            </div>
            <div class="audit-search">
              <label for="audit-search" class="field-label">搜尋</label>
              <input id="audit-search" v-model="search" type="search" class="field-input" placeholder="動作、目標或內容">
            </div>
          </div>
        </section>

        <section class="ui-panel audit-panel">
          <div class="ui-panel-h">
            <h2 class="ui-h2">紀錄 · {{ filtered.length }} 筆</h2>
            <span class="ui-meta">第 {{ page }} / {{ pages }} 頁</span>
          </div>
          <AppEmpty v-if="!filtered.length" title="沒有符合條件的紀錄" description="試著清除篩選，或確認 API 是否已有操作紀錄。">
            <button v-if="hasFilters" type="button" class="btn btn-secondary" @click="resetFilters">清除篩選</button>
          </AppEmpty>
          <table v-else class="ui-table">
            <thead>
              <tr>
                <th scope="col">時間</th>
                <th scope="col">操作者</th>
                <th scope="col">動作</th>
                <th scope="col">目標</th>
                <th scope="col">內容</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="entry in visible" :key="entry.id">
                <td class="ui-wide audit-time">{{ absoluteTime(entry.createdAt) }}</td>
                <td>{{ actorLabel(entry.actorUserId) }}</td>
                <td><span class="ui-tag">{{ entry.action }}</span></td>
                <td>{{ entry.targetType }}<span v-if="entry.targetId" class="ui-meta audit-target"> · {{ entry.targetId }}</span></td>
                <td class="ui-audit-detail">{{ entry.detail || '—' }}</td>
              </tr>
            </tbody>
          </table>
          <div v-if="filtered.length" class="audit-pager">
            <button type="button" class="btn btn-secondary" :disabled="page <= 1" @click="page--">上一頁</button>
            <button type="button" class="btn btn-secondary" :disabled="page >= pages" @click="page++">下一頁</button>
          </div>
        </section>
      </template>
    </template>
  </div>
</template>

<style scoped>
.audit-notice, .audit-source, .audit-panel { margin-top: 24px; }
.audit-filters { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; }
.audit-search { grid-column: 1 / -1; }
.audit-time { white-space: nowrap; }
.audit-target { display: block; max-width: 100%; font-size: 13px; overflow-wrap: anywhere; }
.ui-audit-detail { overflow-wrap: anywhere; }
.audit-pager { display: flex; justify-content: flex-end; gap: 10px; padding: 16px 22px; }
@media (max-width: 720px) {
  .audit-filters { grid-template-columns: 1fr; }
  .audit-search { grid-column: auto; }
  .audit-pager { justify-content: center; }
}
</style>
