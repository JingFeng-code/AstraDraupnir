import type { AgentRun, Evidence, HumanGate, Paged, Project, Requirement, Review, WorkItem, WorkStatus } from '~/types/api'
import { buildTrace, runTone, type Tone, type Trace } from '~/lib/engineering'

interface LabData {
  ready: boolean
  loadedAt: number
  projects: Project[]
  items: WorkItem[]
  requirements: Requirement[]
  runs: AgentRun[]
  evidence: Evidence[]
  reviews: Review[]
  gates: HumanGate[]
  /** work item id -> ids it depends on */
  deps: Record<string, string[]>
}

const empty = (): LabData => ({ ready: false, loadedAt: 0, projects: [], items: [], requirements: [], runs: [], evidence: [], reviews: [], gates: [], deps: {} })
const unwrap = <T>(value: T[] | Paged<T>): T[] => (Array.isArray(value) ? value : value.items)

/**
 * One shared snapshot of the workspace data (projects, work items, runs, evidence, gates), read through the /api proxy.
 */
export function useWorkspace() {
  const data = useState<LabData>('ws-data', empty)
  const error = useState<string | null>('ws-error', () => null)
  const busy = useState<boolean>('ws-busy', () => false)
  const toast = useState<string | null>('ws-toast', () => null)

  async function load(force = false) {
    if (data.value.ready && !force) return
    busy.value = true
    error.value = null
    try {
      const get = <T>(path: string, query?: Record<string, string | number>) => $fetch<T>(path, { credentials: 'include', query })
      const projects = unwrap(await get<Project[] | Paged<Project>>('/api/projects'))
      const per = await Promise.all(projects.map(async (p) => {
        const [items, requirements, gates] = await Promise.all([
          get<Paged<WorkItem>>(`/api/projects/${p.id}/work-items`, { pageSize: 100 }).then(unwrap),
          get<Requirement[] | Paged<Requirement>>(`/api/projects/${p.id}/requirements`).then(unwrap),
          get<HumanGate[] | Paged<HumanGate>>(`/api/projects/${p.id}/gates`).then(unwrap),
        ])
        return { items, requirements, gates }
      }))
      const [runs, evidence, reviews] = await Promise.all([
        get<AgentRun[] | Paged<AgentRun>>('/api/agent-runs').then(unwrap),
        get<Evidence[] | Paged<Evidence>>('/api/evidence').then(unwrap),
        get<Review[] | Paged<Review>>('/api/reviews').then(unwrap),
      ])
      const items = per.flatMap(x => x.items)
      const deps: Record<string, string[]> = {}
      await Promise.all(items.map(async (i) => {
        const list = await get<{ dependsOnId: string }[]>(`/api/work-items/${i.id}/dependencies`)
        if (list.length) deps[i.id] = list.map(d => d.dependsOnId)
      }))
      data.value = {
        ready: true, loadedAt: Date.now(), projects, items, deps, runs, evidence, reviews,
        requirements: per.flatMap(x => x.requirements), gates: per.flatMap(x => x.gates),
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : '無法讀取資料'
    } finally {
      busy.value = false
    }
  }

  const byId = computed(() => ({
    items: new Map(data.value.items.map(i => [i.id, i])),
    requirements: new Map(data.value.requirements.map(r => [r.id, r])),
    projects: new Map(data.value.projects.map(p => [p.id, p])),
  }))

  function traceOf(item: WorkItem): Trace {
    return buildTrace(item, { requirements: byId.value.requirements, runs: data.value.runs, evidence: data.value.evidence, reviews: data.value.reviews, gates: data.value.gates })
  }

  /** Items that wait on this one (reverse dependencies). */
  function dependentsOf(id: string): WorkItem[] {
    return data.value.items.filter(i => (data.value.deps[i.id] ?? []).includes(id))
  }
  function dependenciesOf(id: string): WorkItem[] {
    return (data.value.deps[id] ?? []).map(d => byId.value.items.get(d)).filter((x): x is WorkItem => !!x)
  }

  /** The moves the API will accept from a status, with the reason a move is not available. */
  function allowedMoves(item: WorkItem): { to: WorkStatus, ok: boolean, why: string }[] {
    const unfinished = dependenciesOf(item.id).filter(d => d.status !== 'Done')
    const order: WorkStatus[] = ['Backlog', 'Ready', 'InProgress', 'InReview', 'Done']
    const targets: WorkStatus[] = item.status === 'Blocked' ? ['Ready', 'InProgress'] : [...order.filter(s => s !== item.status), 'Blocked']
    return targets.map((to) => {
      if (to === 'Done' && unfinished.length) return { to, ok: false, why: `要先完成 ${unfinished.map(d => d.code).join('、')}` }
      if (to === 'Blocked') return { to, ok: true, why: '需要填寫原因' }
      return { to, ok: true, why: '' }
    })
  }

  async function setStatus(item: WorkItem, status: WorkStatus, reason?: string) {
    busy.value = true
    try {
      await $fetch(`/api/work-items/${item.id}/status`, { method: 'PATCH', credentials: 'include', body: { status, reason } })
      toast.value = `${item.code} 已改為${status}`
      await load(true)
    } catch (e: unknown) {
      const message = (e as { data?: { detail?: string, errors?: Record<string, string[]> } })?.data
      toast.value = message?.detail ?? Object.values(message?.errors ?? {}).flat()[0] ?? '無法變更狀態'
    } finally {
      busy.value = false
    }
  }

  async function decide(gate: HumanGate, approved: boolean, note: string) {
    busy.value = true
    try {
      await $fetch(`/api/gates/${gate.id}/decision`, { method: 'POST', credentials: 'include', body: { approved, note } })
      toast.value = approved ? '已核准（只記錄在 Draupnir，未同步到 Supervisor）' : '已駁回（只記錄在 Draupnir，未同步到 Supervisor）'
      await load(true)
    } catch (e: unknown) {
      toast.value = (e as { data?: { detail?: string } })?.data?.detail ?? '無法送出決定'
    } finally {
      busy.value = false
    }
  }

    /** What happened, newest first, built from agent runs (the only timestamped records) and decided gates. */
  const events = computed(() => {
    const out: { id: string, at: string, tone: Tone, verb: string, agent: string, runStatus: string, itemId: string | null, projectId: string | null, title: string, taskId: string }[] = []
    const verbs: Record<string, string> = {
      DONE: '完成了', IMPLEMENTING: '開始實作', REVIEWING: '送去審查', VERIFYING: '開始驗證', REWORKING: '正在修正', CHANGES_REQUESTED: '被要求修改',
      WAITING_HUMAN: '在等你決定', AGENT_LOST: '連線中斷於', READY: '排進待派工',
    }
    for (const r of data.value.runs) {
      const at = r.finishedAt ?? r.startedAt
      if (!at) continue
      const item = r.workItemId ? byId.value.items.get(r.workItemId) : null
      out.push({ id: r.id, at, tone: runTone(r.status), verb: verbs[r.status] ?? r.status, agent: r.agent, runStatus: r.status, itemId: item?.id ?? null, projectId: r.projectId, title: item?.title ?? r.externalTaskId, taskId: r.externalTaskId })
    }
    return out.sort((a, b) => b.at.localeCompare(a.at))
  })

  /** Current activity per agent: the newest run that is still moving. */
  const agents = computed(() => {
    const names = [...new Set(data.value.runs.map(r => r.agent))]
    return names.map((name) => {
      const mine = data.value.runs.filter(r => r.agent === name)
      const live = mine.filter(r => runTone(r.status) === 'moving' || r.status === 'AGENT_LOST' || r.status === 'WAITING_HUMAN')
      const latest = [...mine].sort((a, b) => (b.finishedAt ?? b.startedAt ?? '').localeCompare(a.finishedAt ?? a.startedAt ?? ''))
      return { name, model: mine.find(r => r.model)?.model ?? '', runs: latest, live, done: mine.filter(r => r.status === 'DONE').length }
    }).sort((a, b) => b.live.length - a.live.length || b.runs.length - a.runs.length)
  })

  const pendingGates = computed(() => data.value.gates.filter(g => g.status === 'Pending'))
  /** What needs the owner right now, most urgent first. */
  const attention = computed(() => {
    const out: { kind: 'gate' | 'blocked' | 'fault', id: string, title: string, projectId: string, itemId?: string, why: string }[] = []
    for (const g of pendingGates.value) out.push({ kind: 'gate', id: g.id, title: g.title, projectId: g.projectId, itemId: g.workItemId ?? undefined, why: '等你決定' })
    for (const i of data.value.items.filter(x => x.status === 'Blocked')) out.push({ kind: 'blocked', id: i.id, title: i.title, projectId: i.projectId, itemId: i.id, why: i.blockedReason ?? '被擋住' })
    for (const r of data.value.runs.filter(x => ['AGENT_LOST', 'CHANGES_REQUESTED', 'BLOCKED_CONFIG'].includes(x.status))) {
      out.push({ kind: 'fault', id: r.id, title: `${r.externalTaskId}：${r.agent}`, projectId: r.projectId ?? '', itemId: r.workItemId ?? undefined, why: r.status === 'AGENT_LOST' ? 'Agent 連線中斷' : '要求修改' })
    }
    return out
  })

  return { data, error, busy, toast, load, byId, traceOf, dependentsOf, dependenciesOf, allowedMoves, setStatus, decide, pendingGates, attention, events, agents }
}
