import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { SessionStore } from '../auth/session-store';

export const authGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const auth = inject(AuthService);
  const router = inject(Router);

  if (session.isAuthenticated()) {
    return true;
  }

  const restored = await auth.restoreSession();
  return restored ? true : router.createUrlTree(['/login']);
};
