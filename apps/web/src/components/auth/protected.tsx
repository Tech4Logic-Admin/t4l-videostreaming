'use client';

import { useAuth } from '@/lib/auth';
import { Roles, hasRole, hasAnyRole } from '@/lib/auth/types';
import type { Role } from '@/lib/auth/types';

interface ProtectedProps {
  children: React.ReactNode;
  /** Required role(s) - user must have at least one */
  roles?: Role[];
  /** Fallback component when not authorized */
  fallback?: React.ReactNode;
  /** Whether to show loading state */
  showLoading?: boolean;
}

/**
 * Protect content based on authentication and roles
 */
export function Protected({
  children,
  roles,
  fallback = null,
  showLoading = true,
}: ProtectedProps) {
  const { user, isAuthenticated, isLoading } = useAuth();

  if (isLoading && showLoading) {
    return (
      <div className="flex items-center justify-center p-4">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <>{fallback}</>;
  }

  if (roles && roles.length > 0 && !hasAnyRole(user, roles)) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}

/**
 * Show content only for admins
 */
export function AdminOnly({
  children,
  fallback = null,
}: {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}) {
  return (
    <Protected roles={[Roles.Admin]} fallback={fallback}>
      {children}
    </Protected>
  );
}

/**
 * Show content only for users who can upload
 */
export function UploaderOnly({
  children,
  fallback = null,
}: {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}) {
  return (
    <Protected roles={[Roles.Admin, Roles.Uploader]} fallback={fallback}>
      {children}
    </Protected>
  );
}

/**
 * Show content only for reviewers
 */
export function ReviewerOnly({
  children,
  fallback = null,
}: {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}) {
  return (
    <Protected roles={[Roles.Admin, Roles.Reviewer]} fallback={fallback}>
      {children}
    </Protected>
  );
}

/**
 * Hook to check permissions
 */
export function usePermissions() {
  const { user, isAuthenticated } = useAuth();

  return {
    isAuthenticated,
    isAdmin: hasRole(user, Roles.Admin),
    canUpload: hasAnyRole(user, [Roles.Admin, Roles.Uploader]),
    canReview: hasAnyRole(user, [Roles.Admin, Roles.Reviewer]),
    canViewVideos: hasAnyRole(user, [Roles.Admin, Roles.Uploader, Roles.Reviewer, Roles.Viewer]),
    canManageUsers: hasRole(user, Roles.Admin),
    canViewAuditLogs: hasRole(user, Roles.Admin),
    canViewReports: hasRole(user, Roles.Admin),
  };
}
