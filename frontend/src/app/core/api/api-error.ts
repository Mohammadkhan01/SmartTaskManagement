import { HttpErrorResponse } from '@angular/common/http';

/** Extracts a readable message from an ASP.NET Core ProblemDetails / ValidationProblemDetails body. */
export function validationMessage(error: HttpErrorResponse): string | null {
  const body = error.error;
  if (!body || typeof body !== 'object') return null;

  const errors = body.errors as Record<string, string[]> | undefined;
  if (errors) {
    const messages = Object.values(errors).flat().filter(Boolean);
    if (messages.length > 0) return messages.join(' ');
  }

  return typeof body.detail === 'string' ? body.detail : null;
}

export interface ErrorMessages {
  badRequest?: string;
  forbidden?: string;
  notFound?: string;
  conflict?: string;
  fallback: string;
}

/** Maps common API failures to user-facing text, matching the projects pages' wording. */
export function describeHttpError(error: unknown, messages: ErrorMessages): string {
  if (error instanceof HttpErrorResponse) {
    switch (error.status) {
      case 0:
        return 'Cannot reach the server. Check your connection and try again.';
      case 400:
        return validationMessage(error) ?? messages.badRequest ?? messages.fallback;
      case 403:
        return messages.forbidden ?? 'You do not have permission to perform this action.';
      case 404:
        return messages.notFound ?? messages.fallback;
      case 409:
        return messages.conflict ?? 'This item was changed by someone else. Reload it and try again.';
    }
  }
  return messages.fallback;
}
