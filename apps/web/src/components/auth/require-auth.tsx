'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Loader2, ShieldAlert } from 'lucide-react';
import { useAuth } from '@/lib/auth';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import Link from 'next/link';

export type Role = 'Admin' | 'Reviewer' | 'Uploader' | 'Viewer' | 'admin' | 'reviewer' | 'uploader' | 'viewer';

interface RequireAuthProps {
  children: React.ReactNode;
  /**
   * If specified, user must have at least one of these roles
   */
  roles?: Role[];
  /**
   * Redirect URL if unauthorized (default: '/')
   */
  redirectTo?: string;
  /**
   * Show access denied message instead of redirecting
   */
  showAccessDenied?: boolean;
}

/**
 * Protect routes with authentication and role-based access control
 */
export function RequireAuth({
  children,
  roles,
  redirectTo = '/',
  showAccessDenied = false,
}: RequireAuthProps) {
  const router = useRouter();
  const { user, isAuthenticated, isLoading } = useAuth();

  // Normalize roles for case-insensitive comparison
  const normalizeRole = (role: string) => role.toLowerCase();

  const userRoles = user?.roles?.map(normalizeRole) || [];
  const requiredRoles = roles?.map(normalizeRole) || [];

  const hasRequiredRole = requiredRoles.length === 0 ||
    requiredRoles.some(role => userRoles.includes(role));

  const isAuthorized = isAuthenticated && hasRequiredRole;

  useEffect(() => {
    if (!isLoading && !isAuthorized && !showAccessDenied) {
      router.push(redirectTo);
    }
  }, [isLoading, isAuthorized, showAccessDenied, router, redirectTo]);

  // Loading state
  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <Loader2 className="h-8 w-8 animate-spin text-primary mx-auto mb-4" />
          <p className="text-gray-500">Loading...</p>
        </div>
      </div>
    );
  }

  // Not authenticated
  if (!isAuthenticated) {
    if (showAccessDenied) {
      return (
        <div className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
          <Card className="max-w-md w-full">
            <CardContent className="py-12 text-center">
              <ShieldAlert className="h-16 w-16 text-yellow-500 mx-auto mb-4" />
              <h2 className="text-2xl font-bold text-gray-900 mb-2">
                Sign In Required
              </h2>
              <p className="text-gray-500 mb-6">
                You need to sign in to access this page.
              </p>
              <div className="flex justify-center gap-3">
                <Button variant="outline" asChild>
                  <Link href="/">Go Home</Link>
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      );
    }
    return null;
  }

  // Authenticated but missing required role
  if (!hasRequiredRole) {
    if (showAccessDenied) {
      return (
        <div className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
          <Card className="max-w-md w-full border-red-200">
            <CardContent className="py-12 text-center">
              <ShieldAlert className="h-16 w-16 text-red-500 mx-auto mb-4" />
              <h2 className="text-2xl font-bold text-gray-900 mb-2">
                Access Denied
              </h2>
              <p className="text-gray-500 mb-4">
                You don&apos;t have permission to access this page.
              </p>
              {roles && roles.length > 0 && (
                <p className="text-sm text-gray-400 mb-6">
                  Required role: {roles.join(' or ')}
                </p>
              )}
              <div className="flex justify-center gap-3">
                <Button variant="outline" asChild>
                  <Link href="/">Go Home</Link>
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      );
    }
    return null;
  }

  // Authorized
  return <>{children}</>;
}

/**
 * Hook to check if user has specific roles
 */
export function useHasRole(...roles: Role[]): boolean {
  const { user } = useAuth();

  if (!user?.roles) return false;

  const normalizeRole = (role: string) => role.toLowerCase();
  const userRoles = user.roles.map(normalizeRole);
  const requiredRoles = roles.map(normalizeRole);

  return requiredRoles.some(role => userRoles.includes(role));
}

/**
 * Hook to check if user is admin
 */
export function useIsAdmin(): boolean {
  return useHasRole('Admin', 'admin');
}

/**
 * Hook to check if user can review content
 */
export function useCanReview(): boolean {
  return useHasRole('Admin', 'admin', 'Reviewer', 'reviewer');
}

/**
 * Hook to check if user can upload content
 */
export function useCanUpload(): boolean {
  return useHasRole('Admin', 'admin', 'Uploader', 'uploader');
}
