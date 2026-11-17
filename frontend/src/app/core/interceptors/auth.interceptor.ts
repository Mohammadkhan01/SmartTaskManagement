import { API_ORIGIN, API_BASE_URL } from '../api/api.config';
import {
  HttpErrorResponse,
  HttpEvent,
  HttpInterceptorFn,
  HttpRequest,
  HttpResponse
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, tap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { SessionStore } from '../auth/session-store';
import { AppNoticeStore } from '../services/app-notice.store';

const AUTH_API = `${API_BASE_URL}/auth/`;
const NETWORK_ERROR_MESSAGE =
  'Cannot reach the Smart Task server. Check your connection and try again.';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionStore);
  const auth = inject(AuthService);
  const router = inject(Router);
  const notice = inject(AppNoticeStore);
  const isApiRequest = request.url.startsWith(`${API_ORIGIN}/`);

  if (!isApiRequest) {
    return next(request);
  }

  const isAuthRequest = request.url.startsWith(AUTH_API);

  const authorize = (original: HttpRequest<unknown>): HttpRequest<unknown> => {
    let headers = original.headers;
    const token = session.accessToken;
    const csrfToken = session.csrfToken;

    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
    }

    if (csrfToken && !['GET', 'HEAD', 'OPTIONS'].includes(original.method)) {
      headers = headers.set('X-CSRF-TOKEN', csrfToken);
    }

    return original.clone({ headers, withCredentials: true });
  };

  const clearNoticeOnSuccess = tap<HttpEvent<unknown>>({
    next: event => {
      if (event instanceof HttpResponse) notice.clear();
    }
  });

  const reportNetworkError = (error: unknown) => {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      notice.show(NETWORK_ERROR_MESSAGE);
    }
    return throwError(() => error);
  };

  return next(authorize(request)).pipe(
    clearNoticeOnSuccess,
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthRequest) {
        return reportNetworkError(error);
      }

      return from(auth.refreshSession()).pipe(
        switchMap(refreshed => {
          if (!refreshed) {
            session.clearSession();
            void router.navigate(['/login']);
            return throwError(() => error);
          }

          return next(authorize(request)).pipe(
            clearNoticeOnSuccess,
            catchError(retryError => {
              if (retryError instanceof HttpErrorResponse && retryError.status === 401) {
                session.clearSession();
                void router.navigate(['/login']);
              }
              return reportNetworkError(retryError);
            })
          );
        })
      );
    })
  );
};
