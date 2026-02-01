'use client';

import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import type { User, AuthState, DevUser } from './types';
import { getApiBaseUrl } from '../api-client';

interface AuthContextType extends AuthState {
  login: (userId?: string, role?: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
  tryDevMode: () => Promise<boolean>;
  devUsers: DevUser[];
  isDevMode: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

// Store dev auth headers in localStorage
const DEV_USER_KEY = 'dev-user-id';
const DEV_ROLE_KEY = 'dev-user-role';

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    user: null,
    isAuthenticated: false,
    isLoading: true,
    error: null,
  });
  const [devUsers, setDevUsers] = useState<DevUser[]>([]);
  const [isDevMode, setIsDevMode] = useState(false);

  // Get stored dev credentials
  const getDevHeaders = useCallback((): Record<string, string> => {
    if (typeof window === 'undefined') return {};

    const userId = localStorage.getItem(DEV_USER_KEY);
    const role = localStorage.getItem(DEV_ROLE_KEY);

    const headers: Record<string, string> = {};
    if (userId) headers['X-Dev-User'] = userId;
    if (role) headers['X-Dev-Role'] = role;

    return headers;
  }, []);

  // Fetch current user from API
  const fetchCurrentUser = useCallback(async (): Promise<User | null> => {
    try {
      const response = await fetch(`${getApiBaseUrl()}/api/auth/me`, {
        headers: {
          'Content-Type': 'application/json',
          ...getDevHeaders(),
        },
        credentials: 'include',
      });

      if (!response.ok) {
        if (response.status === 401) {
          return null;
        }
        throw new Error(`Failed to fetch user: ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      console.error('Error fetching current user:', error);
      return null;
    }
  }, [getDevHeaders]);

  // Fetch available dev users
  const fetchDevUsers = useCallback(async (): Promise<DevUser[]> => {
    try {
      const response = await fetch(`${getApiBaseUrl()}/api/auth/dev-users`);
      if (response.ok) {
        const users = await response.json();
        setDevUsers(users);
        setIsDevMode(true);
        return users;
      } else {
        setIsDevMode(false);
        return [];
      }
    } catch {
      setIsDevMode(false);
      return [];
    }
  }, []);

  // Initialize auth state - runs once on mount
  useEffect(() => {
    let mounted = true;

    const init = async () => {
      // Fetch dev users and get the result directly (don't rely on state)
      const users = await fetchDevUsers();

      if (!mounted) return;

      // If dev mode is available and no stored dev user, default to admin for demo convenience
      if (users.length > 0 && typeof window !== 'undefined') {
        const storedUser = localStorage.getItem(DEV_USER_KEY);
        if (!storedUser) {
          const adminUser = users.find((u) => u.role?.toLowerCase() === 'admin') ?? users[0];
          localStorage.setItem(DEV_USER_KEY, adminUser.id);
          localStorage.setItem(DEV_ROLE_KEY, adminUser.role);
        }
      }

      const user = await fetchCurrentUser();

      if (!mounted) return;

      setState({
        user,
        isAuthenticated: !!user,
        isLoading: false,
        error: null,
      });
    };

    init();

    return () => {
      mounted = false;
    };
  }, [fetchCurrentUser, fetchDevUsers]);

  // Login (for dev mode)
  const login = useCallback(async (userId?: string, role?: string) => {
    setState((prev) => ({ ...prev, isLoading: true, error: null }));

    try {
      if (isDevMode) {
        // Dev mode: store credentials and fetch user
        if (userId) localStorage.setItem(DEV_USER_KEY, userId);
        if (role) localStorage.setItem(DEV_ROLE_KEY, role);
      }

      const user = await fetchCurrentUser();
      setState({
        user,
        isAuthenticated: !!user,
        isLoading: false,
        error: user ? null : 'Failed to authenticate',
      });
    } catch (error) {
      setState({
        user: null,
        isAuthenticated: false,
        isLoading: false,
        error: error instanceof Error ? error.message : 'Authentication failed',
      });
    }
  }, [isDevMode, fetchCurrentUser]);

  // Logout
  const logout = useCallback(async () => {
    if (isDevMode) {
      localStorage.removeItem(DEV_USER_KEY);
      localStorage.removeItem(DEV_ROLE_KEY);
    }

    setState({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      error: null,
    });
  }, [isDevMode]);

  // Refresh user data
  const refreshUser = useCallback(async () => {
    const user = await fetchCurrentUser();
    setState((prev) => ({
      ...prev,
      user,
      isAuthenticated: !!user,
    }));
  }, [fetchCurrentUser]);

  // Try to enable dev mode (e.g. when API supports dev-users). Returns true if dev users were loaded.
  const tryDevMode = useCallback(async (): Promise<boolean> => {
    try {
      const response = await fetch(`${getApiBaseUrl()}/api/auth/dev-users`);
      if (response.ok) {
        const users = await response.json();
        if (Array.isArray(users) && users.length > 0) {
          setDevUsers(users);
          setIsDevMode(true);
          return true;
        }
      }
    } catch {
      // ignore
    }
    return false;
  }, []);

  return (
    <AuthContext.Provider
      value={{
        ...state,
        login,
        logout,
        refreshUser,
        tryDevMode,
        devUsers,
        isDevMode,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

// Hook to get fetch options with auth headers
export function useAuthFetch() {
  const getDevHeaders = (): Record<string, string> => {
    if (typeof window === 'undefined') return {};

    const userId = localStorage.getItem(DEV_USER_KEY);
    const role = localStorage.getItem(DEV_ROLE_KEY);

    const headers: Record<string, string> = {};
    if (userId) headers['X-Dev-User'] = userId;
    if (role) headers['X-Dev-Role'] = role;

    return headers;
  };

  const authFetch = async (url: string, options: RequestInit = {}): Promise<Response> => {
    const headers = {
      'Content-Type': 'application/json',
      ...getDevHeaders(),
      ...(options.headers || {}),
    };

    return fetch(url, {
      ...options,
      headers,
      credentials: 'include',
    });
  };

  return { authFetch, getDevHeaders };
}
