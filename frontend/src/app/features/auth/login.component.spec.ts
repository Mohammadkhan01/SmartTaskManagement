import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LoginComponent } from './login.component';

const STRONG = 'Str0ng!Passw0rd';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let element: HTMLElement;
  let auth: { login: ReturnType<typeof vi.fn>; register: ReturnType<typeof vi.fn> };
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    auth = { login: vi.fn().mockResolvedValue(undefined), register: vi.fn().mockResolvedValue(undefined) };
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    element = fixture.nativeElement;
    fixture.detectChanges();
  });

  function type(selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
  }

  const submitButton = () => element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
  const errorText = () => Array.from(element.querySelectorAll('.error')).map(e => e.textContent?.trim());

  function switchToRegister(): void {
    element.querySelector<HTMLButtonElement>('.text-button')!.click();
    fixture.detectChanges();
  }

  describe('sign-in mode', () => {
    it('renders the sign-in form without the name field or password rules', () => {
      expect(element.querySelector('h1')?.textContent).toContain('Welcome back');
      expect(element.querySelector('#displayName')).toBeNull();
      expect(element.querySelector('#password-rules')).toBeNull();
      expect(submitButton().textContent).toContain('Sign in');
    });

    it('requires a valid email and a password', () => {
      expect(submitButton().disabled).toBe(true);

      type('#email', 'not-an-email');
      type('#password', 'x');
      expect(component.form.controls.email.hasError('email')).toBe(true);
      expect(submitButton().disabled).toBe(true);

      type('#email', 'pat@example.com');
      expect(submitButton().disabled).toBe(false);
    });

    it('does not apply registration password rules when signing in', () => {
      type('#email', 'pat@example.com');
      type('#password', 'weak');
      expect(component.form.valid).toBe(true);
    });

    it('logs in with trimmed email and navigates to the dashboard', async () => {
      type('#email', 'pat@example.com ');
      type('#password', ' secret ');

      await component.submit();

      expect(auth.login).toHaveBeenCalledWith({ email: 'pat@example.com', password: ' secret ' });
      expect(auth.register).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/dashboard']);
      expect(component.loading()).toBe(false);
    });

    it('shows a loading state while signing in', async () => {
      let resolveLogin!: () => void;
      auth.login.mockReturnValue(new Promise<void>(resolve => (resolveLogin = resolve)));
      type('#email', 'pat@example.com');
      type('#password', 'secret');

      const pending = component.submit();
      fixture.detectChanges();
      expect(submitButton().disabled).toBe(true);
      expect(submitButton().textContent).toContain('Please wait');

      resolveLogin();
      await pending;
      fixture.detectChanges();
      expect(submitButton().textContent).toContain('Sign in');
    });

    it.each([
      [401, 'Incorrect email or password.'],
      [429, 'Too many attempts. Wait a minute and try again.'],
      [0, 'Cannot reach the server. Check your connection and try again.'],
      [500, 'Sign-in failed. Check your details and API connection, then try again.']
    ])('shows a message for HTTP %s', async (status, message) => {
      auth.login.mockRejectedValue(new HttpErrorResponse({ status }));
      type('#email', 'pat@example.com');
      type('#password', 'secret');

      await component.submit();
      fixture.detectChanges();

      expect(errorText()).toContain(message);
      expect(navigate).not.toHaveBeenCalled();
      expect(submitButton().disabled).toBe(false);
    });

    it('marks fields touched instead of submitting an invalid form', async () => {
      await component.submit();
      expect(auth.login).not.toHaveBeenCalled();
      expect(component.form.controls.email.touched).toBe(true);
    });
  });

  describe('registration mode', () => {
    beforeEach(() => switchToRegister());

    it('shows the name field, password rules and register heading', () => {
      expect(element.querySelector('h1')?.textContent).toContain('Create your account');
      expect(element.querySelector('#displayName')).not.toBeNull();
      expect(element.querySelectorAll('#password-rules li')).toHaveLength(5);
      expect(element.querySelector('#password')?.getAttribute('autocomplete')).toBe('new-password');
      expect(submitButton().textContent).toContain('Create account');
    });

    it('requires a display name', () => {
      type('#email', 'pat@example.com');
      type('#password', STRONG);
      expect(component.form.controls.displayName.hasError('required')).toBe(true);
      expect(submitButton().disabled).toBe(true);

      type('#displayName', 'Pat');
      expect(submitButton().disabled).toBe(false);
    });

    it.each([
      ['Sh0rt!Pass', 'at least 12 characters'],
      ['str0ng!passw0rd', 'an uppercase letter'],
      ['STR0NG!PASSW0RD', 'a lowercase letter'],
      ['Strong!Password', 'a digit'],
      ['Str0ngPassw0rd1', 'a symbol']
    ])('rejects %s because it lacks %s', (password, missing) => {
      type('#displayName', 'Pat');
      type('#email', 'pat@example.com');
      type('#password', password);

      expect(component.form.controls.password.hasError('weakPassword')).toBe(true);
      expect(submitButton().disabled).toBe(true);
      expect(errorText().join(' ')).toContain(`Password must have ${missing}`);
    });

    it('marks each satisfied rule as met and accepts a strong password', () => {
      type('#password', 'abc');
      const met = () => Array.from(element.querySelectorAll('#password-rules li.met')).length;
      expect(met()).toBe(1);

      type('#password', STRONG);
      expect(met()).toBe(5);
      expect(component.form.controls.password.valid).toBe(true);
    });

    it('registers with trimmed values and navigates to the dashboard', async () => {
      type('#displayName', '  Pat Lee ');
      type('#email', 'pat@example.com ');
      type('#password', STRONG);

      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();

      expect(auth.register).toHaveBeenCalledWith({
        displayName: 'Pat Lee',
        email: 'pat@example.com',
        password: STRONG
      });
      expect(navigate).toHaveBeenCalledWith(['/dashboard']);
    });

    it('shows the API validation message when registration is rejected', async () => {
      auth.register.mockRejectedValue(new HttpErrorResponse({
        status: 409,
        error: { detail: 'An account with this email already exists.' }
      }));
      type('#displayName', 'Pat');
      type('#email', 'pat@example.com');
      type('#password', STRONG);

      await component.submit();
      fixture.detectChanges();

      expect(errorText()).toContain('An account with this email already exists.');
      expect(navigate).not.toHaveBeenCalled();
    });

    it('falls back to a generic registration error', async () => {
      auth.register.mockRejectedValue(new HttpErrorResponse({ status: 400 }));
      type('#displayName', 'Pat');
      type('#email', 'pat@example.com');
      type('#password', STRONG);

      await component.submit();
      fixture.detectChanges();

      expect(errorText()).toContain('Registration failed. Check your details and try again.');
    });

    it('switching back to sign-in drops the registration-only validation', () => {
      type('#password', 'weak');
      expect(component.form.invalid).toBe(true);

      switchToRegister();
      type('#email', 'pat@example.com');
      expect(element.querySelector('#displayName')).toBeNull();
      expect(component.form.valid).toBe(true);
    });
  });
});
