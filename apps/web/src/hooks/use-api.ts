'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  videoApi,
  searchApi,
  authApi,
  auditApi,
  ApiClientError,
} from '@/lib/api-client';
import { useToast } from './use-toast';

// Query keys for cache management
export const queryKeys = {
  videos: {
    all: ['videos'] as const,
    lists: () => [...queryKeys.videos.all, 'list'] as const,
    list: (page: number, pageSize: number) => [...queryKeys.videos.lists(), { page, pageSize }] as const,
    details: () => [...queryKeys.videos.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.videos.details(), id] as const,
    pending: (page: number, pageSize: number) => [...queryKeys.videos.all, 'pending', { page, pageSize }] as const,
    streaming: (id: string) => [...queryKeys.videos.all, 'streaming', id] as const,
    processing: (id: string) => [...queryKeys.videos.all, 'processing', id] as const,
  },
  search: {
    all: ['search'] as const,
    results: (query: string, page: number, pageSize: number) => [...queryKeys.search.all, { query, page, pageSize }] as const,
    suggestions: (query: string) => [...queryKeys.search.all, 'suggestions', query] as const,
    facets: () => [...queryKeys.search.all, 'facets'] as const,
  },
  auth: {
    user: ['auth', 'user'] as const,
    check: ['auth', 'check'] as const,
    devUsers: ['auth', 'dev-users'] as const,
  },
  audit: {
    all: ['audit'] as const,
    list: (page: number, pageSize: number, action?: string, targetType?: string) =>
      [...queryKeys.audit.all, 'list', { page, pageSize, action, targetType }] as const,
    stats: (days: number) => [...queryKeys.audit.all, 'stats', days] as const,
  },
};

// Video hooks
export function useVideos(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: queryKeys.videos.list(page, pageSize),
    queryFn: () => videoApi.list(page, pageSize),
    staleTime: 30000, // 30 seconds
  });
}

export function useVideo(id: string) {
  return useQuery({
    queryKey: queryKeys.videos.detail(id),
    queryFn: () => videoApi.get(id),
    enabled: !!id,
    staleTime: 60000, // 1 minute
    refetchInterval: (query) => {
      const data = query.state.data;
      return data && (data.status === 'Published' || data.status === 'Ready') ? false : 4000;
    },
    retry: (failureCount, error) => {
      // Don't retry on 404s
      if (error instanceof ApiClientError && error.isNotFound) {
        return false;
      }
      return failureCount < 3;
    },
  });
}

export function usePendingVideos(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: queryKeys.videos.pending(page, pageSize),
    queryFn: () => videoApi.getPendingReview(page, pageSize),
    staleTime: 10000, // 10 seconds - pending items change frequently
  });
}

export function useStreamingInfo(id: string) {
  return useQuery({
    queryKey: queryKeys.videos.streaming(id),
    queryFn: () => videoApi.streamingInfo(id),
    enabled: !!id,
    staleTime: 5000,
    refetchInterval: (query) => {
      const data = query.state.data;
      return data && data.masterPlaylistUrl ? false : 4000;
    },
  });
}

export function useProcessingStatus(id: string) {
  return useQuery({
    queryKey: queryKeys.videos.processing(id),
    queryFn: () => videoApi.processingStatus(id),
    enabled: !!id,
    refetchInterval: 4000,
    staleTime: 0,
  });
}

// Video Highlights hooks
export function useVideoHighlights(id: string, language?: string) {
  return useQuery({
    queryKey: [...queryKeys.videos.detail(id), 'highlights', language ?? 'en'] as const,
    queryFn: () => videoApi.getHighlights(id, language),
    enabled: !!id,
    staleTime: 60000, // 1 minute
  });
}

export function useSupportedLanguages() {
  return useQuery({
    queryKey: ['supported-languages'] as const,
    queryFn: () => videoApi.getSupportedLanguages(),
    staleTime: Infinity, // Languages don't change
  });
}

