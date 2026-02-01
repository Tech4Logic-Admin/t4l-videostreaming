'use client';

import { useState, useEffect, useCallback } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { Search, Filter, X, ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { SkeletonVideoGrid } from '@/components/ui/skeleton';
import { useSearch, useSearchFacets, useToast } from '@/hooks';
import Link from 'next/link';

export default function SearchPage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const { apiError } = useToast();

  const initialQuery = searchParams.get('q') || '';
  const initialPage = parseInt(searchParams.get('page') || '1', 10);

  const [query, setQuery] = useState(initialQuery);
  const [searchQuery, setSearchQuery] = useState(initialQuery);
  const [page, setPage] = useState(initialPage);
  const [showFilters, setShowFilters] = useState(false);

  const {
    data: searchResults,
    isLoading,
    isError,
    error,
    isFetching
  } = useSearch(searchQuery, page, 20);

  const { data: facets } = useSearchFacets();

  // Handle search errors
  useEffect(() => {
    if (isError && error) {
      apiError(error, 'Search failed');
    }
  }, [isError, error, apiError]);

  // Update URL when search params change
  const updateUrl = useCallback((newQuery: string, newPage: number) => {
    const params = new URLSearchParams();
    if (newQuery) params.set('q', newQuery);
    if (newPage > 1) params.set('page', String(newPage));
    router.push(`/search?${params.toString()}`);
  }, [router]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setSearchQuery(query);
    setPage(1);
    updateUrl(query, 1);
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    updateUrl(searchQuery, newPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const clearSearch = () => {
    setQuery('');
    setSearchQuery('');
    setPage(1);
    router.push('/search');
  };

  const totalPages = searchResults
    ? Math.ceil(searchResults.totalCount / 20)
    : 0;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="container mx-auto px-4 py-8">
        {/* Search Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-4">Search Videos</h1>

          <form onSubmit={handleSearch} className="flex gap-2">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-5 w-5 text-gray-400" />
              <Input
                type="text"
                placeholder="Search for videos..."
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                className="pl-10 pr-10 h-12 text-lg"
              />
              {query && (
                <button
                  type="button"
                  onClick={clearSearch}
                  className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  <X className="h-5 w-5" />
                </button>
              )}
            </div>
            <Button type="submit" size="lg" disabled={!query.trim()}>
              Search
            </Button>
            <Button
              type="button"
              variant="outline"
              size="lg"
              onClick={() => setShowFilters(!showFilters)}
            >
              <Filter className="h-5 w-5" />
            </Button>
          </form>
        </div>

        <div className="flex gap-6">
          {/* Filters Sidebar */}
          {showFilters && facets && (
            <aside className="w-64 shrink-0">
              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Filters</CardTitle>
                </CardHeader>
                <CardContent className="space-y-6">
                  {/* Languages */}
                  {facets.languages && facets.languages.length > 0 && (
                    <div>
                      <h3 className="font-medium mb-2">Language</h3>
                      <div className="space-y-1">
                        {facets.languages.map((lang) => (
                          <label
                            key={lang.value}
                            className="flex items-center gap-2 text-sm cursor-pointer hover:bg-gray-50 p-1 rounded"
                          >
                            <input type="checkbox" className="rounded" />
                            <span>{lang.value}</span>
                            <Badge variant="secondary" className="ml-auto">
                              {lang.count}
                            </Badge>
                          </label>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Tags */}
                  {facets.tags && facets.tags.length > 0 && (
                    <div>
                      <h3 className="font-medium mb-2">Tags</h3>
                      <div className="flex flex-wrap gap-1">
                        {facets.tags.slice(0, 10).map((tag) => (
                          <Badge
                            key={tag.value}
                            variant="outline"
                            className="cursor-pointer hover:bg-primary hover:text-primary-foreground"
                          >
                            {tag.value} ({tag.count})
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Duration */}
                  {facets.durations && facets.durations.length > 0 && (
                    <div>
                      <h3 className="font-medium mb-2">Duration</h3>
                      <div className="space-y-1">
                        {facets.durations.map((dur) => (
                          <label
                            key={dur.value}
                            className="flex items-center gap-2 text-sm cursor-pointer hover:bg-gray-50 p-1 rounded"
                          >
                            <input type="checkbox" className="rounded" />
                            <span>{dur.value}</span>
                            <Badge variant="secondary" className="ml-auto">
                              {dur.count}
                            </Badge>
                          </label>
                        ))}
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>
            </aside>
          )}

          {/* Results */}
          <main className="flex-1">
            {/* Results Info */}
            {searchQuery && (
              <div className="mb-4 flex items-center justify-between">
                <p className="text-gray-600">
                  {isLoading ? (
                    'Searching...'
                  ) : searchResults ? (
                    <>
                      Found <strong>{searchResults.totalCount}</strong> results for{' '}
                      <strong>&ldquo;{searchQuery}&rdquo;</strong>
                    </>
                  ) : null}
                </p>
                {isFetching && !isLoading && (
                  <span className="text-sm text-gray-500">Updating...</span>
                )}
              </div>
            )}

            {/* Loading State */}
            {isLoading && <SkeletonVideoGrid count={8} />}

            {/* Error State */}
            {isError && (
              <Card className="border-red-200 bg-red-50">
                <CardContent className="py-8 text-center">
                  <p className="text-red-600 mb-4">
                    Sorry, we couldn&apos;t complete your search. Please try again.
                  </p>
                  <Button onClick={() => setSearchQuery(query)} variant="outline">
                    Retry Search
                  </Button>
                </CardContent>
              </Card>
            )}

            {/* Empty State */}
            {!isLoading && !isError && searchQuery && searchResults?.items.length === 0 && (
              <Card>
                <CardContent className="py-12 text-center">
                  <Search className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                  <h3 className="text-lg font-medium text-gray-900 mb-2">
                    No results found
                  </h3>
                  <p className="text-gray-500 mb-4">
                    We couldn&apos;t find any videos matching &ldquo;{searchQuery}&rdquo;.
                    <br />
                    Try different keywords or check your spelling.
                  </p>
                  <Button onClick={clearSearch} variant="outline">
                    Clear Search
                  </Button>
                </CardContent>
              </Card>
            )}

            {/* Initial State */}
            {!isLoading && !searchQuery && (
              <Card>
                <CardContent className="py-12 text-center">
                  <Search className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                  <h3 className="text-lg font-medium text-gray-900 mb-2">
                    Search for videos
                  </h3>
                  <p className="text-gray-500">
                    Enter keywords to search across all videos including titles,
                    descriptions, and transcriptions.
                  </p>
                </CardContent>
              </Card>
            )}

            {/* Results Grid */}
            {!isLoading && searchResults && searchResults.items.length > 0 && (
              <>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                  {searchResults.items.map((item) => (
                    <Link
                      key={item.id}
                      href={`/videos/${item.id}`}
                      className="group"
                    >
                      <Card className="overflow-hidden hover:shadow-lg transition-shadow">
                        {/* Thumbnail */}
                        <div className="aspect-video bg-gray-200 relative">
                          {item.thumbnailUrl ? (
                            <img
                              src={item.thumbnailUrl}
                              alt={item.title}
                              className="w-full h-full object-cover"
                            />
                          ) : (
                            <div className="w-full h-full flex items-center justify-center">
                              <span className="text-gray-400">No thumbnail</span>
                            </div>
                          )}
                          {item.duration && (
                            <div className="absolute bottom-2 right-2 bg-black/75 text-white text-xs px-2 py-1 rounded">
                              {formatDuration(item.duration)}
                            </div>
                          )}
                        </div>

                        <CardContent className="p-3">
                          <h3 className="font-medium text-gray-900 line-clamp-2 group-hover:text-primary">
                            {item.title}
                          </h3>
                          {item.description && (
                            <p className="text-sm text-gray-500 line-clamp-2 mt-1">
                              {item.description}
                            </p>
                          )}
                          {/* Highlights */}
                          {item.highlights && item.highlights.length > 0 && (
                            <div className="mt-2 text-xs text-gray-600 bg-yellow-50 p-2 rounded">
                              <span
                                dangerouslySetInnerHTML={{
                                  __html: item.highlights[0],
                                }}
                              />
                            </div>
                          )}
                        </CardContent>
                      </Card>
                    </Link>
                  ))}
                </div>

                {/* Pagination */}
                {totalPages > 1 && (
                  <div className="flex items-center justify-center gap-2 mt-8">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={page <= 1}
                      onClick={() => handlePageChange(page - 1)}
                    >
                      <ChevronLeft className="h-4 w-4 mr-1" />
                      Previous
                    </Button>

                    <div className="flex items-center gap-1">
                      {generatePageNumbers(page, totalPages).map((pageNum, i) =>
                        pageNum === '...' ? (
                          <span key={`ellipsis-${i}`} className="px-2">
                            ...
                          </span>
                        ) : (
                          <Button
                            key={pageNum}
                            variant={page === pageNum ? 'default' : 'outline'}
                            size="sm"
                            className="w-10"
                            onClick={() => handlePageChange(pageNum as number)}
                          >
                            {pageNum}
                          </Button>
                        )
                      )}
                    </div>

                    <Button
                      variant="outline"
                      size="sm"
                      disabled={page >= totalPages}
                      onClick={() => handlePageChange(page + 1)}
                    >
                      Next
                      <ChevronRight className="h-4 w-4 ml-1" />
                    </Button>
                  </div>
                )}
              </>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

function generatePageNumbers(
  currentPage: number,
  totalPages: number
): (number | string)[] {
  const pages: (number | string)[] = [];
  const delta = 2;

  if (totalPages <= 7) {
    for (let i = 1; i <= totalPages; i++) {
      pages.push(i);
    }
    return pages;
  }

  pages.push(1);

  if (currentPage > delta + 2) {
    pages.push('...');
  }

  for (
    let i = Math.max(2, currentPage - delta);
    i <= Math.min(totalPages - 1, currentPage + delta);
    i++
  ) {
    pages.push(i);
  }

  if (currentPage < totalPages - delta - 1) {
    pages.push('...');
  }

  if (totalPages > 1) {
    pages.push(totalPages);
  }

  return pages;
}
