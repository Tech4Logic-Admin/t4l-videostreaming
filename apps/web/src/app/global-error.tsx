'use client';

import { useEffect } from 'react';
import { AlertOctagon, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error('Critical application error:', error);
  }, [error]);

  return (
    <html>
      <body>
        <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
          <div className="text-center">
            <div className="mx-auto mb-6 flex h-20 w-20 items-center justify-center rounded-full bg-red-100">
              <AlertOctagon className="h-10 w-10 text-red-600" />
            </div>
            <h1 className="text-3xl font-bold text-slate-900 mb-2">
              Critical Error
            </h1>
            <p className="text-slate-600 mb-6 max-w-md">
              A critical error has occurred and the application could not recover.
              Please try refreshing the page.
            </p>
            {error.digest && (
              <p className="text-sm text-slate-500 mb-4">
                Reference: {error.digest}
              </p>
            )}
            <Button
              onClick={() => reset()}
              className="bg-blue-600 hover:bg-blue-700 text-white"
            >
              <RefreshCw className="mr-2 h-4 w-4" />
              Refresh Page
            </Button>
          </div>
        </div>
      </body>
    </html>
  );
}
