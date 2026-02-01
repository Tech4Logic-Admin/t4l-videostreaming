"use client";

import { useAuth } from "@/lib/auth";
import { UploadForm } from "@/components/upload";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { AlertTriangle, Upload, FileVideo } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

export default function UploadPage() {
  const { user, isAuthenticated, isLoading } = useAuth();
  const router = useRouter();

  // Check if user can upload (from permissions)
  const canUpload = user?.permissions.canUpload ?? false;

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push("/");
    }
  }, [isLoading, isAuthenticated, router]);

  if (isLoading) {
    return (
      <div className="container mx-auto py-8 px-4">
        <div className="flex items-center justify-center min-h-[400px]">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null; // Will redirect
  }

  if (!canUpload) {
    return (
      <div className="container mx-auto py-8 px-4">
        <Alert variant="destructive" className="max-w-2xl mx-auto">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Access Denied</AlertTitle>
          <AlertDescription>
            You don&apos;t have permission to upload videos. Please contact an administrator
            if you believe this is an error.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const handleUploadComplete = (videoAssetId: string) => {
    // Navigate to the video details page or library
    router.push(`/videos/${videoAssetId}`);
  };

  return (
    <div className="container mx-auto py-8 px-4">
      <div className="max-w-3xl mx-auto space-y-6">
        {/* Page Header */}
        <div className="flex items-center gap-3">
          <div className="p-2 bg-primary/10 rounded-lg">
            <Upload className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h1 className="text-2xl font-bold">Upload Video</h1>
            <p className="text-muted-foreground">
              Upload a video file to add it to your library
            </p>
          </div>
        </div>

        {/* Upload Guidelines */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base flex items-center gap-2">
              <FileVideo className="h-4 w-4" />
              Upload Guidelines
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="text-sm text-muted-foreground space-y-1">
              <li>- Supported formats: MP4, MOV, AVI, MKV, WebM, MPEG</li>
              <li>- Maximum file size: 5 GB</li>
              <li>- Videos are uploaded in chunks for reliability</li>
              <li>- You can pause and resume uploads</li>
              <li>- Video processing may take a few minutes after upload</li>
            </ul>
          </CardContent>
        </Card>

        {/* Upload Form */}
        <Card>
          <CardHeader>
            <CardTitle>New Video</CardTitle>
            <CardDescription>
              Select a video file and add details to upload
            </CardDescription>
          </CardHeader>
          <CardContent>
            <UploadForm onSuccess={handleUploadComplete} />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
