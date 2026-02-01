'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  Video,
  Upload,
  Play,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Loader2,
  RefreshCw,
  Eye,
  MoreHorizontal
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';

import { SkeletonTable } from '@/components/ui/skeleton';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useToast } from '@/hooks';
import { useAuth } from '@/lib/auth';
import { videoApi, type Video as VideoType, type VideoListResponse } from '@/lib/api-client';

export default function MyVideosPage() {
  const router = useRouter();
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const { apiError } = useToast();

  const [videos, setVideos] = useState<VideoType[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const totalPages = Math.ceil(totalCount / pageSize);

  // Redirect if not authenticated
  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      router.push('/');
    }
  }, [authLoading, isAuthenticated, router]);

  // Fetch user's videos
  const fetchVideos = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response: VideoListResponse = await videoApi.my(page, pageSize);
      setVideos(response.items);
      setTotalCount(response.totalCount);
    } catch (err) {
      apiError(err, 'Failed to load your videos');
      setError('Failed to load videos');
    } finally {
      setIsLoading(false);
    }
  }, [apiError, page, pageSize]);

  useEffect(() => {
    if (isAuthenticated) {
      fetchVideos();
    }
  }, [fetchVideos, isAuthenticated, page]);

  if (authLoading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  const statusConfig: Record<string, { icon: React.ReactNode; className: string; label: string }> = {
    Published: {
      icon: <CheckCircle className="h-3 w-3" />,
      className: 'bg-green-100 text-green-800',
      label: 'Published',
    },
    Processing: {
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
      className: 'bg-blue-100 text-blue-800',
      label: 'Processing',
    },
    Queued: {
      icon: <Clock className="h-3 w-3" />,
      className: 'bg-gray-100 text-gray-800',
      label: 'Queued',
    },
    PendingReview: {
      icon: <Clock className="h-3 w-3" />,
      className: 'bg-yellow-100 text-yellow-800',
      label: 'Pending Review',
    },
    Failed: {
      icon: <XCircle className="h-3 w-3" />,
      className: 'bg-red-100 text-red-800',
      label: 'Failed',
    },
    Rejected: {
      icon: <XCircle className="h-3 w-3" />,
      className: 'bg-red-100 text-red-800',
      label: 'Rejected',
    },
    Ready: {
      icon: <CheckCircle className="h-3 w-3" />,
      className: 'bg-green-100 text-green-800',
      label: 'Ready',
    },
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="container mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 mb-2">My Videos</h1>
            <p className="text-gray-600">
              Manage and track your uploaded videos
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={fetchVideos} disabled={isLoading}>
              <RefreshCw className={`h-4 w-4 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
            <Button asChild>
              <Link href="/upload">
                <Upload className="h-4 w-4 mr-2" />
                Upload Video
              </Link>
            </Button>
          </div>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Total Videos</p>
                  <p className="text-2xl font-bold text-gray-900">{totalCount}</p>
                </div>
                <Video className="h-8 w-8 text-blue-500" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Published</p>
                  <p className="text-2xl font-bold text-green-600">
                    {videos.filter(v => v.status === 'Published' || v.status === 'Ready').length}
                  </p>
                </div>
                <CheckCircle className="h-8 w-8 text-green-500" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Processing</p>
                  <p className="text-2xl font-bold text-blue-600">
                    {videos.filter(v => v.status === 'Processing' || v.status === 'Queued').length}
                  </p>
                </div>
                <Clock className="h-8 w-8 text-blue-500" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Pending Review</p>
                  <p className="text-2xl font-bold text-yellow-600">
                    {videos.filter(v => v.status === 'PendingReview').length}
                  </p>
                </div>
                <AlertCircle className="h-8 w-8 text-yellow-500" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Videos Table */}
        <Card>
          <CardHeader>
            <CardTitle>Your Uploads</CardTitle>
            <CardDescription>
              All videos you have uploaded to the platform
            </CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <SkeletonTable rows={5} />
            ) : error ? (
              <div className="text-center py-12">
                <AlertCircle className="h-12 w-12 text-red-400 mx-auto mb-4" />
                <p className="text-red-600 mb-4">{error}</p>
                <Button onClick={fetchVideos} variant="outline">
                  Try Again
                </Button>
              </div>
            ) : videos.length === 0 ? (
              <div className="text-center py-12">
                <Video className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                <h3 className="text-lg font-medium text-gray-900 mb-2">
                  No videos yet
                </h3>
                <p className="text-gray-500 mb-4">
                  Start by uploading your first video
                </p>
                <Button asChild>
                  <Link href="/upload">
                    <Upload className="h-4 w-4 mr-2" />
                    Upload Video
                  </Link>
                </Button>
              </div>
            ) : (
              <>
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr className="border-b">
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Video</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Status</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Uploaded</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Duration</th>
                        <th className="text-right py-3 px-4 font-medium text-gray-500">Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {videos.map((video) => {
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
                                      <Play className="h-4 w-4 text-gray-400" />
                                    </div>
                                  )}
                                </div>
                                <div className="min-w-0">
                                  <p className="font-medium text-gray-900 truncate max-w-xs">
                                    {video.title}
                                  </p>
                                  {video.description && (
                                    <p className="text-sm text-gray-500 truncate max-w-xs">
                                      {video.description}
                                    </p>
                                  )}
                                </div>
                              </div>
                            </td>
                            <td className="py-3 px-4">
                              <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${status.className}`}>
                                {status.icon}
                                {status.label}
                              </span>
                            </td>
                            <td className="py-3 px-4 text-sm text-gray-500">
                              {new Date(video.createdAt).toLocaleDateString()}
                            </td>
                            <td className="py-3 px-4 text-sm text-gray-500">
                              {video.durationMs ? formatDuration(video.durationMs / 1000) : '-'}
                            </td>
                            <td className="py-3 px-4">
                              <div className="flex justify-end">
                                <DropdownMenu>
                                  <DropdownMenuTrigger asChild>
                                    <Button variant="ghost" size="sm">
                                      <MoreHorizontal className="h-4 w-4" />
                                    </Button>
                                  </DropdownMenuTrigger>
                                  <DropdownMenuContent align="end">
                                    <DropdownMenuItem asChild>
                                      <Link href={`/videos/${video.id}`}>
                                        <Eye className="h-4 w-4 mr-2" />
                                        View Details
                                      </Link>
                                    </DropdownMenuItem>
                                    {(video.status === 'Published' || video.status === 'Ready') && (
                                      <DropdownMenuItem asChild>
                                        <Link href={`/videos/${video.id}`}>
                                          <Play className="h-4 w-4 mr-2" />
                                          Watch Video
                                        </Link>
                                      </DropdownMenuItem>
                                    )}
                                  </DropdownMenuContent>
                                </DropdownMenu>
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
                  <div className="flex items-center justify-between mt-6">
                    <p className="text-sm text-gray-500">
                      Showing {(page - 1) * pageSize + 1} to {Math.min(page * pageSize, totalCount)} of {totalCount} videos
                    </p>
                    <div className="flex items-center gap-2">
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
                  </div>
                )}
              </>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}
