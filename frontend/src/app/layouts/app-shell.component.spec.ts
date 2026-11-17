import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { SessionStore } from '../core/auth/session-store';
import { AppNoticeStore } from '../core/services/app-notice.store';
import { AppShellComponent } from './app-shell.component';

describe('AppShellComponent', () => {
  let fixture: ComponentFixture<AppShellComponent>;
  let element: HTMLElement;
  let logout: ReturnType<typeof vi.fn>;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  function render(roles: string[]): void {
    TestBed.inject(SessionStore).setSession('t', {
      userId: 'u1',
      displayName: 'Pat Lee',
      email: 'pat@example.com',
      roles
    });
    fixture = TestBed.createComponent(AppShellComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
  }

  const navLabels = () =>
    Array.from(element.querySelectorAll('nav[aria-label="Main navigation"] .nav-link'))
      .map(a => a.textContent?.trim());
  const accountTrigger = () => element.querySelector<HTMLButtonElement>('.account-trigger')!;

  function openUserMenu(): void {
    accountTrigger().click();
    fixture.detectChanges();
  }

  beforeEach(() => {
    logout = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: { logout } }]
    });
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  it('shows the Administration > Users link for administrators', () => {
    render(['Admin']);
    expect(navLabels()).toEqual(['Dashboard', 'Projects', 'Users']);
    expect(element.querySelector('a[href="/admin/users"]')).not.toBeNull();
    expect(element.textContent).toContain('Administration');
  });

  it.each([['Project Manager'], ['Team Member']])('hides the Admin link for %s', role => {
    render([role]);
    expect(navLabels()).toEqual(['Dashboard', 'Projects']);
    expect(element.querySelector('a[href="/admin/users"]')).toBeNull();
    expect(element.textContent).not.toContain('Administration');
  });

  it('shows the signed-in user name and role', () => {
    render(['Team Member']);
    expect(accountTrigger().textContent).toContain('Pat Lee');
    expect(accountTrigger().textContent).toContain('Team Member');
  });

  it('opens the user menu, and Escape closes it and returns focus to the trigger', () => {
    render(['Project Manager']);
    expect(accountTrigger().getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('#account-menu')).toBeNull();

    openUserMenu();
    expect(accountTrigger().getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('#account-menu')?.textContent).toContain('pat@example.com');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(element.querySelector('#account-menu')).toBeNull();
    expect(document.activeElement).toBe(accountTrigger());
  });

  it('closes the user menu on an outside click', () => {
    render(['Team Member']);
    openUserMenu();

    document.body.click();
    fixture.detectChanges();
    expect(element.querySelector('#account-menu')).toBeNull();
  });

  it('toggles the projects sub-menu', () => {
    render(['Team Member']);
    const toggle = element.querySelector<HTMLButtonElement>('button[aria-controls="projects-submenu"]')!;
    const submenu = element.querySelector<HTMLElement>('#projects-submenu')!;
    const initial = toggle.getAttribute('aria-expanded') === 'true';
    expect(submenu.hidden).toBe(!initial);

    toggle.click();
    fixture.detectChanges();
    expect(toggle.getAttribute('aria-expanded')).toBe(String(!initial));
    expect(submenu.hidden).toBe(initial);
    expect(submenu.querySelector('a[href="/projects"]')?.textContent).toContain('All projects');
  });

  it('opens the mobile drawer and closes it with Escape', () => {
    render(['Team Member']);
    const trigger = element.querySelector<HTMLButtonElement>('.nav-trigger')!;

    trigger.click();
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('.sidebar')?.classList).toContain('open');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(trigger);
  });

  it('signs out and navigates to /login', async () => {
    render(['Team Member']);
    openUserMenu();
    const button = element.querySelector<HTMLButtonElement>('#account-menu button')!;

    button.click();
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('Signing out');

    await fixture.whenStable();
    expect(logout).toHaveBeenCalledTimes(1);
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('still navigates to /login when the logout request fails', async () => {
    logout.mockRejectedValue(new Error('offline'));
    render(['Team Member']);

    await fixture.componentInstance.logout().catch(() => undefined);

    expect(navigateByUrl).toHaveBeenCalledWith('/login');
    expect(fixture.componentInstance.signingOut()).toBe(false);
  });

  it('renders and dismisses the app-wide notice banner', () => {
    render(['Team Member']);
    expect(element.querySelector('.notice')).toBeNull();

    TestBed.inject(AppNoticeStore).show('Cannot reach the Smart Task server.');
    fixture.detectChanges();
    const banner = element.querySelector('.notice[role="alert"]');
    expect(banner?.textContent).toContain('Cannot reach the Smart Task server.');

    banner!.querySelector('button')!.click();
    fixture.detectChanges();
    expect(element.querySelector('.notice')).toBeNull();
  });
});
