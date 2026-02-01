'use client';

import { useState, useEffect } from 'react';
import {
  CheckCircle,
  XCircle,
  Clock,
  ChevronLeft,
  ChevronRight,
  Play,
  AlertCircle,
  Loader2
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { SkeletonTable, SkeletonText } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  usePendingVideos,
  useApproveVideo,
  useRejectVideo,
  useToast
} from '@/hooks';

export default function ReviewPage() {
  const [page, setPage] = useState(1);
  const [selectedVideo, setSelectedVideo] = useState<string | null>(null);
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');

  const { apiError } = useToast();

  const {
    data: pendingVideos,
    isLoading,
    isError,
    error,
    refetch,
  } = usePendingVideos(page, 20);

  const approveMutation = useApproveVideo();
  const rejectMutation = useRejectVideo();

  // Handle errors
  useEffect(() => {
    if (isError && error) {
      apiError(error, 'Failed to load pending videos');
    }
  }, [isError, error, apiError]);

  const handleApprove = async (videoId: string) => {
    try {
      await approveMutation.mutateAsync({ id: videoId });
      setSelectedVideo(null);
    } catch (_err) {
      // Error is handled by mutation
    }
  };

  const handleReject = async () => {
    if (!selectedVideo || !rejectReason.trim()) return;

    try {
      await rejectMutation.mutateAsync({ id: selectedVideo, reason: rejectReason });
      setRejectReason('');
      setRejectDialogOpen(false);
      setSelectedVideo(null);
    } catch (err) {
      // Error is handled by mutation
    }
  };

  const openRejectDialog = (videoId: string) => {
    setSelectedVideo(videoId);
    setRejectDialogOpen(true);
  };

  const totalPages = pendingVideos
    ? Math.ceil(pendingVideos.totalCount / 20)
    : 0;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="container mx-auto px-4 py-8">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">
            Content Review Queue
          </h1>
          <p className="text-gray-600">
            Review and moderate uploaded videos before they are published.
          </p>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Pending Review</p>
                  <p className="text-2xl font-bold text-yellow-600">
                    {isLoading ? '-' : pendingVideos?.totalCount || 0}
                  </p>
                </div>
                <Clock className="h-10 w-10 text-yellow-200" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Approved Today</p>
                  <p className="text-2xl font-bold text-green-600">-</p>
                </div>
                <CheckCircle className="h-10 w-10 text-green-200" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Rejected Today</p>
                  <p className="text-2xl font-bold text-red-600">-</p>
                </div>
                <XCircle className="h-10 w-10 text-red-200" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Loading State */}
        {isLoading && (
          <Card>
            <CardHeader>
              <SkeletonText className="w-48 h-6" />
            </CardHeader>
            <CardContent>
              <SkeletonTable rows={5} />
            </CardContent>
          </Card>
        )}

        {/* Error State */}
        {isError && (
          <Card className="border-red-200 bg-red-50">
            <CardContent className="py-12 text-center">
              <AlertCircle className="h-12 w-12 text-red-400 mx-auto mb-4" />
              <h3 className="text-lg font-medium text-red-900 mb-2">
                Failed to Load Review Queue
              </h3>
              <p className="text-red-600 mb-4">
                Something went wrong while loading the pending videos.
              </p>
              <Button onClick={() => refetch()} variant="outline">
                Try Again
              </Button>
            </CardContent>
          </Card>
        )}

        {/* Empty State */}
        {!isLoading && !isError && (!pendingVideos || pendingVideos.items.length === 0) && (
          <Card>
            <CardContent className="py-12 text-center">
              <CheckCircle className="h-12 w-12 text-green-400 mx-auto mb-4" />
              <h3 className="text-lg font-medium text-gray-900 mb-2">
                All Caught Up!
              </h3>
              <p className="text-gray-500">
                There are no videos waiting for review at the moment.
              </p>
            </CardContent>
          </Card>
        )}

        {/* Videos List */}
        {!isLoading && pendingVideos && pendingVideos.items.length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle>Pending Videos</CardTitle>
              <CardDescription>
                Review each video before approving or rejecting.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {pendingVideos.items.map((video) => (
                  <div
                    key={video.id}
                    className="flex items-center gap-4 p-4 bg-gray-50 rounded-lg hover:bg-gray-100 transition-colors"
                  >
                    {/* Thumbnail */}
                    <div className="w-32 h-20 bg-gray-200 rounded overflow-hidden shrink-0">
                      {video.thumbnailUrl ? (
                        <img
                          src={video.thumbnailUrl}
                          alt={video.title}
                          className="w-full h-full object-cover"
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center">
                          <Play className="h-8 w-8 text-gray-400" />
                        </div>
                      )}
                    </div>

                    {/* Info */}
                    <div className="flex-1 min-w-0">
                      <h3 className="font-medium text-gray-900 truncate">
                        {video.title}
                      </h3>
                      {video.description && (
                        <p className="text-sm text-gray-500 line-clamp-2 mt-1">
                          {video.description}
                        </p>
                      )}
                      <div className="flex items-center gap-4 mt-2 text-xs text-gray-400">
                        <span>
                          Uploaded {new Date(video.createdAt).toLocaleDateString()}
                        </span>
                        <span>by {video.createdByOid || 'Unknown'}</span>
                        {video.durationMs && (
                          <span>{formatDuration(video.durationMs / 1000)}</span>
                        )}
                      </div>
                    </div>

                    {/* Actions */}
                    <div className="flex items-center gap-2 shrink-0">
                      <Button
                        variant="outline"
                        size="sm"
                        asChild
                      >
                        <a
                          href={`/videos/${video.id}`}
                          target="_blank"
                          rel="noopener noreferrer"
                        >
                          Preview
                        </a>
                      </Button>
                      <Button
                        variant="default"
                        size="sm"
                        className="bg-green-600 hover:bg-green-700"
                        onClick={() => handleApprove(video.id)}
                        disabled={approveMutation.isPending}
                      >
                        {approveMutation.isPending && approveMutation.variables?.id === video.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <>
                            <CheckCircle className="h-4 w-4 mr-1" />
                            Approve
                          </>
                        )}
                      </Button>
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => openRejectDialog(video.id)}
                        disabled={rejectMutation.isPending}
                      >
                        <XCircle className="h-4 w-4 mr-1" />
                        Reject
                      </Button>
                    </div>
                  </div>
                ))}
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex items-center justify-center gap-2 mt-6">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page <= 1}
                    onClick={() => setPage(page - 1)}
                  >
                    <ChevronLeft className="h-4 w-4 mr-1" />
                    Previous
                  </Button>
                  <span className="text-sm text-gray-500">
                    Page {page} of {totalPages}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page >= totalPages}
                    onClick={() => setPage(page + 1)}
                  >
                    Next
                    <ChevronRight className="h-4 w-4 ml-1" />
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        )}

        {/* Reject Dialog */}
        <Dialog open={rejectDialogOpen} onOpenChange={setRejectDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Reject Video</DialogTitle>
              <DialogDescription>
                Please provide a reason for rejecting this video. This will be
                visible to the uploader.
              </DialogDescription>
            </DialogHeader>
            <div className="py-4">
              <Textarea
                placeholder="Enter rejection reason..."
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                rows={4}
              />
            </div>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => {
                  setRejectDialogOpen(false);
                  setRejectReason('');
                  setSelectedVideo(null);
                }}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={handleReject}
                disabled={!rejectReason.trim() || rejectMutation.isPending}
              >
                {rejectMutation.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin mr-2" />
                ) : null}
                Reject Video
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </div>
  );
}

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}
