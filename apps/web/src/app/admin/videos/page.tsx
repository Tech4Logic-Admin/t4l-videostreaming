'use client';

import { useState } from 'react';
import Link from 'next/link';
import {
  Video,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
  Trash2
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { SkeletonTable } from '@/components/ui/skeleton';
import { useVideos, useDeleteVideo } from '@/hooks';
import { RequireAuth } from '@/components/auth';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';

export default function AdminVideosPage() {
  return (
    <RequireAuth roles={['Admin']} showAccessDenied>
      <AdminVideosContent />
    </RequireAuth>
  );
}

function AdminVideosContent() {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [deleteVideoId, setDeleteVideoId] = useState<string | null>(null);
  const [deleteVideoTitle, setDeleteVideoTitle] = useState<string>('');
  const deleteVideo = useDeleteVideo();

  const {
    data: videos,
    isLoading,
    isError,
    refetch
  } = useVideos(page, pageSize);

  const totalPages = videos?.totalPages || 1;

  const handleDeleteClick = (videoId: string, videoTitle: string) => {
    setDeleteVideoId(videoId);
    setDeleteVideoTitle(videoTitle);
  };

  const handleDeleteConfirm = () => {
    if (deleteVideoId) {
      deleteVideo.mutate(deleteVideoId);
      setDeleteVideoId(null);
      setDeleteVideoTitle('');
    }
  };

  const handleDeleteCancel = () => {
    setDeleteVideoId(null);
    setDeleteVideoTitle('');
  };

  const statusConfig: Record<string, { icon: React.ReactNode; className: string }> = {
    Published: {
      icon: <CheckCircle className="h-3 w-3" />,
      className: 'bg-green-100 text-green-800',
    },
    Ready: {
      icon: <CheckCircle className="h-3 w-3" />,
      className: 'bg-green-100 text-green-800',
    },
    Processing: {
      icon: <RefreshCw className="h-3 w-3 animate-spin" />,
      className: 'bg-blue-100 text-blue-800',
    },
    Indexing: {
      icon: <RefreshCw className="h-3 w-3 animate-spin" />,
      className: 'bg-blue-100 text-blue-800',
    },
    Moderating: {
      icon: <RefreshCw className="h-3 w-3 animate-spin" />,
      className: 'bg-blue-100 text-blue-800',
    },
    Queued: {
      icon: <Clock className="h-3 w-3" />,
      className: 'bg-gray-100 text-gray-800',
    },
    PendingReview: {
      icon: <Clock className="h-3 w-3" />,
      className: 'bg-yellow-100 text-yellow-800',
    },
    Quarantined: {
      icon: <AlertCircle className="h-3 w-3" />,
      className: 'bg-yellow-100 text-yellow-800',
    },
    Failed: {
      icon: <XCircle className="h-3 w-3" />,
      className: 'bg-red-100 text-red-800',
    },
    Rejected: {
      icon: <XCircle className="h-3 w-3" />,
      className: 'bg-red-100 text-red-800',
    },
  };

  const formatDuration = (ms: number | undefined | null): string => {
    if (!ms) return '--:--';
    const seconds = Math.floor(ms / 1000);
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="container mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">All Videos</h1>
            <p className="text-gray-500 mt-1">
              Manage all videos in the system
            </p>
          </div>
          <div className="flex gap-3">
            <Button variant="outline" onClick={() => refetch()}>
              <RefreshCw className="h-4 w-4 mr-2" />
              Refresh
            </Button>
            <Link href="/admin/dashboard">
              <Button variant="outline">Back to Dashboard</Button>
            </Link>
          </div>
        </div>

        {/* Videos Table */}
        <Card>
          <CardHeader>
            <CardTitle>Videos ({videos?.totalCount || 0})</CardTitle>
            <CardDescription>
              All videos uploaded to the platform
            </CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <SkeletonTable rows={5} />
            ) : isError ? (
              <div className="text-center py-12">
                <AlertCircle className="h-12 w-12 text-red-400 mx-auto mb-4" />
                <p className="text-red-600 mb-4">Failed to load videos</p>
                <Button onClick={() => refetch()}>Try Again</Button>
              </div>
            ) : videos?.videos && videos.videos.length > 0 ? (
              <>
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr className="border-b">
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Video</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Status</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Duration</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Uploaded By</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Date</th>
                        <th className="text-right py-3 px-4 font-medium text-gray-500">Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {videos.videos.map((video) => {
                        const status = statusConfig[video.status] || statusConfig.Queued;
                        return (
                          <tr key={video.id} className="border-b hover:bg-gray-50">
                            <td className="py-3 px-4">
                              <div className="flex items-center gap-3">
                                <div className="w-16 h-10 bg-gray-200 rounded overflow-hidden shrink-0">
                                  {video.thumbnailUrl ? (
                                    <img
                                      src={video.thumbnailUrl}
                                      alt={video.title}
                                      className="w-full h-full object-cover"
                                    />
                                  ) : (
                                    <div className="w-full h-full flex items-center justify-center">
                                      <Video className="h-4 w-4 text-gray-400" />
                                    </div>
                                  )}
                                </div>
                                <div className="min-w-0">
                                  <Link
                                    href={`/videos/${video.id}`}
                                    className="font-medium text-gray-900 hover:text-primary truncate block max-w-xs"
                                  >
                                    {video.title}
                                  </Link>
                                </div>
                              </div>
                            </td>
                            <td className="py-3 px-4">
                              <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${status.className}`}>
                                {status.icon}
                                {video.status}
                              </span>
                            </td>
                            <td className="py-3 px-4 text-gray-600">
                              {formatDuration(video.durationMs)}
                            </td>
                            <td className="py-3 px-4 text-gray-600 truncate max-w-[150px]">
                              {video.createdByOid}
                            </td>
                            <td className="py-3 px-4 text-gray-600">
                              {new Date(video.createdAt).toLocaleDateString()}
                            </td>
                            <td className="py-3 px-4 text-right">
                              <div className="flex items-center justify-end gap-2">
                                <Link href={`/videos/${video.id}`}>
                                  <Button variant="ghost" size="sm">
                                    View
                                  </Button>
                                </Link>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  className="text-red-600 hover:text-red-700 hover:bg-red-50"
                                  onClick={() => handleDeleteClick(video.id, video.title)}
                                  disabled={deleteVideo.isPending}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {/* Pagination */}
                {totalPages > 1 && (
                  <div className="flex items-center justify-between mt-6 pt-4 border-t">
                    <p className="text-sm text-gray-500">
                      Showing {(page - 1) * pageSize + 1} - {Math.min(page * pageSize, videos.totalCount)} of {videos.totalCount} videos
                    </p>
                    <div className="flex items-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        disabled={page === 1}
                      >
                        <ChevronLeft className="h-4 w-4" />
                        Previous
                      </Button>
                      <span className="text-sm text-gray-600">
                        Page {page} of {totalPages}
                      </span>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                        disabled={page === totalPages}
                      >
                        Next
                        <ChevronRight className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                )}
              </>
            ) : (
              <div className="text-center py-12">
                <Video className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                <p className="text-gray-500">No videos found</p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Delete Confirmation Dialog */}
      <AlertDialog open={!!deleteVideoId} onOpenChange={(open) => !open && handleDeleteCancel()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Video</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete &quot;{deleteVideoTitle}&quot;? This action cannot be undone.
              All video data, including transcriptions, thumbnails, and encoded variants will be permanently removed.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={handleDeleteCancel}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDeleteConfirm}
              className="bg-red-600 hover:bg-red-700 focus:ring-red-600"
            >
              {deleteVideo.isPending ? 'Deleting...' : 'Delete'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
