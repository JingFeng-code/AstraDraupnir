import type { User } from '~/types/api'

export function isAdmin(user: User | null | undefined): boolean {
  return user?.role === 'Admin'
}

export function canManageUsers(user: User | null | undefined): boolean {
  return isAdmin(user)
}

export function canCreateProjects(user: User | null | undefined): boolean {
  return isAdmin(user)
}

export function canApproveGates(user: User | null | undefined): boolean {
  return isAdmin(user)
}

export function canImport(user: User | null | undefined): boolean {
  return isAdmin(user)
}
