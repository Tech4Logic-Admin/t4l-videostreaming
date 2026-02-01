/**
 * API Client for Tech4Logic Video Search API
 * Provides type-safe API calls with error handling and response normalization
 */

// ---------- Shared types ----------

export interface ApiError {
  status: number;
  title: string;
  detail: string;
  traceId?: string;
  timestamp?: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  traceId?: string;
  timestamp?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/**
 * API list endpoints return `videos`; the UI prefers `items`.
 * Keep both to support existing screens.
 */
export interface VideoListResponse extends PaginatedResponse<Video> {
  videos?: Video[];
}

export type VideoStatus =
  | 'Queued'
  | 'Processing'
  | 'Ready'
  | 'Published'
  | 'Failed'
  | 'PendingReview'
  | 'Rejected'
  | 'Moderating'
  | 'Quarantined';

export interface TranscriptSegment {
  id: string;
  startMs: number;
  endMs: number;
  text: string;
  detectedLanguage?: string;
  speaker?: string;
  confidence?: number;
}

export interface VideoHighlight {
  id: string;
  text: string;
  category: string; // 'key_point' | 'promise' | 'announcement' | 'statistic' | 'quote'
  importance?: number;
  timestampMs?: number;
  originalText?: string;
  sourceLanguage?: string;
  displayLanguage?: string;
}

export interface VideoSummaryInfo {
  summary?: string;
  tldr?: string;
  keywords?: string[];
  topics?: string[];
  sentiment?: string; // 'positive' | 'negative' | 'neutral' | 'mixed'
  originalSummary?: string;
  originalTlDr?: string;
  sourceLanguage?: string;
}

export interface LanguageOption {
  code: string;
  name: string;
}

export interface VideoHighlightsResponse {
  videoId: string;
  highlights: VideoHighlight[];
  availableLanguages: LanguageOption[];
  sourceLanguage?: string;
  currentLanguage: string;
  summary?: VideoSummaryInfo;
}

export interface TranslatedHighlight {
  id: string;
  originalText: string;
  translatedText: string;
  category: string;
  importance?: number;
  timestampMs?: number;
}

export interface TranslateHighlightsResponse {
  videoId: string;
  targetLanguage: string;
  targetLanguageName: string;
  highlights: TranslatedHighlight[];
}

export interface SupportedLanguagesResponse {
  languages: LanguageOption[];
}

export interface Video {
  id: string;
  title: string;
  description?: string;
  status: VideoStatus;
  createdAt: string;
  createdByOid: string;
  durationMs?: number;
  thumbnailUrl?: string;
  streamUrl?: string;
  tags?: string[];
  languageHint?: string;
  transcriptSegments?: TranscriptSegment[];
  highlights?: VideoHighlight[];
  summaryInfo?: VideoSummaryInfo;
}

export interface VideoDetail extends Video {
  blobPath: string;
  transcriptSegments: TranscriptSegment[];
  highlights?: VideoHighlight[];
  summaryInfo?: VideoSummaryInfo;
}

export interface StreamingVariant {
  quality: string;
  width: number;
  height: number;
  videoBitrateKbps: number;
  audioBitrateKbps: number;
  playlistUrl: string;
}

export interface StreamingInfo {
  videoId: string;
  title: string;
  durationMs?: number;
  thumbnailUrl?: string | null;
  masterPlaylistUrl?: string | null;
  sourceUrl?: string | null;
  isEncodingComplete: boolean;
  variants: StreamingVariant[];
}

export interface ProcessingJob {
  stage: string;
  status: string;
  progress: number;
  progressMessage?: string;
  attempts: number;
  lastError?: string;
  startedAt?: string;
  completedAt?: string;
}

export interface VariantProgress {
  quality: string;
  status: string;
  progress: number;
  progressMessage?: string;
}

export interface ProcessingStatus {
  videoId: string;
  videoStatus: string;
  overallProcessingStatus: string;
  progressPercentage: number;
  jobs: ProcessingJob[];
  variants?: VariantProgress[];
}

export interface TimelineMatch {
  segmentId: string;
  startMs: number;
  endMs: number;
  text: string;
  highlightedText?: string;
  score: number;
}

export interface VideoSearchItem {
  id: string;
  title: string;
  description?: string;
  thumbnailUrl?: string;
  duration?: number;
  highlights?: string[];
  matches?: TimelineMatch[];
  score: number;
}

export interface SearchResult {
  items: VideoSearchItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  facets?: SearchFacets;
}

export interface SearchFacets {
  languages: FacetValue[];
  tags: FacetValue[];
  durations: FacetValue[];
  videos?: FacetValue[];
}

export interface FacetValue {
  value: string;
  count: number;
}

export interface User {
  id: string;
  name: string;
  email: string;
  roles: string[];
  tenantId?: string;
}

export interface DevUser {
  id: string;
  name: string;
  email: string;
  role: string;
}

export interface AuditLog {
  id: string;
  action: string;
  targetType: string;
  targetId?: string;
  actorId: string;
  actorName?: string;
  timestamp: string;
  ipAddress?: string;
  metadata?: Record<string, unknown>;
}

export interface AuditStats {
  periodDays: number;
  totalActions: number;
  uniqueActors: number;
  securityEvents: number;
  actionBreakdown: { action: string; count: number }[];
}

// ---------- Error handling ----------

export class ApiClientError extends Error {
  public status: number;
  public traceId?: string;
  public detail: string;

