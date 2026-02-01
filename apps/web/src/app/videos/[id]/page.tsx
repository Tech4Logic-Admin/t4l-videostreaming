'use client';

import { useEffect, useRef, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import {
  Play,
  Clock,
  AlertCircle,
  Loader2,
  Info,
  CheckCircle2,
  RefreshCw,
  Download,
  Shield,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { SkeletonText, SkeletonTable } from '@/components/ui/skeleton';
import { useVideo, useStreamingInfo, useProcessingStatus, useToast } from '@/hooks';
import { KeyHighlights } from '@/components/video/KeyHighlights';

export default function VideoDetailsPage() {
  const params = useParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const videoId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const { apiError } = useToast();
  const playerRef = useRef<HTMLVideoElement | null>(null);
  const hlsRef = useRef<any | null>(null);

  const { data: video, isLoading: videoLoading, isError: videoError, error: videoErrorData } = useVideo(videoId);
  const { data: streamInfo, isLoading: streamLoading } = useStreamingInfo(videoId);
  const { data: processingStatus } = useProcessingStatus(videoId);

  // Handle errors from queries
  useEffect(() => {
    if (videoError && videoErrorData) {
      apiError(videoErrorData, 'Unable to load video details');
    }
  }, [apiError, videoError, videoErrorData]);

  // Attach HLS.js when needed
  useEffect(() => {
    const player = playerRef.current;
    const manifest = streamInfo?.masterPlaylistUrl;
    if (!player || !manifest) return;

    const useNative = player.canPlayType('application/vnd.apple.mpegurl');
    if (useNative) {
      player.src = manifest;
      return;
    }

    let cancelled = false;
    (async () => {
      const Hls = (await import('hls.js')).default;
      if (cancelled) return;
      if (Hls.isSupported()) {
        const hls = new Hls();
        hlsRef.current = hls;
        hls.attachMedia(player);
        hls.on(Hls.Events.MEDIA_ATTACHED, () => {
          hls.loadSource(manifest);
        });
      } else {
        player.src = manifest;
      }
    })();

    return () => {
      cancelled = true;
      if (hlsRef.current) {
        hlsRef.current.destroy();
        hlsRef.current = null;
      }
    };
  }, [streamInfo?.masterPlaylistUrl]);

  // Define jumpTo handler before any early returns to follow hooks rules
  const jumpTo = useCallback((startMs: number) => {
    const player = playerRef.current;
    if (player) {
      player.currentTime = startMs / 1000;
      player.play().catch(() => undefined);
    }
  }, []);

  const handleRefresh = useCallback(() => {
    queryClient.invalidateQueries();
  }, [queryClient]);

  if (!videoId) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <Card className="max-w-md w-full">
          <CardContent className="py-8 text-center">
            <AlertCircle className="h-10 w-10 text-red-500 mx-auto mb-3" />
            <p className="text-gray-700 mb-4">No video id provided.</p>
            <Button variant="outline" onClick={() => router.push('/')}>
              Go home
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (videoLoading) {
    return (
      <div className="min-h-screen bg-gray-50">
        <div className="container mx-auto px-4 py-8 space-y-4">
          <SkeletonText className="h-8 w-64" />
          <Card>
            <CardContent className="p-6">
              <SkeletonText className="h-64 w-full" />
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <SkeletonText className="h-4 w-32" />
              <SkeletonText className="h-3 w-24" />
            </CardHeader>
            <CardContent>
              <SkeletonTable rows={4} />
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  if (!video) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <Card className="max-w-md w-full border-red-200">
          <CardContent className="py-10 text-center">
            <AlertCircle className="h-10 w-10 text-red-500 mx-auto mb-3" />
            <h2 className="text-xl font-semibold text-gray-900 mb-2">Video not found</h2>
            <p className="text-gray-600 mb-4">The requested video could not be located.</p>
            <Button variant="outline" onClick={() => router.push('/search')}>
              Back to search
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const statusBadge = getStatusBadge(video.status);
  const durationLabel = video.durationMs ? formatDuration(video.durationMs / 1000) : 'Unknown';
  const isReadyToPlay = !!streamInfo?.masterPlaylistUrl || !!streamInfo?.sourceUrl;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="container mx-auto px-4 py-8 space-y-8">
        {/* Hero */}
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-3">
            <Badge variant="secondary" className={statusBadge.className}>
              {statusBadge.icon}
              {video.status}
            </Badge>
            {video.languageHint && (
              <Badge variant="outline" className="border-blue-200 text-blue-700">
                Lang: {video.languageHint}
              </Badge>
            )}
            {video.tags?.slice(0, 4).map((tag) => (
              <Badge key={tag} variant="outline" className="border-slate-200 text-slate-700">
                {tag}
              </Badge>
            ))}
          </div>

          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="space-y-2">
              <h1 className="text-3xl font-bold text-gray-900 leading-tight">{video.title}</h1>
              {video.description && (
                <p className="text-gray-600 max-w-4xl">{video.description}</p>
              )}
              <div className="flex flex-wrap items-center gap-4 text-sm text-gray-500">
                <span className="inline-flex items-center gap-1">
                  <Clock className="h-4 w-4" />
                  {durationLabel}
                </span>
                <span>Uploaded {new Date(video.createdAt).toLocaleString()}</span>
                <span className="inline-flex items-center gap-1">
                  <Shield className="h-4 w-4" />
                  By {video.createdByOid}
                </span>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" onClick={() => router.push('/search')}>
                Back to search
              </Button>
              <Button variant="secondary" onClick={handleRefresh} className="border-blue-200 text-blue-700">
                <RefreshCw className="h-4 w-4 mr-2" />
                Refresh status
              </Button>
              {streamInfo?.sourceUrl && (
                <Button variant="default" asChild>
                  <a href={streamInfo.sourceUrl} target="_blank" rel="noreferrer">
                    <Download className="h-4 w-4 mr-2" />
                    Download
                  </a>
                </Button>
              )}
            </div>
          </div>
        </div>

        {/* Main grid */}
        <div className="grid gap-6 xl:grid-cols-[2fr,1fr]">
          <Card className="overflow-hidden shadow-sm">
            <CardContent className="p-0">
              {isReadyToPlay ? (
                <video
                  controls
                  className="w-full h-[460px] bg-black"
                  poster={video.thumbnailUrl ?? undefined}
                  ref={playerRef}
                  key={`${streamInfo?.sourceUrl || ''}-${streamInfo?.masterPlaylistUrl || ''}`}
                >
                  {streamInfo?.sourceUrl ? <source src={streamInfo.sourceUrl} type="video/mp4" /> : null}
                  {streamInfo?.masterPlaylistUrl ? (
                    <source src={streamInfo.masterPlaylistUrl} type="application/vnd.apple.mpegurl" />
                  ) : null}
                  Your browser does not support HTML5 video.
                </video>
              ) : (
                <div className="flex items-center justify-center h-[460px] bg-gray-100 text-gray-600">
                  {streamLoading ? (
                    <div className="flex items-center gap-2">
                      <Loader2 className="h-6 w-6 animate-spin" />
                      <span>Preparing streaming assets…</span>
                    </div>
                  ) : (
                    <div className="text-center space-y-2">
                      <Info className="h-8 w-8 mx-auto text-gray-500" />
                      <p>Streaming is not ready yet.</p>
                      <p className="text-sm text-gray-500">We’ll refresh automatically once ready.</p>
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="shadow-sm">
            <CardHeader>
              <CardTitle>Overview</CardTitle>
              <CardDescription>Processing, access and metadata</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 text-sm text-gray-700">
              <div className="flex items-center justify-between">
                <span>Status</span>
                <Badge variant="secondary" className={statusBadge.className}>
                  {video.status}
                </Badge>
              </div>
              <Separator />
              <div className="flex items-center justify-between">
                <span>Progress</span>
                <span className="font-semibold">
                  {processingStatus ? `${processingStatus.progressPercentage}%` : '—'}
                </span>
              </div>
              <Separator />
              <div className="flex items-center justify-between">
                <span>Duration</span>
                <span className="font-semibold">{durationLabel}</span>
              </div>
              <div className="flex items-center justify-between">
                <span>Uploaded</span>
                <span>{new Date(video.createdAt).toLocaleString()}</span>
              </div>
              {video.tags && video.tags.length > 0 && (
                <div className="space-y-2">
                  <span className="text-gray-500">Tags</span>
                  <div className="flex flex-wrap gap-2">
                    {video.tags.map((tag) => (
                      <Badge key={tag} variant="outline" className="border-slate-200 text-slate-700">
                        {tag}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Processing timeline */}
        {processingStatus && (
          <Card className="shadow-sm">
            <CardHeader>
              <CardTitle>Processing Status</CardTitle>
              <CardDescription>
                {processingStatus.progressPercentage}% complete — {processingStatus.overallProcessingStatus}
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
              {processingStatus.jobs.map((job) => (
                <div
                  key={job.stage}
                  className="p-3 rounded-lg border bg-white shadow-[0_1px_2px_rgba(0,0,0,0.03)] flex flex-col gap-1"
                >
                  <div className="flex items-center justify-between">
                    <span className="font-medium text-gray-900">{job.stage}</span>
                    <Badge variant="outline" className={getJobBadge(job.status).className}>
                      {getJobBadge(job.status).label}
                    </Badge>
                  </div>
                  <span className="text-xs text-gray-500">
                    {job.startedAt ? `Started ${new Date(job.startedAt).toLocaleString()}` : 'Not started'}
                  </span>
                  {job.completedAt && (
                    <span className="text-xs text-gray-500">
                      Completed {new Date(job.completedAt).toLocaleString()}
                    </span>
                  )}
                  {job.lastError && (
                    <span className="text-xs text-red-600">Error: {job.lastError}</span>
                  )}
                </div>
              ))}
            </CardContent>
          </Card>
        )}

        {/* Streaming variants + details */}
        <div className="grid gap-6 lg:grid-cols-2">
          <Card className="shadow-sm">
            <CardHeader>
              <CardTitle>Streaming Variants</CardTitle>
              <CardDescription>Available HLS renditions</CardDescription>
            </CardHeader>
            <CardContent>
              {streamLoading ? (
                <SkeletonTable rows={2} />
              ) : streamInfo && streamInfo.variants.length > 0 ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {streamInfo.variants.map((variant) => (
                    <div key={variant.quality} className="p-3 rounded-lg border bg-white flex flex-col gap-1">
                      <div className="flex items-center justify-between">
                        <span className="font-semibold text-gray-900">{variant.quality}</span>
                        <Badge variant="outline">{variant.videoBitrateKbps} kbps</Badge>
                      </div>
                      <span className="text-sm text-gray-600">
                        {variant.width}x{variant.height} • {variant.audioBitrateKbps} kbps audio
                      </span>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-sm text-gray-500 flex items-center gap-2">
                  <Info className="h-4 w-4" />
                  No streaming variants available yet.
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="shadow-sm">
            <CardHeader>
              <CardTitle>Details</CardTitle>
              <CardDescription>File and processing information</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2 text-sm text-gray-700">
              <div className="flex items-center justify-between">
                <span>Status</span>
                <span className="font-semibold">{video.status}</span>
              </div>
              <div className="flex items-center justify-between">
                <span>Duration</span>
                <span className="font-semibold">{durationLabel}</span>
              </div>
              <div className="flex items-center justify-between">
                <span>Created</span>
                <span>{new Date(video.createdAt).toLocaleString()}</span>
              </div>
              {video.tags && video.tags.length > 0 && (
                <div className="space-y-2">
                  <span className="text-gray-500">Tags</span>
                  <div className="flex flex-wrap gap-2">
                    {video.tags.map((tag) => (
                      <Badge key={tag} variant="outline" className="border-slate-200 text-slate-700">
                        {tag}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Key Highlights */}
        <KeyHighlights videoId={videoId} onTimestampClick={jumpTo} />

        {/* Transcript */}
        <Card className="shadow-sm">
          <CardHeader>
            <CardTitle>Transcript</CardTitle>
            <CardDescription>Timeline-aligned segments (ordered by start time)</CardDescription>
          </CardHeader>
          <CardContent>
            {video.transcriptSegments && video.transcriptSegments.length > 0 ? (
              <div className="space-y-3 max-h-[420px] overflow-y-auto pr-2">
                {video.transcriptSegments
                  .slice()
                  .sort((a, b) => a.startMs - b.startMs)
                  .map((segment) => (
                    <div
                      key={segment.id}
                      className="p-3 rounded-md bg-gray-50 border border-gray-100"
                    >
                      <div className="flex items-center gap-2 text-xs text-gray-500 mb-1">
                        <button
                          type="button"
                          onClick={() => jumpTo(segment.startMs)}
                          className="flex items-center gap-1 text-primary hover:text-primary/80"
                        >
                          <Play className="h-3 w-3" />
                          <span>{formatDuration(segment.startMs / 1000)}</span>
                        </button>
                        <span>—</span>
                        <span>{formatDuration(segment.endMs / 1000)}</span>
                        {segment.detectedLanguage && (
                          <Badge variant="secondary" className="text-[11px]">
                            {segment.detectedLanguage}
                          </Badge>
                        )}
                      </div>
                      <p className="text-sm text-gray-800 leading-relaxed">
                        {segment.text}
                      </p>
                    </div>
                  ))}
              </div>
            ) : (
              <div className="flex items-center gap-2 text-sm text-gray-500">
                <AlertCircle className="h-4 w-4" />
                No transcript available yet.
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds)) return '—';
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

function getStatusBadge(status: string) {
  const map: Record<string, { className: string; icon: React.ReactNode }> = {
    Published: { className: 'bg-green-100 text-green-700', icon: <CheckCircle2 className="h-3 w-3" /> },
    Ready: { className: 'bg-green-100 text-green-700', icon: <CheckCircle2 className="h-3 w-3" /> },
    Processing: { className: 'bg-blue-100 text-blue-700', icon: <Loader2 className="h-3 w-3 animate-spin" /> },
    Indexing: { className: 'bg-blue-100 text-blue-700', icon: <Loader2 className="h-3 w-3 animate-spin" /> },
    Moderating: { className: 'bg-amber-100 text-amber-700', icon: <Clock className="h-3 w-3" /> },
    PendingReview: { className: 'bg-amber-100 text-amber-700', icon: <Clock className="h-3 w-3" /> },
    Queued: { className: 'bg-gray-100 text-gray-700', icon: <Clock className="h-3 w-3" /> },
    Failed: { className: 'bg-red-100 text-red-700', icon: <AlertCircle className="h-3 w-3" /> },
    Rejected: { className: 'bg-red-100 text-red-700', icon: <AlertCircle className="h-3 w-3" /> },
    Quarantined: { className: 'bg-amber-100 text-amber-700', icon: <Clock className="h-3 w-3" /> },
  };
  return map[status] || { className: 'bg-gray-100 text-gray-700', icon: <Info className="h-3 w-3" /> };
}

function getJobBadge(status: string) {
  const normalized = status?.toLowerCase();
  if (normalized === 'completed') return { className: 'bg-green-100 text-green-700', label: 'Completed' };
  if (normalized === 'inprogress') return { className: 'bg-blue-100 text-blue-700', label: 'In Progress' };
  if (normalized === 'pending' || normalized === 'skipped') return { className: 'bg-gray-100 text-gray-700', label: status };
  if (normalized === 'failed') return { className: 'bg-red-100 text-red-700', label: 'Failed' };
  return { className: 'bg-gray-100 text-gray-700', label: status || 'Unknown' };
}
