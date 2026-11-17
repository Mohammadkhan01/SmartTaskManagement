import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { SessionStore } from '../core/auth/session-store';
import { AppNoticeStore } from '../core/services/app-notice.store';

@Component({
  selector: 'app-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="shell">
      <header class="topbar">
        <a class="brand" routerLink="/dashboard">SMART TASK MANAGEMENT</a>
        <nav aria-label="Main navigation">
          <a routerLink="/dashboard" routerLinkActive="active" ariaCurrentWhenActive="page">Dashboard</a>
          <a routerLink="/projects" routerLinkActive="active" ariaCurrentWhenActive="page">Projects</a>
          @if (session.isAdmin()) {
            <a routerLink="/admin/users" routerLinkActive="active" ariaCurrentWhenActive="page">Admin</a>
          }
        </nav>
        <div class="account">
          <span>{{ session.user()?.displayName }}</span>
          <button type="button" class="secondary" [disabled]="signingOut()" (click)="logout()">
            {{ signingOut() ? 'Signing out…' : 'Sign out' }}
          </button>
        </div>
      </header>

      @if (notice.message(); as message) {
        <div class="notice" role="alert">
          <span>{{ message }}</span>
          <button type="button" class="link-button" (click)="notice.clear()">Dismiss</button>
        </div>
      }

      <router-outlet />
    </div>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:#f4f6fa; }
    .topbar { display:flex; align-items:center; gap:32px; min-height:54px; padding:0 28px; background:white; border-bottom:1px solid #e5eaf1; }
    .brand { color:#17243b; font-size:12px; font-weight:800; letter-spacing:1px; text-decoration:none; }
    nav { display:flex; gap:22px; flex:1; }
    nav a { color:#64748b; font-size:14px; text-decoration:none; padding:17px 0; border-bottom:2px solid transparent; }
    nav a.active { color:#1d4ed8; border-bottom-color:#1d4ed8; }
    .account { display:flex; align-items:center; gap:14px; color:#64748b; font-size:13px; }
    button { border:0; border-radius:8px; padding:9px 15px; cursor:pointer; }
    button:disabled { opacity:.5; cursor:not-allowed; }
    .secondary { background:#eef2f7; color:#334155; }
    .notice { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:12px 28px; background:#fef3c7; color:#92400e; font-size:14px; }
    .link-button { padding:0; background:transparent; color:#92400e; font-weight:600; }
    @media(max-width:720px) {
      .topbar { flex-wrap:wrap; gap:12px; padding:10px 16px; }
      nav { order:3; width:100%; }
      nav a { padding:8px 0; }
      .account { margin-left:auto; }
      .notice { padding:12px 16px; }
    }
  `]
})
export class AppShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);
  readonly notice = inject(AppNoticeStore);

  readonly signingOut = signal(false);

  async logout(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.auth.logout();
    } finally {
      this.signingOut.set(false);
      await this.router.navigateByUrl('/login');
    }
  }
}