  constructor(status: number, message: string, detail: string, traceId?: string) {
    super(message);
    this.name = 'ApiClientError';
    this.status = status;
    this.detail = detail;
    this.traceId = traceId;
  }

  get isNotFound(): boolean {
    return this.status === 404;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isForbidden(): boolean {
    return this.status === 403;
  }

  get isValidationError(): boolean {
    return this.status === 400 || this.status === 422;
  }

  get isServerError(): boolean {
    return this.status >= 500;
  }
}

const ENV_API_URL = process.env.NEXT_PUBLIC_API_URL || '';

/**
 * Returns an absolute base URL for API requests. Paths are always /api/... so base must be
 * the origin (e.g. http://13.68.145.89) when NEXT_PUBLIC_API_URL is /api, to avoid double /api/api.
 * Also ensures new URL(path, base) never receives a relative base (which throws).
 */
export function getApiBaseUrl(): string {
  const raw = ENV_API_URL || (typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000');
  if (typeof raw !== 'string' || !raw.trim()) {
    return typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000';
  }
  const trimmed = raw.trim();
  if (trimmed.startsWith('/')) {
    return typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000';
  }
  try {
    new URL(trimmed);
    return trimmed;
  } catch {
    return typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000';
  }
}

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: unknown;
  params?: Record<string, string | number | boolean | undefined>;
};

async function handleResponse<T>(response: Response): Promise<T> {
  const contentType = response.headers.get('content-type');

  if (!response.ok) {
    let errorData: ApiError;

    if (contentType?.includes('application/json') || contentType?.includes('application/problem+json')) {
      errorData = await response.json();
    } else {
      errorData = {
        status: response.status,
        title: response.statusText,
        detail: 'An unexpected error occurred',
      };
    }

    throw new ApiClientError(
      errorData.status || response.status,
      errorData.title || 'Error',
      errorData.detail || 'An unexpected error occurred',
      errorData.traceId
    );
  }

  // Handle empty responses (204 No Content)
  if (response.status === 204 || !contentType) {
    return undefined as T;
  }

  if (contentType?.includes('application/json')) {
    return response.json();
  }

  return response.text() as unknown as T;
}

function buildUrl(path: string, params?: Record<string, string | number | boolean | undefined>): string {
  const base = getApiBaseUrl();
  const url = new URL(path, base);

  if (params) {
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        url.searchParams.append(key, String(value));
      }
    });
  }

  return url.toString();
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { body, params, headers: customHeaders, ...rest } = options;

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...customHeaders,
  };

  // Add dev auth headers if in dev mode
  if (typeof window !== 'undefined') {
    const devUserId = localStorage.getItem('dev-user-id');
    const devUserRole = localStorage.getItem('dev-user-role');

    if (devUserId) {
      (headers as Record<string, string>)['X-Dev-User'] = devUserId;
    }
    if (devUserRole) {
      (headers as Record<string, string>)['X-Dev-Role'] = devUserRole;
    }
  }

  const url = buildUrl(path, params);

  const response = await fetch(url, {
    ...rest,
    headers,
    body: body ? JSON.stringify(body) : undefined,
    credentials: 'include',
  });

  return handleResponse<T>(response);
}

// Convenience methods
export const api = {
  get: <T>(path: string, params?: Record<string, string | number | boolean | undefined>) =>
    apiRequest<T>(path, { method: 'GET', params }),

  post: <T>(path: string, body?: unknown, params?: Record<string, string | number | boolean | undefined>) =>
    apiRequest<T>(path, { method: 'POST', body, params }),

  put: <T>(path: string, body?: unknown) =>
    apiRequest<T>(path, { method: 'PUT', body }),

  patch: <T>(path: string, body?: unknown) =>
    apiRequest<T>(path, { method: 'PATCH', body }),

  delete: <T>(path: string) =>
    apiRequest<T>(path, { method: 'DELETE' }),
};

// ---------- Helpers ----------

type ApiVideoList = {
  videos?: Video[];
  items?: Video[];
  totalCount?: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
};

