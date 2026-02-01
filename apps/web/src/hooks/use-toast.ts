'use client';

import { useCallback } from 'react';
import { toast as sonnerToast } from 'sonner';
import { ApiClientError } from '@/lib/api-client';

interface ToastOptions {
  title?: string;
  description?: string;
  duration?: number;
  action?: {
    label: string;
    onClick: () => void;
  };
}

/**
 * Custom toast hook with API error handling support
 */
export function useToast() {
  const success = useCallback((options: ToastOptions | string) => {
    const opts = typeof options === 'string' ? { title: options } : options;
    sonnerToast.success(opts.title, {
      description: opts.description,
      duration: opts.duration || 4000,
      action: opts.action ? {
        label: opts.action.label,
        onClick: opts.action.onClick,
      } : undefined,
    });
  }, []);

  const error = useCallback((options: ToastOptions | string) => {
    const opts = typeof options === 'string' ? { title: options } : options;
    sonnerToast.error(opts.title, {
      description: opts.description,
      duration: opts.duration || 5000,
      action: opts.action ? {
        label: opts.action.label,
        onClick: opts.action.onClick,
      } : undefined,
    });
  }, []);

  const warning = useCallback((options: ToastOptions | string) => {
    const opts = typeof options === 'string' ? { title: options } : options;
    sonnerToast.warning(opts.title, {
      description: opts.description,
      duration: opts.duration || 4500,
      action: opts.action ? {
        label: opts.action.label,
        onClick: opts.action.onClick,
      } : undefined,
    });
  }, []);

  const info = useCallback((options: ToastOptions | string) => {
    const opts = typeof options === 'string' ? { title: options } : options;
    sonnerToast.info(opts.title, {
      description: opts.description,
      duration: opts.duration || 4000,
      action: opts.action ? {
        label: opts.action.label,
        onClick: opts.action.onClick,
      } : undefined,
    });
  }, []);

  const loading = useCallback((message: string) => {
    return sonnerToast.loading(message);
  }, []);

  const dismiss = useCallback((toastId?: string | number) => {
    sonnerToast.dismiss(toastId);
  }, []);

  const promise = useCallback(<T>(
    promiseFn: Promise<T>,
    messages: {
      loading: string;
      success: string | ((_data: T) => string);
      error?: string | ((_error: unknown) => string);
    }
  ) => {
    return sonnerToast.promise(promiseFn, {
      loading: messages.loading,
      success: messages.success,
      error: (err) => {
        if (messages.error) {
          return typeof messages.error === 'function'
            ? messages.error(err)
            : messages.error;
        }
        return getErrorMessage(err);
      },
    });
  }, []);

  /**
   * Handle API errors with appropriate toast messages
   */
  const apiError = useCallback((err: unknown, fallbackMessage = 'An error occurred') => {
    if (err instanceof ApiClientError) {
      let title = 'Error';
      let description = err.detail || err.message;

      if (err.isNotFound) {
        title = 'Not Found';
        description = err.detail || 'The requested resource was not found';
      } else if (err.isUnauthorized) {
        title = 'Unauthorized';
        description = 'Please sign in to continue';
      } else if (err.isForbidden) {
        title = 'Access Denied';
        description = 'You do not have permission to perform this action';
      } else if (err.isValidationError) {
        title = 'Validation Error';
        description = err.detail || 'Please check your input and try again';
      } else if (err.isServerError) {
        title = 'Server Error';
        description = 'Something went wrong. Please try again later.';
      }

      error({
        title,
        description: err.traceId
          ? `${description}\n\nTrace ID: ${err.traceId}`
          : description,
      });
    } else {
      error({
        title: 'Error',
        description: getErrorMessage(err) || fallbackMessage,
      });
    }
  }, [error]);

  return {
    success,
    error,
    warning,
    info,
    loading,
    dismiss,
    promise,
    apiError,
  };
}

/**
 * Extract error message from unknown error type
 */
function getErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  if (typeof error === 'string') {
    return error;
  }
  if (error && typeof error === 'object' && 'message' in error) {
    return String((error as { message: unknown }).message);
  }
  return 'An unexpected error occurred';
}

// Re-export for direct usage without hook
export const toast = {
  success: (message: string, description?: string) =>
    sonnerToast.success(message, { description }),
  error: (message: string, description?: string) =>
    sonnerToast.error(message, { description }),
  warning: (message: string, description?: string) =>
    sonnerToast.warning(message, { description }),
  info: (message: string, description?: string) =>
    sonnerToast.info(message, { description }),
  loading: (message: string) =>
    sonnerToast.loading(message),
  dismiss: (id?: string | number) =>
    sonnerToast.dismiss(id),
};
