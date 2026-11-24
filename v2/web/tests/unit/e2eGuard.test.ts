import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { afterEach, describe, expect, it } from 'vitest'
import { assertSafeE2e } from '../e2e/guard'

const dirs: string[] = []
function env(overrides: Record<string, string | undefined> = {}) {
  const state = fs.mkdtempSync(path.join(os.tmpdir(), 'draupnir-e2e-guard-'))
  dirs.push(state)
  return { E2E_ALLOW_WRITE: '1', NUXT_API_BASE: 'http://127.0.0.1:5088', E2E_STATE_DIR: state, ...overrides }
}
const health = { status: 'ok', database: 'draupnir_v21_fix2', disposable: true }

afterEach(() => {
  for (const dir of dirs.splice(0)) fs.rmSync(dir, { recursive: true, force: true })
})

describe('E2E write guard', () => {
  it('accepts a local disposable API', () => {
    expect(() => assertSafeE2e(env(), health)).not.toThrow()
  })

  it('refuses without the explicit write opt-in', () => {
    expect(() => assertSafeE2e(env({ E2E_ALLOW_WRITE: undefined }), health)).toThrow(/E2E_ALLOW_WRITE/)
  })

  it('refuses a non-loopback API', () => {
    expect(() => assertSafeE2e(env({ NUXT_API_BASE: 'http://192.0.2.10:5088' }), health)).toThrow(/loopback/)
  })

  it('refuses the shared preview database', () => {
    expect(() => assertSafeE2e(env(), { ...health, database: 'draupnir_v21', disposable: false }))
      .toThrow(/draupnir_v21/)
  })

  it('refuses when the API cannot report a disposable database', () => {
    expect(() => assertSafeE2e(env(), undefined)).toThrow(/health/)
  })
})