export function useTranslateHighlights() {
  const queryClient = useQueryClient();
  const { success, apiError } = useToast();

  return useMutation({
    mutationFn: ({ id, targetLanguage }: { id: string; targetLanguage: string }) =>
      videoApi.translateHighlights(id, targetLanguage),
    onSuccess: (data) => {
      success({
        title: 'Translation Complete',
        description: `Highlights translated to ${data.targetLanguageName}`,
      });
      // Invalidate highlights cache to show updated translations
      queryClient.invalidateQueries({ queryKey: queryKeys.videos.details() });
    },
    onError: (error) => {
      apiError(error, 'Failed to translate highlights');
    },
  });
}

export function useApproveVideo() {
  const queryClient = useQueryClient();
  const { success, apiError } = useToast();

  return useMutation({
    mutationFn: ({ id, notes }: { id: string; notes?: string }) =>
      videoApi.approve(id, notes),
    onSuccess: () => {
      success({
        title: 'Video Approved',
        description: 'The video has been approved and published.',
      });
      // Invalidate related queries
      queryClient.invalidateQueries({ queryKey: queryKeys.videos.all });
    },
    onError: (error) => {
      apiError(error, 'Failed to approve video');
    },
  });
}

export function useRejectVideo() {
  const queryClient = useQueryClient();
  const { success, apiError } = useToast();

  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      videoApi.reject(id, reason),
    onSuccess: () => {
      success({
        title: 'Video Rejected',
        description: 'The video has been rejected.',
      });
      queryClient.invalidateQueries({ queryKey: queryKeys.videos.all });
    },
    onError: (error) => {
      apiError(error, 'Failed to reject video');
    },
  });
}

export function useDeleteVideo() {
  const queryClient = useQueryClient();
  const { success, apiError } = useToast();

  return useMutation({
    mutationFn: (id: string) => videoApi.delete(id),
    onSuccess: () => {
      success({
        title: 'Video Deleted',
        description: 'The video has been permanently deleted.',
      });
      queryClient.invalidateQueries({ queryKey: queryKeys.videos.all });
    },
    onError: (error) => {
      apiError(error, 'Failed to delete video');
    },
  });
}

// Search hooks
export function useSearch(query: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: queryKeys.search.results(query, page, pageSize),
    queryFn: () => searchApi.search(query, page, pageSize),
    enabled: query.length > 0,
    staleTime: 60000, // 1 minute
    placeholderData: (previousData) => previousData, // Keep previous data while loading
  });
}

export function useSearchSuggestions(query: string) {
  return useQuery({
    queryKey: queryKeys.search.suggestions(query),
    queryFn: () => searchApi.suggest(query),
    enabled: query.length >= 2,
    staleTime: 120000, // 2 minutes
  });
}

export function useSearchFacets() {
  return useQuery({
    queryKey: queryKeys.search.facets(),
    queryFn: () => searchApi.facets(),
    staleTime: 300000, // 5 minutes
  });
}

// Auth hooks
export function useCurrentUser() {
  return useQuery({
    queryKey: queryKeys.auth.user,
    queryFn: () => authApi.me(),
    staleTime: 300000, // 5 minutes
    retry: false, // Don't retry auth failures
  });
}

export function useAuthCheck() {
  return useQuery({
    queryKey: queryKeys.auth.check,
    queryFn: () => authApi.check(),
    staleTime: 60000, // 1 minute
    retry: false,
  });
}

export function useDevUsers() {
  return useQuery({
    queryKey: queryKeys.auth.devUsers,
    queryFn: () => authApi.devUsers(),
    staleTime: Infinity, // Dev users don't change
  });
}

// Audit hooks
export function useAuditLogs(page = 1, pageSize = 50, action?: string, targetType?: string) {
  return useQuery({
    queryKey: queryKeys.audit.list(page, pageSize, action, targetType),
    queryFn: () => auditApi.list(page, pageSize, action, targetType),
    staleTime: 30000, // 30 seconds
  });
}

export function useAuditStats(days = 7) {
  return useQuery({
    queryKey: queryKeys.audit.stats(days),
    queryFn: () => auditApi.stats(days),
    staleTime: 60000, // 1 minute
  });
}

// Utility hook for handling query errors in components
export function useQueryErrorHandler() {
  const { apiError } = useToast();

  return (error: unknown) => {
    apiError(error);
  };
}
