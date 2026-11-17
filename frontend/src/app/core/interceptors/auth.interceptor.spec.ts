import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { API_BASE_URL } from '../api/api.config';
import { AuthSessionResponse } from '../auth/auth.models';
import { SessionStore } from '../auth/session-store';
import { AppNoticeStore } from '../services/app-notice.store';
import { authInterceptor } from './auth.interceptor';

const AUTH = `${API_BASE_URL}/auth`;
const PROJECTS = `${API_BASE_URL}/projects`;
const tick = () => new Promise<void>(resolve => setTimeout(resolve, 0));
const unauthorized = { status: 401, statusText: 'Unauthorized' };

const refreshed: AuthSessionResponse = {
  userId: 'u1',
  displayName: 'Pat',
  email: 'pat@example.com',
  roles: ['Team Member'],
  accessToken: 'fresh-token',
  expiresAtUtc: '2030-01-01T00:00:00Z'
};

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let session: SessionStore;
  let notice: AppNoticeStore;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
    notice = TestBed.inject(AppNoticeStore);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    session.setSession('old-token', {
      userId: 'u1',
      displayName: 'Pat',
      email: 'pat@example.com',
      roles: ['Team Member']
    });
    session.setCsrfToken('csrf-1');
  });

  afterEach(() => http.verify());

  /** Completes the AuthService refresh (CSRF fetch + refresh POST) triggered by a 401. */
  async function completeRefresh(succeed: boolean): Promise<void> {
    await tick();
    http.expectOne(`${AUTH}/csrf`).flush({ requestToken: 'csrf-2' });
    await tick();
    const refresh = http.expectOne(`${AUTH}/refresh`);
    if (succeed) {
      refresh.flush(refreshed);
    } else {
      refresh.flush(null, unauthorized);
    }
    await tick();
  }

  it('adds the bearer token and credentials to API GET requests without a CSRF header', () => {
    client.get(PROJECTS).subscribe();

    const req = http.expectOne(PROJECTS);
    expect(req.request.headers.get('Authorization')).toBe('Bearer old-token');
    expect(req.request.headers.has('X-CSRF-TOKEN')).toBe(false);
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('adds the CSRF header to unsafe methods', () => {
    client.post(PROJECTS, {}).subscribe();
    client.put(`${PROJECTS}/p1`, {}).subscribe();
    client.delete(`${PROJECTS}/p1`).subscribe();

    for (const req of http.match(r => r.url.startsWith(PROJECTS))) {
      expect(req.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-1');
      expect(req.request.headers.get('Authorization')).toBe('Bearer old-token');
      req.flush({});
    }
  });

  it('omits the Authorization header when there is no access token', () => {
    session.clearSession();
    client.get(PROJECTS).subscribe();

    const req = http.expectOne(PROJECTS);
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('leaves non-API requests untouched', () => {
    client.get('https://cdn.example.com/asset.json').subscribe();

    const req = http.expectOne('https://cdn.example.com/asset.json');
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.withCredentials).toBe(false);
    req.flush({});
  });

  it('does not try to refresh when an auth endpoint returns 401', async () => {
    let failure: HttpErrorResponse | undefined;
    client.post(`${AUTH}/login`, {}).subscribe({ error: e => (failure = e) });

    http.expectOne(`${AUTH}/login`).flush(null, unauthorized);
    await tick();

    expect(failure?.status).toBe(401);
    http.expectNone(`${AUTH}/refresh`);
    expect(navigate).not.toHaveBeenCalled();
    expect(session.isAuthenticated()).toBe(true);
  });

  it('refreshes once on 401 and retries the request with the new token', async () => {
    let body: unknown;
    client.get(PROJECTS).subscribe(value => (body = value));

    http.expectOne(PROJECTS).flush(null, unauthorized);
    await completeRefresh(true);

    const retry = http.expectOne(PROJECTS);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    retry.flush({ items: [] });

    expect(body).toEqual({ items: [] });
    expect(navigate).not.toHaveBeenCalled();
  });

  it('uses the refreshed CSRF token when retrying unsafe requests', async () => {
    client.post(PROJECTS, { name: 'X' }).subscribe();

    http.expectOne(PROJECTS).flush(null, unauthorized);
    await completeRefresh(true);

    const retry = http.expectOne(PROJECTS);
    expect(retry.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-2');
    expect(retry.request.body).toEqual({ name: 'X' });
    retry.flush({});
  });

  it('clears the session and redirects to /login when the refresh fails', async () => {
    let failure: HttpErrorResponse | undefined;
    client.get(PROJECTS).subscribe({ error: e => (failure = e) });

    http.expectOne(PROJECTS).flush(null, unauthorized);
    await completeRefresh(false);

    expect(failure?.status).toBe(401);
    expect(session.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
    http.expectNone(PROJECTS);
  });

  it('clears the session and redirects when the retried request is still 401', async () => {
    let failure: HttpErrorResponse | undefined;
    client.get(PROJECTS).subscribe({ error: e => (failure = e) });

    http.expectOne(PROJECTS).flush(null, unauthorized);
    await completeRefresh(true);
    http.expectOne(PROJECTS).flush(null, unauthorized);

    expect(failure?.status).toBe(401);
    expect(session.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
    http.expectNone(`${AUTH}/refresh`);
  });

  it('shares a single refresh between concurrent 401 responses', async () => {
    const results: unknown[] = [];
    client.get(`${PROJECTS}/a`).subscribe(value => results.push(value));
    client.get(`${PROJECTS}/b`).subscribe(value => results.push(value));

    http.expectOne(`${PROJECTS}/a`).flush(null, unauthorized);
    http.expectOne(`${PROJECTS}/b`).flush(null, unauthorized);

    // expectOne fails if a second CSRF/refresh request was issued.
    await completeRefresh(true);

    const retries = http.match(r => r.url.startsWith(`${PROJECTS}/`));
    expect(retries.length).toBe(2);
    for (const retry of retries) {
      expect(retry.request.headers.get('Authorization')).toBe('Bearer fresh-token');
      retry.flush({ url: retry.request.url });
    }
    expect(results).toHaveLength(2);
  });

  it('does not refresh for non-401 errors', async () => {
    let failure: HttpErrorResponse | undefined;
    client.get(PROJECTS).subscribe({ error: e => (failure = e) });

    http.expectOne(PROJECTS).flush(null, { status: 403, statusText: 'Forbidden' });
    await tick();

    expect(failure?.status).toBe(403);
    http.expectNone(`${AUTH}/csrf`);
    expect(notice.message()).toBeNull();
  });

  it('shows the app notice on network errors and clears it after the next success', () => {
    let failure: HttpErrorResponse | undefined;
    client.get(PROJECTS).subscribe({ error: e => (failure = e) });

    http.expectOne(PROJECTS).error(new ProgressEvent('error'), { status: 0 });

    expect(failure?.status).toBe(0);
    expect(notice.message()).toContain('Cannot reach the Smart Task server');

    client.get(PROJECTS).subscribe();
    http.expectOne(PROJECTS).flush({});
    expect(notice.message()).toBeNull();
  });
});
