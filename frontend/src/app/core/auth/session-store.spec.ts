import { TestBed } from '@angular/core/testing';
import { AuthUser, SessionStore } from './session-store';

const user: AuthUser = {
  userId: 'u1',
  displayName: 'Pat Lee',
  email: 'pat@example.com',
  roles: ['Project Manager']
};

describe('SessionStore', () => {
  let store: SessionStore;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    store = TestBed.inject(SessionStore);
  });

  it('starts signed out', () => {
    expect(store.isAuthenticated()).toBe(false);
    expect(store.user()).toBeNull();
    expect(store.accessToken).toBeNull();
    expect(store.csrfToken).toBeNull();
    expect(store.isAdmin()).toBe(false);
  });

  it('stores the token and user when a session is set', () => {
    store.setSession('access-token', user);

    expect(store.isAuthenticated()).toBe(true);
    expect(store.accessToken).toBe('access-token');
    expect(store.user()).toEqual(user);
    expect(store.hasRole('Project Manager')).toBe(true);
    expect(store.hasRole('Admin')).toBe(false);
    expect(store.isAdmin()).toBe(false);
  });

  it('derives isAdmin from the Admin role', () => {
    store.setSession('t', { ...user, roles: ['Admin'] });
    expect(store.isAdmin()).toBe(true);

    store.setSession('t', { ...user, roles: ['Team Member'] });
    expect(store.isAdmin()).toBe(false);
  });

  it('keeps the CSRF token separately from the session', () => {
    store.setCsrfToken('csrf-1');
    expect(store.csrfToken).toBe('csrf-1');

    store.setSession('t', user);
    store.clearSession();
    expect(store.csrfToken).toBe('csrf-1');
  });

  it('clears the token, user and authentication flag', () => {
    store.setSession('t', { ...user, roles: ['Admin'] });
    store.clearSession();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.accessToken).toBeNull();
    expect(store.user()).toBeNull();
    expect(store.isAdmin()).toBe(false);
  });
});
