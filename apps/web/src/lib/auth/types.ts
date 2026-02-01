// User and Auth types for the frontend

export interface User {
  id: string;
  email: string;
  name: string;
  roles: string[];
  permissions: UserPermissions;
}

export interface UserPermissions {
  canUpload: boolean;
  canReview: boolean;
  canViewVideos: boolean;
  canManageUsers: boolean;
  canViewAuditLogs: boolean;
  canViewReports: boolean;
  canConfigureSystem: boolean;
}

export interface AuthState {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

export interface DevUser {
  id: string;
  email: string;
  name: string;
  role: string;
}

// Role constants (must match backend)
export const Roles = {
  Admin: 'Admin',
  Uploader: 'Uploader',
  Reviewer: 'Reviewer',
  Viewer: 'Viewer',
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

// Helper to check if user has a role
export function hasRole(user: User | null, role: Role): boolean {
  return user?.roles.includes(role) ?? false;
}

// Helper to check if user has any of the specified roles
export function hasAnyRole(user: User | null, roles: Role[]): boolean {
  return roles.some((role) => hasRole(user, role));
}
