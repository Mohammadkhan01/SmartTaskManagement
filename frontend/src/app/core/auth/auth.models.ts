export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
}

export interface AuthSessionResponse {
  userId: string;
  displayName: string;
  email: string;
  roles: string[];
  accessToken: string;
  expiresAtUtc: string;
}

export interface CsrfResponse {
  requestToken: string;
}

export interface CurrentUserResponse {
  userId: string;
  email: string;
  roles: string[];
}
