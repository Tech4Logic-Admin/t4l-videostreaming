import {
  UploadSession,
  ChunkCompleteResponse,
  UploadCompleteResponse,
  CreateUploadSessionRequest,
  UploadProgress,
  DEFAULT_CHUNK_SIZE,
} from "./types";
import { getApiBaseUrl } from "../api-client";

export interface ChunkedUploaderOptions {
  file: File;
  metadata: {
    title: string;
    description?: string;
    tags?: string[];
    languageHint?: string;
  };
  chunkSize?: number;
  maxConcurrentChunks?: number;
  onProgress?: (_progress: UploadProgress) => void;
  onComplete?: (_response: UploadCompleteResponse) => void;
  onError?: (_error: Error) => void;
  getAuthHeaders: () => Record<string, string>;
  apiBaseUrl?: string;
}

export class ChunkedUploader {
  private file: File;
  private metadata: ChunkedUploaderOptions["metadata"];
  private chunkSize: number;
  private maxConcurrentChunks: number;
  private onProgress?: (progress: UploadProgress) => void;
  private onComplete?: (response: UploadCompleteResponse) => void;
  private onError?: (error: Error) => void;
  private getAuthHeaders: () => Record<string, string>;
  private apiBaseUrl: string;

  private session: UploadSession | null = null;
  private abortController: AbortController | null = null;
  private isPaused = false;
  private uploadedChunks: Set<number> = new Set();
  private startTime = 0;
  private uploadedBytes = 0;

  constructor(options: ChunkedUploaderOptions) {
    this.file = options.file;
    this.metadata = options.metadata;
    this.chunkSize = options.chunkSize || DEFAULT_CHUNK_SIZE;
    this.maxConcurrentChunks = options.maxConcurrentChunks || 3;
    this.onProgress = options.onProgress;
    this.onComplete = options.onComplete;
    this.onError = options.onError;
    this.getAuthHeaders = options.getAuthHeaders;
    this.apiBaseUrl = options.apiBaseUrl || `${getApiBaseUrl()}/api`;
  }

  async start(): Promise<void> {
    this.abortController = new AbortController();
    this.startTime = Date.now();
    this.uploadedBytes = 0;

    try {
      // Create upload session
      this.session = await this.createSession();
      this.reportProgress();

      // Upload all chunks
      await this.uploadAllChunks();

      // Complete the upload
      const result = await this.completeUpload();
      this.onComplete?.(result);
    } catch (error) {
      if (error instanceof Error && error.name === "AbortError") {
        return; // Upload was cancelled
      }
      this.onError?.(error instanceof Error ? error : new Error(String(error)));
    }
  }

  pause(): void {
    this.isPaused = true;
  }

  resume(): void {
    if (this.isPaused && this.session) {
      this.isPaused = false;
      this.uploadAllChunks().catch((error) => {
        this.onError?.(error instanceof Error ? error : new Error(String(error)));
      });
    }
  }

  cancel(): void {
    this.abortController?.abort();
    this.isPaused = false;
    this.session = null;
  }

  getSession(): UploadSession | null {
    return this.session;
  }

  private async createSession(): Promise<UploadSession> {
    const request: CreateUploadSessionRequest = {
      fileName: this.file.name,
      contentType: this.file.type,
      fileSize: this.file.size,
      chunkSize: this.chunkSize,
      title: this.metadata.title,
      description: this.metadata.description,
      tags: this.metadata.tags,
      languageHint: this.metadata.languageHint,
    };

    const response = await fetch(`${this.apiBaseUrl}/upload/sessions`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...this.getAuthHeaders(),
      },
      body: JSON.stringify(request),
      signal: this.abortController?.signal,
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.error || `Failed to create upload session: ${response.status}`);
    }

    return response.json();
  }

  private async uploadAllChunks(): Promise<void> {
    if (!this.session) return;

    const totalChunks = this.session.totalChunks;
    const pendingChunks: number[] = [];

    // Find chunks that haven't been uploaded yet
    for (let i = 0; i < totalChunks; i++) {
      if (!this.uploadedChunks.has(i)) {
        pendingChunks.push(i);
      }
    }

    // Upload chunks with concurrency limit
    while (pendingChunks.length > 0 && !this.isPaused) {
      const batch = pendingChunks.splice(0, this.maxConcurrentChunks);
      await Promise.all(batch.map((chunkIndex) => this.uploadChunk(chunkIndex)));
    }
  }

  private async uploadChunk(chunkIndex: number): Promise<void> {
    if (!this.session || this.isPaused) return;

    const start = chunkIndex * this.chunkSize;
    const end = Math.min(start + this.chunkSize, this.file.size);
    const chunk = this.file.slice(start, end);

    // Upload directly through API (simpler than SAS URL for local dev)
    const response = await fetch(
      `${this.apiBaseUrl}/upload/sessions/${this.session.sessionId}/chunks/${chunkIndex}`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/octet-stream",
          ...this.getAuthHeaders(),
        },
        body: chunk,
        signal: this.abortController?.signal,
      }
    );

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.error || `Failed to upload chunk ${chunkIndex}: ${response.status}`);
    }

    const result: ChunkCompleteResponse = await response.json();

    this.uploadedChunks.add(chunkIndex);
    this.uploadedBytes += chunk.size;

    if (this.session) {
      this.session.uploadedChunks = result.uploadedChunks;
      this.session.status = "Uploading";
    }

    this.reportProgress();
  }

  private async completeUpload(): Promise<UploadCompleteResponse> {
    if (!this.session) {
      throw new Error("No active upload session");
    }

    if (this.session) {
      this.session.status = "Completing";
    }
    this.reportProgress();

    const response = await fetch(
      `${this.apiBaseUrl}/upload/sessions/${this.session.sessionId}/complete`,
      {
        method: "POST",
        headers: this.getAuthHeaders(),
        signal: this.abortController?.signal,
      }
    );

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.error || `Failed to complete upload: ${response.status}`);
    }

    const result: UploadCompleteResponse = await response.json();

    if (this.session) {
      this.session.status = "Completed";
      this.session.videoAssetId = result.videoAssetId;
    }
    this.reportProgress();

    return result;
  }

  private reportProgress(): void {
    if (!this.session || !this.onProgress) return;

    const elapsedSeconds = (Date.now() - this.startTime) / 1000;
    const bytesPerSecond = elapsedSeconds > 0 ? this.uploadedBytes / elapsedSeconds : 0;
    const remainingBytes = this.file.size - this.uploadedBytes;
    const estimatedTimeRemaining =
      bytesPerSecond > 0 ? remainingBytes / bytesPerSecond : undefined;

    this.onProgress({
      sessionId: this.session.sessionId,
      fileName: this.file.name,
      totalBytes: this.file.size,
      uploadedBytes: this.uploadedBytes,
      uploadedChunks: this.session.uploadedChunks,
      totalChunks: this.session.totalChunks,
      status: this.session.status,
      bytesPerSecond,
      estimatedTimeRemaining,
    });
  }
}

// Hook for easier React integration
export function createUploader(options: ChunkedUploaderOptions): ChunkedUploader {
  return new ChunkedUploader(options);
}