function normalizeVideoListResponse(response: ApiVideoList): VideoListResponse {
  const items = response.items ?? response.videos ?? [];
  const page = response.page ?? 1;
  const pageSize = response.pageSize ?? (items.length > 0 ? items.length : 20);
  const totalCount = response.totalCount ?? items.length;
  const totalPages = response.totalPages ?? Math.max(1, Math.ceil(totalCount / Math.max(1, pageSize)));

  return {
    items,
    videos: response.videos ?? items,
    totalCount,
    page,
    pageSize,
    totalPages,
  };
}

// ---------- Typed API endpoints ----------

export const videoApi = {
  list: async (page = 1, pageSize = 20): Promise<VideoListResponse> => {
    const response = await api.get<ApiVideoList>('/api/videos', { page, pageSize });
    return normalizeVideoListResponse(response);
  },

  my: async (page = 1, pageSize = 20): Promise<VideoListResponse> => {
    const response = await api.get<ApiVideoList>('/api/videos/my', { page, pageSize });
    return normalizeVideoListResponse(response);
  },

  get: (id: string) =>
    api.get<VideoDetail>(`/api/videos/${id}`),

  getPendingReview: async (page = 1, pageSize = 20): Promise<VideoListResponse> => {
    const response = await api.get<ApiVideoList>('/api/moderation/queue', { page, pageSize });
    return normalizeVideoListResponse(response);
  },

  approve: (id: string, notes?: string) =>
    api.post(`/api/videos/${id}/approve`, { notes }),

  reject: (id: string, reason: string) =>
    api.post(`/api/videos/${id}/reject`, { reason }),

  delete: (id: string) =>
    api.delete<void>(`/api/videos/${id}`),

  streamingInfo: (id: string) =>
    api.get<StreamingInfo>(`/api/stream/${id}`),

  processingStatus: (id: string) =>
    api.get<ProcessingStatus>(`/api/videos/${id}/processing`),

  // Key Highlights with translation support
  getHighlights: (id: string, language?: string) =>
    api.get<VideoHighlightsResponse>(`/api/videos/${id}/highlights`, language ? { language } : {}),

  translateHighlights: (id: string, targetLanguage: string) =>
    api.post<TranslateHighlightsResponse>(`/api/videos/${id}/highlights/translate`, { targetLanguage }),

  getSupportedLanguages: () =>
    api.get<SupportedLanguagesResponse>('/api/videos/supported-languages'),
};

export const searchApi = {
  search: async (query: string, page = 1, pageSize = 20): Promise<SearchResult> => {
    const response = await api.get<any>('/api/search', { q: query, page, pageSize });

    const items: VideoSearchItem[] = (response.results ?? []).map((result: any) => {
      const matches: TimelineMatch[] = (result.matches ?? []).map((m: any) => ({
        segmentId: m.segmentId,
        startMs: m.startMs,
        endMs: m.endMs,
        text: m.text,
        highlightedText: m.highlightedText,
        score: m.score,
      }));

      const firstMatch = matches[0];
      const highlight = firstMatch?.highlightedText || firstMatch?.text;

      return {
        id: result.videoId,
        title: result.videoTitle,
        description: matches.length ? matches.slice(0, 2).map((m) => m.text).join(' … ') : undefined,
        thumbnailUrl: `${getApiBaseUrl()}/api/stream/${result.videoId}/thumbnail`,
        duration: result.durationMs ? Math.round(result.durationMs / 1000) : undefined,
        highlights: highlight ? [highlight] : undefined,
        matches,
        score: firstMatch?.score ?? 0,
      };
    });

    return {
      items,
      totalCount: response.totalCount ?? items.length,
      page: response.page ?? page,
      pageSize: response.pageSize ?? pageSize,
    };
  },

  suggest: async (query: string) => {
    const response = await api.get<any>('/api/search/suggest', { q: query });
    if (Array.isArray(response)) return response as string[];
    if (response?.suggestions && Array.isArray(response.suggestions)) {
      return response.suggestions as string[];
    }
    return [];
  },

  facets: async () => {
    const response = await api.get<any>('/api/search/facets');
    const languages = (response.languages ?? []).map((f: any) => ({
      value: f.value,
      count: f.count,
    }));

    const videos = (response.videos ?? []).map((f: any) => ({
      value: f.value,
      count: f.count,
    }));

    return {
      languages,
      tags: [],
      durations: [],
      videos,
    } as SearchFacets;
  },
};

export const authApi = {
  me: () =>
    api.get<User>('/api/auth/me'),

  check: () =>
    api.get<{ authenticated: boolean }>('/api/auth/check'),

  devUsers: () =>
    api.get<DevUser[]>('/api/auth/dev-users'),
};

export const auditApi = {
  list: (page = 1, pageSize = 50, action?: string, targetType?: string) =>
    api.get<PaginatedResponse<AuditLog>>('/api/audit', { page, pageSize, action, targetType }),

  stats: (days = 7) =>
    api.get<AuditStats>('/api/audit/stats', { days }),

  export: (startDate?: string, endDate?: string) =>
    api.get<string>('/api/audit/export', { startDate, endDate }),
};
