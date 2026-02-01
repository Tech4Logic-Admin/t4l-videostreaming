"use client";

import { UploadProgress, formatFileSize, formatDuration } from "@/lib/upload";
import { Progress } from "@/components/ui/progress";
import { CheckCircle, XCircle, Loader2, Pause, Play, X } from "lucide-react";
import { Button } from "@/components/ui/button";

interface UploadProgressCardProps {
  progress: UploadProgress;
  onPause?: () => void;
  onResume?: () => void;
  onCancel?: () => void;
  isPaused?: boolean;
}

export function UploadProgressCard({
  progress,
  onPause,
  onResume,
  onCancel,
  isPaused,
}: UploadProgressCardProps) {
  const percentComplete = Math.round((progress.uploadedBytes / progress.totalBytes) * 100);
  const isComplete = progress.status === "Completed";
  const isFailed = progress.status === "Failed";
  const isUploading = progress.status === "Uploading" || progress.status === "Created";
  const isCompleting = progress.status === "Completing";

  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <StatusIcon status={progress.status} />
            <h4 className="font-medium truncate">{progress.fileName}</h4>
          </div>

          <div className="mt-3 space-y-2">
            <Progress value={percentComplete} className="h-2" />

            <div className="flex items-center justify-between text-sm text-muted-foreground">
              <span>
                {formatFileSize(progress.uploadedBytes)} / {formatFileSize(progress.totalBytes)}
              </span>
              <span>{percentComplete}%</span>
            </div>

            {isUploading && !isPaused && progress.bytesPerSecond && (
              <div className="flex items-center justify-between text-sm text-muted-foreground">
                <span>{formatFileSize(progress.bytesPerSecond)}/s</span>
                {progress.estimatedTimeRemaining && (
                  <span>{formatDuration(progress.estimatedTimeRemaining)} remaining</span>
                )}
              </div>
            )}

            {isUploading && (
              <div className="text-sm text-muted-foreground">
                Chunks: {progress.uploadedChunks} / {progress.totalChunks}
              </div>
            )}

            {isCompleting && (
              <div className="text-sm text-muted-foreground">Finalizing upload...</div>
            )}

            {isComplete && (
              <div className="text-sm text-green-600 dark:text-green-400">
                Upload complete! Video is now processing.
              </div>
            )}

            {isFailed && progress.error && (
              <div className="text-sm text-destructive">{progress.error}</div>
            )}
          </div>
        </div>

        <div className="flex items-center gap-1">
          {isUploading && !isPaused && onPause && (
            <Button variant="ghost" size="icon" onClick={onPause} title="Pause upload">
              <Pause className="h-4 w-4" />
            </Button>
          )}

          {isUploading && isPaused && onResume && (
            <Button variant="ghost" size="icon" onClick={onResume} title="Resume upload">
              <Play className="h-4 w-4" />
            </Button>
          )}

          {!isComplete && !isCompleting && onCancel && (
            <Button variant="ghost" size="icon" onClick={onCancel} title="Cancel upload">
              <X className="h-4 w-4" />
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

function StatusIcon({ status }: { status: UploadProgress["status"] }) {
  switch (status) {
    case "Completed":
      return <CheckCircle className="h-5 w-5 text-green-600" />;
    case "Failed":
      return <XCircle className="h-5 w-5 text-destructive" />;
    case "Completing":
      return <Loader2 className="h-5 w-5 animate-spin text-primary" />;
    default:
      return <Loader2 className="h-5 w-5 animate-spin text-primary" />;
  }
}
