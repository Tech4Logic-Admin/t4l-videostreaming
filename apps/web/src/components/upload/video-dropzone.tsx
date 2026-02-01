"use client";

import { useState, useCallback } from "react";
import { useDropzone } from "react-dropzone";
import { Upload, FileVideo, X, AlertCircle } from "lucide-react";
import { cn } from "@/lib/utils";
import { isValidVideoFile, formatFileSize, ALLOWED_VIDEO_TYPES } from "@/lib/upload";

interface VideoDropzoneProps {
  onFileSelect: (file: File) => void;
  disabled?: boolean;
  className?: string;
}

export function VideoDropzone({ onFileSelect, disabled, className }: VideoDropzoneProps) {
  const [error, setError] = useState<string | null>(null);

  const onDrop = useCallback(
    (acceptedFiles: File[]) => {
      setError(null);

      if (acceptedFiles.length === 0) {
        return;
      }

      const file = acceptedFiles[0];
      const validation = isValidVideoFile(file);

      if (!validation.valid) {
        setError(validation.error || "Invalid file");
        return;
      }

      onFileSelect(file);
    },
    [onFileSelect]
  );

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    onDrop,
    accept: ALLOWED_VIDEO_TYPES.reduce(
      (acc, type) => ({ ...acc, [type]: [] }),
      {} as Record<string, string[]>
    ),
    maxFiles: 1,
    disabled,
    onDropRejected: (rejections) => {
      const rejection = rejections[0];
      if (rejection?.errors[0]) {
        setError(rejection.errors[0].message);
      }
    },
  });

  return (
    <div className={className}>
      <div
        {...getRootProps()}
        className={cn(
          "relative flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 transition-colors",
          isDragActive && !isDragReject && "border-primary bg-primary/5",
          isDragReject && "border-destructive bg-destructive/5",
          !isDragActive && !disabled && "border-muted-foreground/25 hover:border-primary/50 hover:bg-muted/50",
          disabled && "cursor-not-allowed opacity-50",
          error && "border-destructive"
        )}
      >
        <input {...getInputProps()} />

        <div className="flex flex-col items-center gap-4 text-center">
          {isDragActive ? (
            <>
              <FileVideo className="h-12 w-12 text-primary" />
              <p className="text-lg font-medium">Drop your video here</p>
            </>
          ) : (
            <>
              <Upload className="h-12 w-12 text-muted-foreground" />
              <div>
                <p className="text-lg font-medium">
                  Drag and drop your video, or{" "}
                  <span className="text-primary underline-offset-4 hover:underline">browse</span>
                </p>
                <p className="mt-1 text-sm text-muted-foreground">
                  MP4, MOV, AVI, MKV, WebM up to 5GB
                </p>
              </div>
            </>
          )}
        </div>
      </div>

      {error && (
        <div className="mt-2 flex items-center gap-2 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          <span>{error}</span>
        </div>
      )}
    </div>
  );
}

interface SelectedFileProps {
  file: File;
  onRemove: () => void;
}

export function SelectedFile({ file, onRemove }: SelectedFileProps) {
  return (
    <div className="flex items-center gap-4 rounded-lg border bg-muted/50 p-4">
      <FileVideo className="h-10 w-10 text-primary" />
      <div className="flex-1 min-w-0">
        <p className="font-medium truncate">{file.name}</p>
        <p className="text-sm text-muted-foreground">{formatFileSize(file.size)}</p>
      </div>
      <button
        onClick={onRemove}
        className="rounded-full p-1 hover:bg-muted"
        aria-label="Remove file"
      >
        <X className="h-5 w-5" />
      </button>
    </div>
  );
}
