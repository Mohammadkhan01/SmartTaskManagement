import { Injectable, computed, signal } from '@angular/core';

export interface AuthUser {
  userId: string;
  displayName: string;
  email: string;
  roles: string[];
}

@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly tokenState = signal<string | null>(null);
  private readonly userState = signal<AuthUser | null>(null);
  private readonly csrfState = signal<string | null>(null);

  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = signal(false);
  readonly isAdmin = computed(() => this.hasRole('Admin'));

  hasRole(role: string): boolean {
    return this.userState()?.roles.includes(role) ?? false;
  }

  get accessToken(): string | null {
    return this.tokenState();
  }

  get csrfToken(): string | null {
    return this.csrfState();
  }

  setCsrfToken(token: string): void {
    this.csrfState.set(token);
  }

  setSession(token: string, user: AuthUser): void {
    this.tokenState.set(token);
    this.userState.set(user);
    this.isAuthenticated.set(true);
  }

  clearSession(): void {
    this.tokenState.set(null);
    this.userState.set(null);
    this.isAuthenticated.set(false);
  }
}
