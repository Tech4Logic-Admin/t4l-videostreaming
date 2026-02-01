'use client';

import { useState, useCallback } from 'react';
import {
  Lightbulb,
  ChevronDown,
  Globe,
  Loader2,
  Star,
  Quote,
  TrendingUp,
  Megaphone,
  Hash,
  Languages,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useVideoHighlights, useSupportedLanguages, useTranslateHighlights } from '@/hooks';
import type { VideoHighlight } from '@/lib/api-client';

interface KeyHighlightsProps {
  videoId: string;
  onTimestampClick?: (ms: number) => void;
}

// Category icons mapping
const categoryIcons: Record<string, React.ReactNode> = {
  key_point: <Lightbulb className="h-3.5 w-3.5" />,
  promise: <Star className="h-3.5 w-3.5" />,
  announcement: <Megaphone className="h-3.5 w-3.5" />,
  statistic: <TrendingUp className="h-3.5 w-3.5" />,
  quote: <Quote className="h-3.5 w-3.5" />,
  default: <Hash className="h-3.5 w-3.5" />,
};

// Category colors mapping
const categoryColors: Record<string, string> = {
  key_point: 'bg-blue-100 text-blue-700 border-blue-200',
  promise: 'bg-amber-100 text-amber-700 border-amber-200',
  announcement: 'bg-purple-100 text-purple-700 border-purple-200',
  statistic: 'bg-green-100 text-green-700 border-green-200',
  quote: 'bg-rose-100 text-rose-700 border-rose-200',
  default: 'bg-gray-100 text-gray-700 border-gray-200',
};

