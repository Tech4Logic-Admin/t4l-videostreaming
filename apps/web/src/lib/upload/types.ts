// Upload API types and hooks

export interface CreateUploadSessionRequest {
  fileName: string;
  contentType: string;
  fileSize: number;
  chunkSize?: number;
  title: string;
  description?: string;
  tags?: string[];
  languageHint?: string;
}

export interface UploadSession {
  sessionId: string;
  fileName: string;
  fileSize: number;
  chunkSize: number;
  totalChunks: number;
  uploadedChunks: number;
  status: UploadSessionStatus;
  expiresAt: string;
  blobPath: string;
  videoAssetId?: string;
}

export type UploadSessionStatus =
  | "Created"
  | "Uploading"
  | "Completing"
  | "Completed"
  | "Failed"
  | "Expired";

export interface ChunkUploadUrl {
  url: string;
  blockId: string;
  expiresAt: string;
  chunkIndex: number;
  chunkSize: number;
  requiredHeaders: Record<string, string>;
}

export interface ChunkCompleteResponse {
  chunkIndex: number;
  uploadedChunks: number;
  totalChunks: number;
  isComplete: boolean;
}

export interface UploadCompleteResponse {
  sessionId: string;
  videoAssetId: string;
  status: string;
}

export interface UploadProgress {
  sessionId: string;
  fileName: string;
  totalBytes: number;
  uploadedBytes: number;
  uploadedChunks: number;
  totalChunks: number;
  status: UploadSessionStatus;
  error?: string;
  bytesPerSecond?: number;
  estimatedTimeRemaining?: number;
}

export const DEFAULT_CHUNK_SIZE = 4 * 1024 * 1024; // 4MB
export const MAX_FILE_SIZE = 5 * 1024 * 1024 * 1024; // 5GB
export const ALLOWED_VIDEO_TYPES = [
  "video/mp4",
  "video/quicktime",
  "video/x-msvideo",
  "video/x-matroska",
  "video/webm",
  "video/mpeg",
];

export function isValidVideoFile(file: File): { valid: boolean; error?: string } {
  if (!ALLOWED_VIDEO_TYPES.includes(file.type)) {
    return {
      valid: false,
      error: `Invalid file type: ${file.type}. Allowed types: MP4, MOV, AVI, MKV, WebM, MPEG`,
    };
  }

  if (file.size > MAX_FILE_SIZE) {
    return {
      valid: false,
      error: `File too large: ${formatFileSize(file.size)}. Maximum size is ${formatFileSize(MAX_FILE_SIZE)}`,
    };
  }

  if (file.size === 0) {
    return { valid: false, error: "File is empty" };
  }

  return { valid: true };
}

export function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
}

export function formatDuration(seconds: number): string {
  if (seconds < 60) return `${Math.round(seconds)}s`;
  if (seconds < 3600) {
    const mins = Math.floor(seconds / 60);
    const secs = Math.round(seconds % 60);
    return `${mins}m ${secs}s`;
  }
  const hours = Math.floor(seconds / 3600);
  const mins = Math.floor((seconds % 3600) / 60);
  return `${hours}h ${mins}m`;
}
