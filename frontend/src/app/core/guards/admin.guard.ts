import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../auth/session-store';

/**
 * UI-only gate; the API enforces the Admin role independently.
 * Must run after authGuard so the session has been restored.
 */
export const adminGuard: CanActivateFn = () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  return session.isAdmin() ? true : router.createUrlTree(['/dashboard']);
};
