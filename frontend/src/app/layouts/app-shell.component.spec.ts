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
    Array.from(element.querySelectorAll('nav a')).map(a => a.textContent?.trim());

  beforeEach(() => {
    logout = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: { logout } }]
    });
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  it('shows the Admin link for administrators', () => {
    render(['Admin']);
    expect(navLabels()).toEqual(['Dashboard', 'Projects', 'Admin']);
    expect(element.querySelector('a[href="/admin/users"]')).not.toBeNull();
  });

  it.each([['Project Manager'], ['Team Member']])('hides the Admin link for %s', role => {
    render([role]);
    expect(navLabels()).toEqual(['Dashboard', 'Projects']);
    expect(element.querySelector('a[href="/admin/users"]')).toBeNull();
  });

  it('shows the signed-in user name', () => {
    render(['Team Member']);
    expect(element.querySelector('.account')?.textContent).toContain('Pat Lee');
  });

  it('signs out and navigates to /login', async () => {
    render(['Team Member']);
    const button = element.querySelector<HTMLButtonElement>('.account button')!;

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
