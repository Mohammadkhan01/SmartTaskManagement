import { API_BASE_URL } from '../api/api.config';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SessionStore, AuthUser } from './session-store';
import {
  AuthSessionResponse,
  CsrfResponse,
  CurrentUserResponse,
  LoginRequest,
  RegisterRequest
} from './auth.models';

const API = `${API_BASE_URL}/auth`;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);

  async login(request: LoginRequest): Promise<void> {
    await this.loadCsrfToken();
    const response = await firstValueFrom(
      this.http.post<AuthSessionResponse>(`${API}/login`, request, {
        withCredentials: true
      })
    );
    this.applySession(response);
  }

  async register(request: RegisterRequest): Promise<void> {
    await this.loadCsrfToken();
    const response = await firstValueFrom(
      this.http.post<AuthSessionResponse>(`${API}/register`, request, {
        withCredentials: true
      })
    );
    this.applySession(response);
  }

  async restoreSession(): Promise<boolean> {
    try {
      await this.loadCsrfToken();
      const response = await firstValueFrom(
        this.http.post<AuthSessionResponse>(`${API}/refresh`, {}, {
          withCredentials: true
        })
      );
      this.applySession(response);
      return true;
    } catch {
      this.session.clearSession();
      return false;
    }
  }

  async logout(): Promise<void> {
    try {
      await this.loadCsrfToken();
      await firstValueFrom(
        this.http.post<void>(`${API}/logout`, {}, {
          withCredentials: true
        })
      );
    } finally {
      this.session.clearSession();
    }
  }

  private async loadCsrfToken(): Promise<void> {
    const response = await firstValueFrom(
      this.http.get<CsrfResponse>(`${API}/csrf`, {
        withCredentials: true
      })
    );
    this.session.setCsrfToken(response.requestToken);
  }

  private applySession(response: AuthSessionResponse): void {
    const user: AuthUser = {
      userId: response.userId,
      displayName: response.displayName,
      email: response.email,
      roles: response.roles ?? []
    };
    this.session.setSession(response.accessToken, user);
  }
}
