'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
  Shield,
  AlertCircle,
  Clock,
  User,
  Download,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  Loader2,
  Search,
  Activity
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { SkeletonTable } from '@/components/ui/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/hooks';
import { useAuth } from '@/lib/auth';
import { api, type AuditLog, type AuditStats, type PaginatedResponse } from '@/lib/api-client';

const ACTION_TYPES = [
  'All Actions',
  'VideoCreated',
  'VideoUpdated',
  'VideoDeleted',
  'VideoApproved',
  'VideoRejected',
  'UserLogin',
  'UserLogout',
  'PermissionChanged',
  'SearchPerformed',
];

const TARGET_TYPES = [
  'All Types',
  'Video',
  'User',
  'Permission',
  'Search',
  'System',
];

export default function AuditPage() {
  const router = useRouter();
  const { user, isAuthenticated, isLoading: authLoading } = useAuth();
  const { apiError, success } = useToast();

  const [logs, setLogs] = useState<AuditLog[]>([]);
  const [stats, setStats] = useState<AuditStats | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [isLoading, setIsLoading] = useState(true);
  const [isStatsLoading, setIsStatsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [actionFilter, setActionFilter] = useState('All Actions');
  const [targetTypeFilter, setTargetTypeFilter] = useState('All Types');
  const [searchQuery, setSearchQuery] = useState('');

  const totalPages = Math.ceil(totalCount / pageSize);

  // Check if user is admin
  const isAdmin = user?.roles?.includes('Admin') || user?.roles?.includes('admin');

  // Redirect if not authenticated or not admin
  useEffect(() => {
    if (!authLoading) {
      if (!isAuthenticated) {
        router.push('/');
      } else if (!isAdmin) {
        router.push('/');
        apiError(new Error('Access denied'), 'You do not have permission to view audit logs');
      }
    }
  }, [authLoading, isAuthenticated, isAdmin, router, apiError]);

  // Fetch audit stats
  const fetchStats = async () => {
    setIsStatsLoading(true);
    try {
      const response = await api.get<AuditStats>('/api/audit/stats', { days: 7 });
      setStats(response);
    } catch (err) {
      console.error('Failed to load audit stats:', err);
    } finally {
      setIsStatsLoading(false);
    }
  };

  // Fetch audit logs
  const fetchLogs = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const params: Record<string, string | number> = {
        page,
        pageSize,
      };

      if (actionFilter !== 'All Actions') {
        params.action = actionFilter;
      }
      if (targetTypeFilter !== 'All Types') {
        params.targetType = targetTypeFilter;
      }

      const response = await api.get<PaginatedResponse<AuditLog>>('/api/audit', params);
      setLogs(response.items);
      setTotalCount(response.totalCount);
    } catch (err) {
      apiError(err, 'Failed to load audit logs');
      setError('Failed to load audit logs');
    } finally {
      setIsLoading(false);
    }
  };

  // Export audit logs
  const handleExport = async () => {
    try {
      const response = await api.get<string>('/api/audit/export', {
        format: 'csv',
      });

      // Create download
      const blob = new Blob([response], { type: 'text/csv' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `audit-logs-${new Date().toISOString().split('T')[0]}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      success('Audit logs exported successfully');
    } catch (err) {
      apiError(err, 'Failed to export audit logs');
    }
  };

  useEffect(() => {
    if (isAuthenticated && isAdmin) {
      fetchStats();
      fetchLogs();
    }
  }, [isAuthenticated, isAdmin, page, actionFilter, targetTypeFilter]);

  if (authLoading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (!isAuthenticated || !isAdmin) {
    return null;
  }

  const getActionBadgeColor = (action: string): string => {
    if (action.includes('Delete') || action.includes('Rejected')) {
      return 'bg-red-100 text-red-800';
    }
    if (action.includes('Created') || action.includes('Approved')) {
      return 'bg-green-100 text-green-800';
    }
    if (action.includes('Updated') || action.includes('Changed')) {
      return 'bg-blue-100 text-blue-800';
    }
    if (action.includes('Login') || action.includes('Logout')) {
      return 'bg-purple-100 text-purple-800';
    }
    return 'bg-gray-100 text-gray-800';
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="container mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 mb-2 flex items-center gap-2">
              <Shield className="h-8 w-8" />
              Audit Logs
            </h1>
            <p className="text-gray-600">
              Monitor system activity and security events
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => { fetchStats(); fetchLogs(); }} disabled={isLoading}>
              <RefreshCw className={`h-4 w-4 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
            <Button variant="outline" onClick={handleExport}>
              <Download className="h-4 w-4 mr-2" />
              Export
            </Button>
          </div>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Total Actions (7 days)</p>
                  <p className="text-2xl font-bold text-gray-900">
                    {isStatsLoading ? '-' : stats?.totalActions || 0}
                  </p>
                </div>
                <Activity className="h-8 w-8 text-blue-500" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Unique Users</p>
                  <p className="text-2xl font-bold text-green-600">
                    {isStatsLoading ? '-' : stats?.uniqueActors || 0}
                  </p>
                </div>
                <User className="h-8 w-8 text-green-500" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">Security Events</p>
                  <p className="text-2xl font-bold text-red-600">
                    {isStatsLoading ? '-' : stats?.securityEvents || 0}
                  </p>
                </div>
                <Shield className="h-8 w-8 text-red-500" />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">This Page</p>
                  <p className="text-2xl font-bold text-gray-900">{totalCount}</p>
                </div>
                <Clock className="h-8 w-8 text-gray-500" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Filters */}
        <Card className="mb-6">
          <CardContent className="pt-6">
            <div className="flex flex-wrap gap-4">
              <div className="flex-1 min-w-[200px]">
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Action Type
                </label>
                <Select value={actionFilter} onValueChange={setActionFilter}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select action type" />
                  </SelectTrigger>
                  <SelectContent>
                    {ACTION_TYPES.map((action) => (
                      <SelectItem key={action} value={action}>
                        {action}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex-1 min-w-[200px]">
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Target Type
                </label>
                <Select value={targetTypeFilter} onValueChange={setTargetTypeFilter}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select target type" />
                  </SelectTrigger>
                  <SelectContent>
                    {TARGET_TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {type}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex-1 min-w-[200px]">
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Search
                </label>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
                  <Input
                    placeholder="Search by actor or target..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="pl-9"
                  />
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Audit Logs Table */}
        <Card>
          <CardHeader>
            <CardTitle>Activity Log</CardTitle>
            <CardDescription>
              Detailed record of all system actions
            </CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <SkeletonTable rows={10} />
            ) : error ? (
              <div className="text-center py-12">
                <AlertCircle className="h-12 w-12 text-red-400 mx-auto mb-4" />
                <p className="text-red-600 mb-4">{error}</p>
                <Button onClick={fetchLogs} variant="outline">
                  Try Again
                </Button>
              </div>
            ) : logs.length === 0 ? (
              <div className="text-center py-12">
                <Shield className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                <h3 className="text-lg font-medium text-gray-900 mb-2">
                  No audit logs found
                </h3>
                <p className="text-gray-500">
                  No activity matching your filters was found.
                </p>
              </div>
            ) : (
              <>
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr className="border-b">
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Timestamp</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Action</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Target</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">Actor</th>
                        <th className="text-left py-3 px-4 font-medium text-gray-500">IP Address</th>
                      </tr>
                    </thead>
                    <tbody>
                      {logs.map((log) => (
                        <tr key={log.id} className="border-b hover:bg-gray-50">
                          <td className="py-3 px-4 text-sm text-gray-500">
                            <div>
                              {new Date(log.timestamp).toLocaleDateString()}
                            </div>
                            <div className="text-xs">
                              {new Date(log.timestamp).toLocaleTimeString()}
                            </div>
                          </td>
                          <td className="py-3 px-4">
                            <Badge className={getActionBadgeColor(log.action)}>
                              {log.action}
                            </Badge>
                          </td>
                          <td className="py-3 px-4 text-sm">
                            <div className="font-medium text-gray-900">
                              {log.targetType}
                            </div>
                            {log.targetId && (
                              <div className="text-xs text-gray-500 font-mono truncate max-w-[150px]">
                                {log.targetId}
                              </div>
                            )}
                          </td>
                          <td className="py-3 px-4 text-sm">
                            <div className="font-medium text-gray-900">
                              {log.actorName || 'Unknown'}
                            </div>
                            <div className="text-xs text-gray-500 font-mono truncate max-w-[150px]">
                              {log.actorId}
                            </div>
                          </td>
                          <td className="py-3 px-4 text-sm text-gray-500 font-mono">
                            {log.ipAddress || '-'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Pagination */}
                {totalPages > 1 && (
                  <div className="flex items-center justify-between mt-6">
                    <p className="text-sm text-gray-500">
                      Showing {(page - 1) * pageSize + 1} to {Math.min(page * pageSize, totalCount)} of {totalCount} logs
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
