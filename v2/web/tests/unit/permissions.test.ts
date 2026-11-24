import { describe, expect, it } from 'vitest'
import { canApproveGates, canCreateProjects, canManageUsers, isAdmin } from '~/utils/permissions'
import type { User } from '~/types/api'

function user(role: 'Admin' | 'Member'): User {
  return { id: '1', email: 'u@example.test', displayName: 'User', role, isActive: true }
}

describe('multi-user UI guards', () => {
  it('only Admin can manage users, create projects and approve gates', () => {
    expect(isAdmin(user('Admin'))).toBe(true)
    expect(canManageUsers(user('Admin'))).toBe(true)
    expect(canCreateProjects(user('Admin'))).toBe(true)
    expect(canApproveGates(user('Admin'))).toBe(true)

    expect(isAdmin(user('Member'))).toBe(false)
    expect(canManageUsers(user('Member'))).toBe(false)
    expect(canCreateProjects(user('Member'))).toBe(false)
    expect(canApproveGates(user('Member'))).toBe(false)
    expect(canApproveGates(null)).toBe(false)
  })
})
