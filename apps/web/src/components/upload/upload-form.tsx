"use client";

import { useState, useCallback, useRef } from "react";
import { useRouter } from "next/navigation";
import { VideoDropzone, SelectedFile } from "./video-dropzone";
import { UploadProgressCard } from "./upload-progress";
import { UploadProgress, ChunkedUploader, createUploader } from "@/lib/upload";
import { useAuthFetch } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Upload } from "lucide-react";

interface UploadFormProps {
  onSuccess?: (_videoAssetId: string) => void;
}

const LANGUAGES = [
  { value: "auto", label: "Auto-detect" },
  { value: "en", label: "English" },
  { value: "es", label: "Spanish" },
  { value: "fr", label: "French" },
  { value: "de", label: "German" },
  { value: "pt", label: "Portuguese" },
  { value: "it", label: "Italian" },
  { value: "nl", label: "Dutch" },
  { value: "ja", label: "Japanese" },
  { value: "ko", label: "Korean" },
  { value: "zh", label: "Chinese" },
  { value: "hi", label: "Hindi" },
  { value: "ar", label: "Arabic" },
];

type UploadState = "idle" | "selected" | "uploading" | "complete" | "error";

export function UploadForm({ onSuccess }: UploadFormProps) {
  const router = useRouter();
  const { getDevHeaders } = useAuthFetch();
  const uploaderRef = useRef<ChunkedUploader | null>(null);

  const [uploadState, setUploadState] = useState<UploadState>("idle");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [progress, setProgress] = useState<UploadProgress | null>(null);
  const [isPaused, setIsPaused] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form fields
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [tags, setTags] = useState("");
  const [languageHint, setLanguageHint] = useState("auto");

  const handleFileSelect = useCallback((file: File) => {
    setSelectedFile(file);
    setUploadState("selected");
    setError(null);

    // Auto-fill title from filename
    const nameWithoutExt = file.name.replace(/\.[^/.]+$/, "");
    setTitle(nameWithoutExt);
  }, []);

  const handleRemoveFile = useCallback(() => {
    setSelectedFile(null);
    setUploadState("idle");
    setTitle("");
    setDescription("");
    setTags("");
    setLanguageHint("auto");
    setError(null);
  }, []);

  const handleStartUpload = useCallback(async () => {
    if (!selectedFile || !title.trim()) {
      setError("Please provide a title for the video");
      return;
    }

    setUploadState("uploading");
    setError(null);

    const uploader = createUploader({
      file: selectedFile,
      metadata: {
        title: title.trim(),
        description: description.trim() || undefined,
        tags: tags
          .split(",")
          .map((t) => t.trim())
          .filter(Boolean),
        languageHint: languageHint === "auto" ? undefined : languageHint,
      },
      getAuthHeaders: getDevHeaders,
      onProgress: (p) => {
        setProgress(p);
        if (p.status === "Failed" && p.error) {
          setError(p.error);
        }
      },
      onComplete: (result) => {
        setUploadState("complete");
        onSuccess?.(result.videoAssetId);
      },
      onError: (err) => {
        setUploadState("error");
        setError(err.message);
      },
    });

    uploaderRef.current = uploader;
    await uploader.start();
  }, [selectedFile, title, description, tags, languageHint, getDevHeaders, onSuccess]);

  const handlePause = useCallback(() => {
    uploaderRef.current?.pause();
    setIsPaused(true);
  }, []);

  const handleResume = useCallback(() => {
    uploaderRef.current?.resume();
    setIsPaused(false);
  }, []);

  const handleCancel = useCallback(() => {
    uploaderRef.current?.cancel();
    setUploadState("idle");
    setProgress(null);
    setIsPaused(false);
    handleRemoveFile();
  }, [handleRemoveFile]);

  const handleUploadAnother = useCallback(() => {
    setUploadState("idle");
    setSelectedFile(null);
    setProgress(null);
    setTitle("");
    setDescription("");
    setTags("");
    setLanguageHint("auto");
    setError(null);
    uploaderRef.current = null;
  }, []);

  // Show progress view during/after upload
  if (uploadState === "uploading" || uploadState === "complete" || (uploadState === "error" && progress)) {
    return (
      <div className="space-y-6">
        {progress && (
          <UploadProgressCard
            progress={progress}
            isPaused={isPaused}
            onPause={uploadState === "uploading" ? handlePause : undefined}
            onResume={uploadState === "uploading" ? handleResume : undefined}
            onCancel={uploadState === "uploading" ? handleCancel : undefined}
          />
        )}

        {uploadState === "complete" && (
          <div className="flex gap-4">
            <Button onClick={handleUploadAnother}>Upload Another Video</Button>
            <Button variant="outline" onClick={() => router.push("/my-videos")}>View My Videos</Button>
          </div>
        )}

        {uploadState === "error" && (
          <div className="flex gap-4">
            <Button onClick={handleUploadAnother}>Try Again</Button>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* File Selection */}
      {uploadState === "idle" && (
        <VideoDropzone onFileSelect={handleFileSelect} />
      )}

      {uploadState === "selected" && selectedFile && (
        <SelectedFile file={selectedFile} onRemove={handleRemoveFile} />
      )}

      {/* Metadata Form */}
      {uploadState === "selected" && (
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="title">Title *</Label>
            <Input
              id="title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Enter video title"
              maxLength={500}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Enter video description (optional)"
              rows={3}
              maxLength={5000}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="tags">Tags</Label>
              <Input
                id="tags"
                value={tags}
                onChange={(e) => setTags(e.target.value)}
                placeholder="tag1, tag2, tag3"
              />
              <p className="text-xs text-muted-foreground">Separate tags with commas</p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="language">Primary Language</Label>
              <Select value={languageHint} onValueChange={setLanguageHint}>
                <SelectTrigger id="language">
                  <SelectValue placeholder="Select language" />
                </SelectTrigger>
                <SelectContent>
                  {LANGUAGES.map((lang) => (
                    <SelectItem key={lang.value} value={lang.value}>
                      {lang.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {error && (
            <div className="text-sm text-destructive">{error}</div>
          )}

          <div className="flex gap-4 pt-4">
            <Button onClick={handleStartUpload} disabled={!title.trim()}>
              <Upload className="mr-2 h-4 w-4" />
              Start Upload
            </Button>
            <Button variant="outline" onClick={handleRemoveFile}>
              Cancel
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
