import { API_ORIGIN } from '../api/api.config';
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { SessionStore } from '../auth/session-store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionStore);
  const isApiRequest = request.url.startsWith(`${API_ORIGIN}/`);

  if (!isApiRequest) {
    return next(request);
  }

  let headers = request.headers;
  const token = session.accessToken;
  const csrfToken = session.csrfToken;

  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  if (csrfToken && !['GET', 'HEAD', 'OPTIONS'].includes(request.method)) {
    headers = headers.set('X-CSRF-TOKEN', csrfToken);
  }

  return next(request.clone({
    headers,
    withCredentials: true
  }));
};
