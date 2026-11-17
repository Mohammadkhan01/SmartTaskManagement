import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../api/api.config';
import { AuthSessionResponse } from './auth.models';
import { AuthService } from './auth.service';
import { SessionStore } from './session-store';

const AUTH = `${API_BASE_URL}/auth`;
const tick = () => new Promise<void>(resolve => setTimeout(resolve, 0));

const sessionResponse: AuthSessionResponse = {
  userId: 'u1',
  displayName: 'Pat Lee',
  email: 'pat@example.com',
  roles: ['Admin'],
  accessToken: 'access-1',
  expiresAtUtc: '2030-01-01T00:00:00Z'
};

describe('AuthService', () => {
  let service: AuthService;
  let session: SessionStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    session = TestBed.inject(SessionStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function flushCsrf(token = 'csrf-1'): Promise<void> {
    await tick();
    const csrf = http.expectOne(`${AUTH}/csrf`);
    expect(csrf.request.method).toBe('GET');
    expect(csrf.request.withCredentials).toBe(true);
    csrf.flush({ requestToken: token });
    await tick();
  }

  it('login fetches a CSRF token, posts credentials with cookies and stores the session', async () => {
    const result = service.login({ email: 'pat@example.com', password: 'secret' });

    await flushCsrf();
    expect(session.csrfToken).toBe('csrf-1');

    const login = http.expectOne(`${AUTH}/login`);
    expect(login.request.method).toBe('POST');
    expect(login.request.withCredentials).toBe(true);
    expect(login.request.body).toEqual({ email: 'pat@example.com', password: 'secret' });
    login.flush(sessionResponse);
    await result;

    expect(session.isAuthenticated()).toBe(true);
    expect(session.accessToken).toBe('access-1');
    expect(session.user()).toEqual({
      userId: 'u1',
      displayName: 'Pat Lee',
      email: 'pat@example.com',
      roles: ['Admin']
    });
  });

  it('login rejects and keeps the user signed out on 401', async () => {
    const result = service.login({ email: 'pat@example.com', password: 'wrong' });
    const assertion = expect(result).rejects.toMatchObject({ status: 401 });

    await flushCsrf();
    http.expectOne(`${AUTH}/login`).flush(null, { status: 401, statusText: 'Unauthorized' });

    await assertion;
    expect(session.isAuthenticated()).toBe(false);
  });

  it('register fetches a CSRF token and posts the registration with cookies', async () => {
    const request = { displayName: 'Pat', email: 'pat@example.com', password: 'Str0ng!Password' };
    const result = service.register(request);

    await flushCsrf();
    const register = http.expectOne(`${AUTH}/register`);
    expect(register.request.method).toBe('POST');
    expect(register.request.withCredentials).toBe(true);
    expect(register.request.body).toEqual(request);
    register.flush({ ...sessionResponse, roles: undefined });
    await result;

    expect(session.isAuthenticated()).toBe(true);
    expect(session.user()?.roles).toEqual([]);
  });

  it('restoreSession refreshes via cookie and returns true', async () => {
    const result = service.restoreSession();

    await flushCsrf();
    const refresh = http.expectOne(`${AUTH}/refresh`);
    expect(refresh.request.method).toBe('POST');
    expect(refresh.request.withCredentials).toBe(true);
    refresh.flush({ ...sessionResponse, accessToken: 'access-2' });

    await expect(result).resolves.toBe(true);
    expect(session.accessToken).toBe('access-2');
  });

  it('restoreSession clears the session and returns false when refresh fails', async () => {
    session.setSession('stale', { userId: 'u1', displayName: 'P', email: 'p@x', roles: [] });
    const result = service.restoreSession();

    await flushCsrf();
    http.expectOne(`${AUTH}/refresh`).flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(result).resolves.toBe(false);
    expect(session.isAuthenticated()).toBe(false);
    expect(session.accessToken).toBeNull();
  });

  it('restoreSession returns false when the CSRF request itself fails', async () => {
    const result = service.restoreSession();

    await tick();
    http.expectOne(`${AUTH}/csrf`).error(new ProgressEvent('error'), { status: 0 });

    await expect(result).resolves.toBe(false);
    http.expectNone(`${AUTH}/refresh`);
  });

  it('refreshSession shares one in-flight refresh between concurrent callers', async () => {
    const first = service.refreshSession();
    const second = service.refreshSession();
    expect(second).toBe(first);

    await flushCsrf();
    http.expectOne(`${AUTH}/refresh`).flush(sessionResponse);

    await expect(Promise.all([first, second])).resolves.toEqual([true, true]);

    // A later refresh starts a new request once the previous one has settled.
    const third = service.refreshSession();
    expect(third).not.toBe(first);
    await flushCsrf();
    http.expectOne(`${AUTH}/refresh`).flush(sessionResponse);
    await expect(third).resolves.toBe(true);
  });

  it('logout posts with CSRF + cookies and clears the session', async () => {
    session.setSession('t', { userId: 'u1', displayName: 'P', email: 'p@x', roles: [] });
    const result = service.logout();

    await flushCsrf();
    const logout = http.expectOne(`${AUTH}/logout`);
    expect(logout.request.method).toBe('POST');
    expect(logout.request.withCredentials).toBe(true);
    logout.flush(null);
    await result;

    expect(session.isAuthenticated()).toBe(false);
  });

  it('logout still clears the local session when the server call fails', async () => {
    session.setSession('t', { userId: 'u1', displayName: 'P', email: 'p@x', roles: [] });
    const result = service.logout();
    const assertion = expect(result).rejects.toBeTruthy();

    await flushCsrf();
    http.expectOne(`${AUTH}/logout`).flush(null, { status: 500, statusText: 'Server Error' });

    await assertion;
    expect(session.isAuthenticated()).toBe(false);
  });
});