function formatDuration(ms: number): string {
  const seconds = Math.floor(ms / 1000);
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

function getCategoryIcon(category: string): React.ReactNode {
  return categoryIcons[category] || categoryIcons.default;
}

function getCategoryColor(category: string): string {
  return categoryColors[category] || categoryColors.default;
}

function formatCategoryLabel(category: string): string {
  return category
    .split('_')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}

export function KeyHighlights({ videoId, onTimestampClick }: KeyHighlightsProps) {
  const [selectedLanguage, setSelectedLanguage] = useState<string>('en');

  const { data: highlightsData, isLoading: highlightsLoading } = useVideoHighlights(videoId, selectedLanguage);
  const { data: languagesData, isLoading: languagesLoading } = useSupportedLanguages();
  const translateMutation = useTranslateHighlights();

  const handleLanguageChange = useCallback(async (languageCode: string) => {
    setSelectedLanguage(languageCode);

    // If not English and translation might be needed, trigger translation
    if (languageCode !== 'en') {
      translateMutation.mutate({ id: videoId, targetLanguage: languageCode });
    }
  }, [videoId, translateMutation]);

  const currentLanguageName = languagesData?.languages.find(
    (l) => l.code === selectedLanguage
  )?.name ?? 'English';

  if (highlightsLoading || languagesLoading) {
    return (
      <Card className="shadow-sm">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Lightbulb className="h-5 w-5 text-amber-500" />
            Key Highlights
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-center py-8 text-gray-500">
            <Loader2 className="h-6 w-6 animate-spin mr-2" />
            Loading highlights...
          </div>
        </CardContent>
      </Card>
    );
  }

  if (!highlightsData?.highlights || highlightsData.highlights.length === 0) {
    return (
      <Card className="shadow-sm">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Lightbulb className="h-5 w-5 text-amber-500" />
            Key Highlights
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-2 text-sm text-gray-500 py-4">
            <Lightbulb className="h-4 w-4" />
            No highlights available yet. Highlights will appear once AI processing completes.
          </div>
        </CardContent>
      </Card>
    );
  }

  const { highlights, summary, sourceLanguage } = highlightsData;

  return (
    <Card className="shadow-sm">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Lightbulb className="h-5 w-5 text-amber-500" />
              Key Highlights
            </CardTitle>
            <CardDescription className="mt-1">
              {highlights.length} key points extracted from video
              {sourceLanguage && sourceLanguage !== 'en' && (
                <span className="ml-2 text-xs">
                  (Original: {languagesData?.languages.find(l => l.code === sourceLanguage)?.name ?? sourceLanguage})
                </span>
              )}
            </CardDescription>
          </div>

          {/* Language selector */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="gap-2">
                <Languages className="h-4 w-4" />
                {currentLanguageName}
                <ChevronDown className="h-3 w-3" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-48">
              {languagesData?.languages.map((lang) => (
                <DropdownMenuItem
                  key={lang.code}
                  onClick={() => handleLanguageChange(lang.code)}
                  className={selectedLanguage === lang.code ? 'bg-blue-50' : ''}
                >
                  <Globe className="h-4 w-4 mr-2" />
                  {lang.name}
                  {selectedLanguage === lang.code && (
                    <Badge variant="secondary" className="ml-auto text-xs">
                      Active
                    </Badge>
                  )}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Summary section */}
        {summary && (summary.summary || summary.tldr) && (
          <div className="p-4 rounded-lg bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-100">
            {summary.tldr && (
              <div className="mb-2">
                <span className="text-xs font-semibold text-blue-600 uppercase tracking-wide">
                  TL;DR
                </span>
                <p className="text-sm text-gray-800 mt-1">{summary.tldr}</p>
              </div>
            )}
            {summary.summary && summary.summary !== summary.tldr && (
              <div>
                <span className="text-xs font-semibold text-blue-600 uppercase tracking-wide">
                  Summary
                </span>
                <p className="text-sm text-gray-700 mt-1">{summary.summary}</p>
              </div>
            )}
          </div>
        )}

        {/* Highlights list */}
        <div className="space-y-3 max-h-[400px] overflow-y-auto pr-2">
          {highlights.map((highlight, index) => (
            <HighlightItem
              key={highlight.id}
              highlight={highlight}
              index={index + 1}
              onTimestampClick={onTimestampClick}
            />
          ))}
        </div>

        {/* Translation loading indicator */}
        {translateMutation.isPending && (
          <div className="flex items-center justify-center py-2 text-sm text-blue-600">
            <Loader2 className="h-4 w-4 animate-spin mr-2" />
            Translating highlights...
          </div>
        )}
      </CardContent>
    </Card>
  );
}

interface HighlightItemProps {
  highlight: VideoHighlight;
  index: number;
  onTimestampClick?: (ms: number) => void;
}

function HighlightItem({ highlight, index, onTimestampClick }: HighlightItemProps) {
  const { text, category, importance, timestampMs } = highlight;

  return (
    <div className="group p-3 rounded-lg bg-white border border-gray-100 shadow-[0_1px_2px_rgba(0,0,0,0.03)] hover:border-gray-200 hover:shadow-sm transition-all">
      <div className="flex items-start gap-3">
        {/* Index number */}
        <div className="flex-shrink-0 w-6 h-6 rounded-full bg-gray-100 flex items-center justify-center text-xs font-semibold text-gray-600">
          {index}
        </div>

        <div className="flex-1 min-w-0">
          {/* Category badge and timestamp */}
          <div className="flex flex-wrap items-center gap-2 mb-1.5">
            <Badge
              variant="outline"
              className={`text-xs flex items-center gap-1 ${getCategoryColor(category)}`}
            >
              {getCategoryIcon(category)}
              {formatCategoryLabel(category)}
            </Badge>

            {importance !== undefined && importance >= 0.8 && (
              <Badge variant="secondary" className="text-xs bg-amber-100 text-amber-700">
                <Star className="h-3 w-3 mr-0.5" />
                High Priority
              </Badge>
            )}

            {timestampMs !== undefined && onTimestampClick && (
              <button
                type="button"
                onClick={() => onTimestampClick(timestampMs)}
                className="text-xs text-blue-600 hover:text-blue-800 hover:underline flex items-center gap-1"
              >
                @ {formatDuration(timestampMs)}
              </button>
            )}
          </div>

          {/* Highlight text */}
          <p className="text-sm text-gray-800 leading-relaxed">
            {text}
          </p>
        </div>
      </div>
    </div>
  );
}

export default KeyHighlights;
