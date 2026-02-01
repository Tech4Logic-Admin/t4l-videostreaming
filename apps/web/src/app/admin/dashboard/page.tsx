'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import {
  BarChart3,
  Video,
  Shield,
  Clock,
  Upload,
  TrendingUp,
  AlertCircle,
  CheckCircle,
  XCircle,
  RefreshCw,
  Trash2
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { SkeletonText } from '@/components/ui/skeleton';
import { useAuditStats, useVideos, usePendingVideos, useToast, useDeleteVideo } from '@/hooks';
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

export default function AdminDashboardPage() {
  return (
    <RequireAuth roles={['Admin']} showAccessDenied>
      <AdminDashboardContent />
    </RequireAuth>
  );
}

function AdminDashboardContent() {
  const { apiError } = useToast();
  const [deleteVideoId, setDeleteVideoId] = useState<string | null>(null);
  const [deleteVideoTitle, setDeleteVideoTitle] = useState<string>('');
  const deleteVideo = useDeleteVideo();

  const {
    data: auditStats,
    isLoading: statsLoading,
    isError: statsError,
    error: statsErrorData,
    refetch: refetchStats
  } = useAuditStats(7);

  const {
    data: videos,
    isLoading: videosLoading,
    isError: videosError,
    refetch: refetchVideos
  } = useVideos(1, 5);

  const {
    data: pendingVideos,
    isLoading: pendingLoading,
    refetch: refetchPending
  } = usePendingVideos(1, 10);

  // Handle errors
  useEffect(() => {
    if (statsError && statsErrorData) {
      apiError(statsErrorData, 'Failed to load audit stats');
    }
  }, [statsError, statsErrorData, apiError]);

  const handleRefreshAll = () => {
    refetchStats();
    refetchVideos();
    refetchPending();
  };

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

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="container mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 mb-2">
              Admin Dashboard
            </h1>
            <p className="text-gray-600">
              Overview of system activity and content management.
            </p>
          </div>
          <Button onClick={handleRefreshAll} variant="outline">
            <RefreshCw className="h-4 w-4 mr-2" />
            Refresh
          </Button>
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          {/* Total Videos */}
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Total Videos</p>
                  <p className="text-2xl font-bold text-gray-900">
                    {videosLoading ? '-' : videos?.totalCount || 0}
                  </p>
                </div>
                <div className="h-12 w-12 bg-blue-100 rounded-full flex items-center justify-center">
                  <Video className="h-6 w-6 text-blue-600" />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Pending Review */}
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Pending Review</p>
                  <p className="text-2xl font-bold text-yellow-600">
                    {pendingLoading ? '-' : pendingVideos?.totalCount || 0}
                  </p>
                </div>
                <div className="h-12 w-12 bg-yellow-100 rounded-full flex items-center justify-center">
                  <Clock className="h-6 w-6 text-yellow-600" />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Total Actions (7 days) */}
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Actions (7 days)</p>
                  <p className="text-2xl font-bold text-gray-900">
                    {statsLoading ? '-' : auditStats?.totalActions || 0}
                  </p>
                </div>
                <div className="h-12 w-12 bg-purple-100 rounded-full flex items-center justify-center">
                  <BarChart3 className="h-6 w-6 text-purple-600" />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Security Events */}
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Security Events</p>
                  <p className="text-2xl font-bold text-red-600">
                    {statsLoading ? '-' : auditStats?.securityEvents || 0}
                  </p>
                </div>
                <div className="h-12 w-12 bg-red-100 rounded-full flex items-center justify-center">
                  <Shield className="h-6 w-6 text-red-600" />
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
          {/* Activity Breakdown */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <TrendingUp className="h-5 w-5" />
                Activity Breakdown
              </CardTitle>
              <CardDescription>
                Actions in the last 7 days
              </CardDescription>
            </CardHeader>
            <CardContent>
              {statsLoading ? (
                <div className="space-y-4">
                  <SkeletonText className="w-full h-4" />
                  <SkeletonText className="w-full h-4" />
                  <SkeletonText className="w-full h-4" />
                </div>
              ) : statsError ? (
                <div className="text-center py-8">
                  <AlertCircle className="h-8 w-8 text-red-400 mx-auto mb-2" />
                  <p className="text-sm text-red-600">Failed to load stats</p>
                </div>
              ) : auditStats?.actionBreakdown && Array.isArray(auditStats.actionBreakdown) && auditStats.actionBreakdown.length > 0 ? (
                <div className="space-y-4">
                  {auditStats.actionBreakdown.slice(0, 5).map((item) => {
                    const percentage = auditStats.totalActions
                      ? Math.round((item.count / auditStats.totalActions) * 100)
                      : 0;
                    return (
                      <div key={item.action}>
                        <div className="flex items-center justify-between text-sm mb-1">
                          <span className="font-medium">{item.action}</span>
                          <span className="text-gray-500">{item.count}</span>
                        </div>
                        <Progress value={percentage} className="h-2" />
                      </div>
                    );
                  })}
                </div>
              ) : (
                <div className="text-center py-8 text-gray-500">
                  No activity data available
                </div>
              )}
            </CardContent>
          </Card>

          {/* Quick Actions */}
          <Card>
            <CardHeader>
              <CardTitle>Quick Actions</CardTitle>
              <CardDescription>
                Common administrative tasks
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 gap-4">
                <Link href="/review">
                  <Button
                    variant="outline"
                    className="w-full h-24 flex-col gap-2"
                  >
                    <Clock className="h-8 w-8 text-yellow-600" />
                    <span>Review Queue</span>
                    {pendingVideos && pendingVideos.totalCount > 0 && (
                      <span className="text-xs text-yellow-600">
                        {pendingVideos.totalCount} pending
                      </span>
                    )}
                  </Button>
                </Link>
                <Link href="/upload">
                  <Button
                    variant="outline"
                    className="w-full h-24 flex-col gap-2"
                  >
                    <Upload className="h-8 w-8 text-blue-600" />
                    <span>Upload Video</span>
                  </Button>
                </Link>
                <Link href="/admin/audit">
                  <Button
                    variant="outline"
                    className="w-full h-24 flex-col gap-2"
                  >
                    <Shield className="h-8 w-8 text-purple-600" />
                    <span>Audit Logs</span>
                  </Button>
                </Link>
                <Link href="/search">
                  <Button
                    variant="outline"
                    className="w-full h-24 flex-col gap-2"
                  >
                    <Video className="h-8 w-8 text-green-600" />
                    <span>Search Videos</span>
                  </Button>
                </Link>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Recent Videos */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <div>
              <CardTitle>Recent Videos</CardTitle>
              <CardDescription>
                Latest uploaded videos
              </CardDescription>
            </div>
            <Link href="/admin/videos">
              <Button variant="ghost" size="sm">
                View All
              </Button>
            </Link>
          </CardHeader>
          <CardContent>
            {videosLoading ? (
              <div className="space-y-4">
                {[1, 2, 3].map((i) => (
                  <div key={i} className="flex items-center gap-4">
                    <SkeletonText className="w-16 h-10" />
                    <div className="flex-1">
                      <SkeletonText className="w-48 h-4 mb-2" />
                      <SkeletonText className="w-24 h-3" />
                    </div>
                  </div>
                ))}
              </div>
            ) : videosError ? (
              <div className="text-center py-8">
                <AlertCircle className="h-8 w-8 text-red-400 mx-auto mb-2" />
                <p className="text-sm text-red-600">Failed to load videos</p>
              </div>
            ) : videos && Array.isArray(videos.videos) && videos.videos.length > 0 ? (
              <div className="space-y-4">
                {videos.videos.map((video) => (
                  <div
                    key={video.id}
                    className="flex items-center gap-4 p-3 bg-gray-50 rounded-lg"
                  >
                    {/* Thumbnail */}
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

                    {/* Info */}
                    <div className="flex-1 min-w-0">
                      <Link
                        href={`/videos/${video.id}`}
                        className="font-medium text-gray-900 hover:text-primary truncate block"
                      >
                        {video.title}
                      </Link>
                      <div className="text-xs text-gray-500">
                        {new Date(video.createdAt).toLocaleDateString()}
                      </div>
                    </div>

                    {/* Status */}
                    <StatusBadge status={video.status} />

                    {/* Delete Button */}
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
                ))}
              </div>
            ) : (
              <div className="text-center py-8 text-gray-500">
                No videos found
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

function StatusBadge({ status }: { status: string }) {
  const config: Record<string, { icon: React.ReactNode; className: string }> = {
    Ready: {
      icon: <CheckCircle className="h-3 w-3" />,
      className: 'bg-green-100 text-green-800',
    },
    Published: {
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

  const { icon, className } = config[status] || {
    icon: null,
    className: 'bg-gray-100 text-gray-800',
  };

  return (
    <span
      className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${className}`}
    >
      {icon}
      {status}
    </span>
  );
}
