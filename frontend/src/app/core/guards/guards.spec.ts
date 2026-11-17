import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree
} from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { SessionStore } from '../auth/session-store';
import { adminGuard } from './admin.guard';
import { authGuard } from './auth.guard';

const route = {} as ActivatedRouteSnapshot;
const state = {} as RouterStateSnapshot;

function signIn(roles: string[]): void {
  TestBed.inject(SessionStore).setSession('token', {
    userId: 'u1',
    displayName: 'Pat',
    email: 'pat@example.com',
    roles
  });
}

describe('route guards', () => {
  let restoreSession: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    restoreSession = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { restoreSession } }
      ]
    });
  });

  const runAuthGuard = () => TestBed.runInInjectionContext(() => authGuard(route, state));
  const runAdminGuard = () => TestBed.runInInjectionContext(() => adminGuard(route, state));
  const serialize = (tree: unknown) => TestBed.inject(Router).serializeUrl(tree as UrlTree);

  describe('authGuard', () => {
    it('allows navigation when already authenticated without refreshing', async () => {
      signIn(['Team Member']);

      await expect(runAuthGuard()).resolves.toBe(true);
      expect(restoreSession).not.toHaveBeenCalled();
    });

    it('allows navigation when the session can be restored from the refresh cookie', async () => {
      restoreSession.mockResolvedValue(true);

      await expect(runAuthGuard()).resolves.toBe(true);
      expect(restoreSession).toHaveBeenCalledTimes(1);
    });

    it('redirects to /login when the session cannot be restored', async () => {
      restoreSession.mockResolvedValue(false);

      const result = await runAuthGuard();

      expect(result).toBeInstanceOf(UrlTree);
      expect(serialize(result)).toBe('/login');
    });
  });

  describe('adminGuard', () => {
    it('allows administrators', () => {
      signIn(['Admin']);
      expect(runAdminGuard()).toBe(true);
    });

    it('redirects non-admins to /dashboard', () => {
      signIn(['Project Manager']);
      const result = runAdminGuard();

      expect(result).toBeInstanceOf(UrlTree);
      expect(serialize(result)).toBe('/dashboard');
    });

    it('redirects signed-out users to /dashboard', () => {
      expect(serialize(runAdminGuard())).toBe('/dashboard');
    });
  });
});
