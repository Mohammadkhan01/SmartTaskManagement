import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  linkedSignal,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { SessionStore } from '../core/auth/session-store';
import { RecentProjectsStore } from '../core/projects/recent-projects.store';
import { AppNoticeStore } from '../core/services/app-notice.store';
import { AvatarComponent } from '../shared/ui/avatar.component';
import { IconComponent } from '../shared/ui/icon.component';
import { buildBreadcrumbs } from './breadcrumbs';

const COLLAPSED_KEY = 'smarttask.sidebar.collapsed';
const FOCUSABLE = 'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])';

@Component({
  selector: 'app-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, IconComponent, AvatarComponent],
  host: {
    '(document:keydown.escape)': 'onEscape()',
    '(document:click)': 'onDocumentClick($event)'
  },
  template: `
    <a class="skip-link" href="#content" (click)="skipToContent($event)">Skip to content</a>

    <div class="shell" [class.collapsed]="collapsed()">
      <div class="sidebar-rail">
      <aside #sidebar id="app-sidebar" class="sidebar" [class.open]="navOpen()" aria-label="Sidebar"
        (keydown)="trapFocus($event)">
        <div class="sidebar-header">
          <a class="brand" routerLink="/dashboard" aria-label="Smart Task Management dashboard">
            <span class="brand-mark"><app-icon name="check" [size]="18" [strokeWidth]="2.75" /></span>
            <span class="brand-text"><strong>Smart Task</strong><small>Management</small></span>
          </a>
          <button #drawerClose type="button" class="btn btn-ghost btn-icon drawer-close"
            aria-label="Close navigation" (click)="closeNav(true)">
            <app-icon name="x" />
          </button>
        </div>

        <nav class="sidebar-nav" aria-label="Main navigation">
          <p class="nav-group" id="nav-workspace">Workspace</p>
          <ul role="list" aria-labelledby="nav-workspace">
            <li>
              <a class="nav-link" routerLink="/dashboard" routerLinkActive="active" ariaCurrentWhenActive="page"
                [attr.title]="collapsed() ? 'Dashboard' : null">
                <app-icon name="dashboard" /><span class="nav-label">Dashboard</span>
              </a>
            </li>
            <li>
              <div class="nav-row">
                <a class="nav-link" routerLink="/projects" [class.active]="inProjects()"
                  [attr.aria-current]="onProjectList() ? 'page' : null"
                  [attr.title]="collapsed() ? 'Projects' : null">
                  <app-icon name="folder" /><span class="nav-label">Projects</span>
                </a>
                <button type="button" class="nav-toggle" aria-label="Project shortcuts"
                  aria-controls="projects-submenu" [attr.aria-expanded]="projectsOpen()"
                  (click)="projectsOpen.set(!projectsOpen())">
                  <app-icon name="chevronDown" [size]="16" />
                </button>
              </div>
              <ul id="projects-submenu" class="submenu" role="list" [hidden]="!projectsOpen()">
                <li>
                  <a class="submenu-link" routerLink="/projects" routerLinkActive="active"
                    [routerLinkActiveOptions]="{ exact: true }" ariaCurrentWhenActive="page">All projects</a>
                </li>
                @if (recentProjects().length > 0) {
                  <li>
                    <span class="submenu-heading" id="recent-projects-heading">Recently viewed</span>
                    <ul role="list" aria-labelledby="recent-projects-heading">
                      @for (project of recentProjects(); track project.id) {
                        <li>
                          <a class="submenu-link" [routerLink]="['/projects', project.id]" routerLinkActive="active">
                            <app-avatar size="sm" [square]="true" [name]="project.name" />
                            <span class="nav-label">{{ project.name }}</span>
                          </a>
                        </li>
                      }
                    </ul>
                  </li>
                }
              </ul>
            </li>
          </ul>

          @if (session.isAdmin()) {
            <p class="nav-group" id="nav-admin">Administration</p>
            <ul role="list" aria-labelledby="nav-admin">
              <li>
                <a class="nav-link" routerLink="/admin/users" routerLinkActive="active" ariaCurrentWhenActive="page"
                  [attr.title]="collapsed() ? 'Users' : null">
                  <app-icon name="users" /><span class="nav-label">Users</span>
                </a>
              </li>
            </ul>
          }
        </nav>

        <div class="sidebar-footer">
          <button type="button" class="nav-link collapse-toggle" [attr.aria-pressed]="collapsed()"
            [attr.title]="collapsed() ? 'Expand sidebar' : null" (click)="toggleCollapsed()">
            <app-icon name="panelLeft" /><span class="nav-label">Collapse sidebar</span>
          </button>
        </div>
      </aside>
      </div>

      @if (navOpen()) {
        <div class="backdrop" aria-hidden="true" (click)="closeNav(true)"></div>
      }

      <div class="workspace">
        <header class="topbar">
          <button #navTrigger type="button" class="btn btn-ghost btn-icon nav-trigger" aria-label="Open navigation"
            aria-controls="app-sidebar" [attr.aria-expanded]="navOpen()" (click)="openNav()">
            <app-icon name="menu" />
          </button>

          <nav class="breadcrumbs" aria-label="Breadcrumb">
            <ol>
              @for (crumb of breadcrumbs(); track $index; let last = $last) {
                <li>
                  @if (crumb.link && !last) {
                    <a [routerLink]="crumb.link">{{ crumb.label }}</a>
                    <app-icon class="crumb-sep" name="chevronRight" [size]="14" />
                  } @else {
                    <span [attr.aria-current]="last ? 'page' : null">{{ crumb.label }}</span>
                    @if (!last) { <app-icon class="crumb-sep" name="chevronRight" [size]="14" /> }
                  }
                </li>
              }
            </ol>
          </nav>

          <div #account class="account">
            <button #accountTrigger type="button" class="account-trigger" [attr.aria-expanded]="userMenuOpen()"
              [attr.aria-controls]="userMenuOpen() ? 'account-menu' : null" (click)="toggleUserMenu()">
              <app-avatar [name]="session.user()?.displayName" />
              <span class="account-text">
                <span class="account-name">{{ session.user()?.displayName }}</span>
                <span class="account-role">{{ role() }}</span>
              </span>
              <app-icon class="account-caret" name="chevronDown" [size]="16" />
            </button>

            @if (userMenuOpen()) {
              <div id="account-menu" class="account-menu">
                <div class="account-card">
                  <app-avatar size="lg" [name]="session.user()?.displayName" />
                  <div>
                    <p class="account-card-name">{{ session.user()?.displayName }}</p>
                    <p class="account-card-email">{{ session.user()?.email }}</p>
                    <span class="pill tone-primary">{{ role() }}</span>
                  </div>
                </div>
                <button #signOutButton type="button" class="menu-item" [disabled]="signingOut()" (click)="logout()">
                  <app-icon name="logOut" />
                  {{ signingOut() ? 'Signing out…' : 'Sign out' }}
                </button>
              </div>
            }
          </div>
        </header>

        @if (notice.message(); as message) {
          <div class="notice" role="alert">
            <app-icon name="alert" />
            <span>{{ message }}</span>
            <button type="button" class="btn btn-ghost btn-sm" (click)="notice.clear()">Dismiss</button>
          </div>
        }

        <div id="content" class="content" tabindex="-1">
          <router-outlet />
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display:block; min-height:100vh; }
    .skip-link { position:fixed; top:-60px; left:12px; z-index:100; padding:10px 14px; border-radius:8px; background:var(--c-surface); box-shadow:var(--shadow-pop); font-weight:600; }
    .skip-link:focus { top:12px; }
    .shell { display:grid; grid-template-columns:var(--sidebar-w) minmax(0,1fr); min-height:100vh; }
    .sidebar-rail { background:var(--c-ink); }
    .sidebar { position:sticky; top:0; display:flex; flex-direction:column; height:100vh; overflow:hidden auto; background:var(--c-ink); color:#c3c8dc; }
    .sidebar :focus-visible { outline-color:var(--c-primary-on-ink); }
    .sidebar-header { display:flex; align-items:center; justify-content:space-between; height:var(--topbar-h); padding:0 18px; flex-shrink:0; }
    .brand { display:flex; align-items:center; gap:10px; min-width:0; color:#fff; text-decoration:none; border-radius:8px; }
    .brand-mark { display:grid; place-items:center; flex-shrink:0; width:32px; height:32px; border-radius:9px; background:var(--c-primary); color:#fff; box-shadow:inset 0 0 0 1px rgb(255 255 255 / .18); }
    .brand-text { display:grid; line-height:1.15; white-space:nowrap; }
    .brand-text strong { font:700 15px/1.2 var(--font-display); }
    .brand-text small { color:#8d93ad; font-size:12px; }
    .sidebar-nav { flex:1; padding:4px 12px 16px; }
    .nav-group { margin:18px 10px 6px; color:#7f86a3; font-size:12px; font-weight:600; white-space:nowrap; }
    ul { display:grid; gap:2px; margin:0; padding:0; list-style:none; }
    .nav-row { display:flex; align-items:center; gap:2px; }
    .nav-link { display:flex; flex:1; align-items:center; gap:12px; min-width:0; height:38px; padding:0 10px; border:0; border-radius:8px; background:transparent; color:#c3c8dc; font-weight:500; text-decoration:none; cursor:pointer; }
    .nav-link app-icon { color:#8f95b1; }
    .nav-link:hover, .nav-toggle:hover { background:rgb(255 255 255 / .06); color:#fff; }
    .nav-link.active { background:rgb(255 255 255 / .1); color:#fff; }
    .nav-link.active app-icon { color:var(--c-primary-on-ink); }
    .nav-label { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .nav-toggle { display:grid; place-items:center; width:30px; height:30px; border:0; border-radius:6px; background:transparent; color:#8f95b1; cursor:pointer; }
    .nav-toggle app-icon { transition:transform .15s ease; }
    .nav-toggle[aria-expanded='true'] app-icon { transform:rotate(180deg); }
    .submenu { margin:2px 0 6px 21px; padding-left:10px; border-left:1px solid rgb(255 255 255 / .1); }
    .submenu-link { display:flex; align-items:center; gap:8px; min-height:32px; padding:4px 10px; border-radius:6px; color:#aab0c7; font-size:13px; text-decoration:none; }
    .submenu-link:hover { background:rgb(255 255 255 / .05); color:#fff; }
    .submenu-link.active { background:rgb(255 255 255 / .08); color:#fff; }
    .submenu-heading { display:block; padding:10px 10px 4px; color:#7f86a3; font-size:12px; }
    .sidebar-footer { padding:12px; border-top:1px solid rgb(255 255 255 / .08); }
    .collapse-toggle { width:100%; }
    .collapse-toggle[aria-pressed='true'] app-icon { transform:scaleX(-1); }
    .drawer-close, .nav-trigger { display:none; }
    .drawer-close { --btn-fg:#c3c8dc; --btn-hover:rgb(255 255 255 / .08); }
    .workspace { display:flex; flex-direction:column; min-width:0; }
    .topbar { position:sticky; top:0; z-index:20; display:flex; align-items:center; gap:12px; height:var(--topbar-h); padding:0 32px; border-bottom:1px solid var(--c-line); background:rgb(255 255 255 / .92); backdrop-filter:blur(8px); }
    .breadcrumbs { flex:1; min-width:0; }
    .breadcrumbs ol { display:flex; align-items:center; gap:6px; margin:0; padding:0; list-style:none; font-size:var(--text-sm); }
    .breadcrumbs li { display:flex; align-items:center; gap:6px; min-width:0; }
    .breadcrumbs a, .breadcrumbs span { max-width:240px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .breadcrumbs a { color:var(--c-muted); text-decoration:none; border-radius:4px; }
    .breadcrumbs a:hover { color:var(--c-primary-text); }
    .breadcrumbs [aria-current] { color:var(--c-text); font-weight:600; }
    .crumb-sep { color:var(--c-faint); }
    .account { position:relative; }
    .account-trigger { display:flex; align-items:center; gap:10px; padding:4px 8px 4px 4px; border:1px solid transparent; border-radius:12px; background:transparent; text-align:left; cursor:pointer; }
    .account-trigger:hover, .account-trigger[aria-expanded='true'] { border-color:var(--c-line); background:var(--c-surface-2); }
    .account-text { display:grid; line-height:1.25; }
    .account-name { font-size:13px; font-weight:600; }
    .account-role { color:var(--c-muted); font-size:12px; }
    .account-caret { color:var(--c-muted); }
    .account-menu { position:absolute; top:calc(100% + 8px); right:0; z-index:30; width:272px; padding:6px; border:1px solid var(--c-line); border-radius:12px; background:var(--c-surface); box-shadow:var(--shadow-pop); }
    .account-card { display:flex; gap:12px; padding:10px 10px 14px; margin-bottom:6px; border-bottom:1px solid var(--c-line); }
    .account-card > div { display:grid; gap:2px; justify-items:start; min-width:0; }
    .account-card-name { font-weight:600; }
    .account-card-email { margin-bottom:6px; color:var(--c-muted); font-size:12px; overflow-wrap:anywhere; }
    .menu-item { display:flex; align-items:center; gap:10px; width:100%; height:38px; padding:0 10px; border:0; border-radius:8px; background:transparent; font-weight:500; cursor:pointer; }
    .menu-item:hover:not(:disabled) { background:var(--c-neutral-soft); }
    .menu-item:disabled { opacity:.55; cursor:not-allowed; }
    .notice { display:flex; align-items:center; gap:10px; margin:16px 32px 0; padding:8px 8px 8px 14px; border:1px solid #f5dfae; border-radius:10px; background:var(--c-warning-soft); color:var(--c-warning-text); font-size:var(--text-sm); }
    .notice span { flex:1; }
    .content:focus { outline:none; }
    @media (min-width:961px) {
      .shell { transition:grid-template-columns .2s ease; }
      .collapsed { grid-template-columns:var(--sidebar-w-collapsed) minmax(0,1fr); }
      .collapsed .sidebar-header { justify-content:center; padding:0; }
      .collapsed .brand-text, .collapsed .nav-toggle, .collapsed .submenu { display:none; }
      .collapsed .nav-label { position:absolute; width:1px; height:1px; overflow:hidden; clip:rect(0 0 0 0); }
      .collapsed .nav-group { height:1px; margin:14px 8px; overflow:hidden; background:rgb(255 255 255 / .1); font-size:0; }
      .collapsed .nav-link { justify-content:center; padding:0; }
    }
    @media (max-width:960px) {
      .shell { grid-template-columns:minmax(0,1fr); }
      .sidebar-rail { display:contents; }
      .sidebar { position:fixed; inset:0 auto 0 0; z-index:50; width:min(300px, 86vw); height:100%; box-shadow:var(--shadow-pop); transform:translateX(-100%); visibility:hidden; transition:transform .22s ease, visibility 0s linear .22s; }
      .sidebar.open { transform:none; visibility:visible; transition:transform .22s ease; }
      .sidebar-footer { display:none; }
      .drawer-close, .nav-trigger { display:inline-flex; }
      .backdrop { position:fixed; inset:0; z-index:40; background:rgb(24 28 52 / .45); }
      .topbar { gap:8px; padding:0 12px 0 8px; }
      .notice { margin:12px 16px 0; }
    }
    @media (max-width:600px) {
      .account-text { display:none; }
      .breadcrumbs li:not(:last-child) { display:none; }
    }
  `]
})
export class AppShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly recent = inject(RecentProjectsStore);
  readonly session = inject(SessionStore);
  readonly notice = inject(AppNoticeStore);

  private readonly sidebar = viewChild.required<ElementRef<HTMLElement>>('sidebar');
  private readonly drawerClose = viewChild.required<ElementRef<HTMLButtonElement>>('drawerClose');
  private readonly navTrigger = viewChild.required<ElementRef<HTMLButtonElement>>('navTrigger');
  private readonly account = viewChild.required<ElementRef<HTMLElement>>('account');
  private readonly accountTrigger = viewChild.required<ElementRef<HTMLButtonElement>>('accountTrigger');
  private readonly signOutButton = viewChild<ElementRef<HTMLButtonElement>>('signOutButton');

  readonly signingOut = signal(false);
  readonly navOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly collapsed = signal(readCollapsedPreference());

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => event.urlAfterRedirects)
    ),
    { initialValue: this.router.url }
  );

  readonly recentProjects = this.recent.projects;
  readonly inProjects = computed(() => /^\/(projects|tasks)(\/|\?|$)/.test(this.url()));
  readonly onProjectList = computed(() => /^\/projects(\?|$)/.test(this.url()));
  /** Follows the current section until the user toggles it. */
  readonly projectsOpen = linkedSignal(() => this.inProjects() || this.recentProjects().length > 0);
  readonly breadcrumbs = computed(() => buildBreadcrumbs(this.url(), id => this.recent.nameOf(id)));
  readonly role = computed(() => {
    const roles = this.session.user()?.roles ?? [];
    return ['Admin', 'Project Manager', 'Team Member'].find(role => roles.includes(role)) ?? roles[0] ?? 'Member';
  });

  constructor() {
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed()
    ).subscribe(() => {
      this.navOpen.set(false);
      this.userMenuOpen.set(false);
    });
  }

  openNav(): void {
    this.navOpen.set(true);
    afterNextRender(() => this.drawerClose().nativeElement.focus(), { injector: this.injector });
  }

  closeNav(restoreFocus = false): void {
    if (!this.navOpen()) return;
    this.navOpen.set(false);
    if (restoreFocus) this.navTrigger().nativeElement.focus();
  }

  toggleCollapsed(): void {
    this.collapsed.update(value => !value);
    try {
      localStorage.setItem(COLLAPSED_KEY, String(this.collapsed()));
    } catch {
      // Storage can be unavailable (private mode); the preference then lasts for this page only.
    }
  }

  toggleUserMenu(): void {
    this.userMenuOpen.update(open => !open);
    if (this.userMenuOpen()) {
      afterNextRender(() => this.signOutButton()?.nativeElement.focus(), { injector: this.injector });
    }
  }

  onEscape(): void {
    if (this.userMenuOpen()) {
      this.userMenuOpen.set(false);
      this.accountTrigger().nativeElement.focus();
    } else {
      this.closeNav(true);
    }
  }

  onDocumentClick(event: MouseEvent): void {
    if (this.userMenuOpen() && !this.account().nativeElement.contains(event.target as Node)) {
      this.userMenuOpen.set(false);
    }
  }

  /** Keeps Tab focus inside the off-canvas drawer while it is open. */
  trapFocus(event: KeyboardEvent): void {
    if (!this.navOpen() || event.key !== 'Tab') return;
    const focusable = Array.from(this.sidebar().nativeElement.querySelectorAll<HTMLElement>(FOCUSABLE))
      .filter(el => el.offsetParent !== null);
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  skipToContent(event: Event): void {
    event.preventDefault();
    document.getElementById('content')?.focus();
  }

  async logout(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.auth.logout();
    } finally {
      this.signingOut.set(false);
      this.userMenuOpen.set(false);
      this.recent.clear();
      await this.router.navigateByUrl('/login');
    }
  }
}

function readCollapsedPreference(): boolean {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === 'true';
  } catch {
    return false;
  }
}
