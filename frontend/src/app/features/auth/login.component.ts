import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { validationMessage } from '../../core/api/api-error';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <main class="auth-shell">
      <section class="auth-card">
        <div class="brand-mark">ST</div>
        <p class="eyebrow">SMART TASK MANAGEMENT</p>
        <h1>{{ registering() ? 'Create your account' : 'Welcome back' }}</h1>
        <p class="subheading">
          {{ registering() ? 'Get your team organised and moving.' : 'Sign in to continue to your workspace.' }}
        </p>

        <form [formGroup]="form" (ngSubmit)="submit()">
          <label *ngIf="registering()" for="displayName">Full name</label>
          <input *ngIf="registering()" id="displayName" formControlName="displayName"
            placeholder="Your name" autocomplete="name" />

          <label for="email">Email address</label>
          <input id="email" type="email" formControlName="email"
            placeholder="you@company.com" autocomplete="email" />

          <label for="password">Password</label>
          <input id="password" type="password" formControlName="password"
            placeholder="Enter your password"
            [attr.autocomplete]="registering() ? 'new-password' : 'current-password'"
            [attr.aria-describedby]="registering() ? 'password-rules' : null" />

          <ul *ngIf="registering()" id="password-rules" class="password-rules" aria-live="polite">
            <li *ngFor="let rule of passwordRules" [class.met]="rule.met(form.controls.password.value)">
              {{ rule.label }}
            </li>
          </ul>
          <p class="error" *ngIf="registering() && form.controls.password.touched && form.controls.password.invalid">
            {{ passwordError() }}
          </p>

          <p class="error" *ngIf="error()">{{ error() }}</p>

          <button type="submit" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Please wait…' : (registering() ? 'Create account' : 'Sign in') }}
          </button>
        </form>

        <p class="switch-mode">
          {{ registering() ? 'Already have an account?' : 'New to Smart Task?' }}
          <button type="button" class="text-button" (click)="toggleMode()">
            {{ registering() ? 'Sign in' : 'Create account' }}
          </button>
        </p>
        <p class="security-note">Protected sign-in · Secure session cookies</p>
      </section>
    </main>
  `,
  styles: [`
    :host { display:block; min-height:100vh; }
    .auth-shell { min-height:100vh; display:grid; place-items:center; padding:24px; background:#f3f5f9; }
    .auth-card { width:min(100%,420px); padding:42px; background:#fff; border:1px solid #e6e9ef; border-radius:18px; box-shadow:0 18px 55px #18243a0d; }
    .brand-mark { display:grid; place-items:center; width:44px; height:44px; border-radius:12px; background:#243b6b; color:white; font-weight:800; }
    .eyebrow { margin:25px 0 9px; color:#63718a; font-size:11px; font-weight:800; letter-spacing:1.5px; }
    h1 { margin:0; color:#17243b; font-size:28px; letter-spacing:-.8px; }
    .subheading { margin:10px 0 28px; color:#758198; font-size:14px; }
    form { display:grid; gap:10px; }
    label { margin-top:8px; color:#344158; font-size:13px; font-weight:650; }
    input { width:100%; box-sizing:border-box; padding:13px 14px; border:1px solid #dce1e9; border-radius:9px; outline:none; font:inherit; }
    input:focus { border-color:#4569ad; box-shadow:0 0 0 3px #4569ad1c; }
    button[type=submit] { margin-top:12px; padding:14px; border:0; border-radius:9px; background:#243b6b; color:white; font-weight:700; cursor:pointer; }
    button:disabled { opacity:.55; cursor:not-allowed; }
    .error { color:#b42318; font-size:13px; }
    .password-rules { margin:2px 0 0; padding-left:18px; color:#758198; font-size:12px; line-height:1.7; }
    .password-rules li.met { color:#15803d; }
    .password-rules li.met::marker { content:'✓  '; }
    .switch-mode { margin:24px 0 0; text-align:center; color:#758198; font-size:13px; }
    .text-button { padding:0; border:0; background:none; color:#294d8b; font-weight:700; cursor:pointer; }
    .security-note { margin:26px 0 0; text-align:center; color:#9aa3b2; font-size:11px; }
    @media(max-width:480px) { .auth-card { padding:28px 22px; } }
  `]
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly registering = signal(false);
  readonly loading = signal(false);
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    displayName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  readonly passwordRules = PASSWORD_RULES;

  passwordError(): string {
    const value = this.form.controls.password.value;
    if (!value) return 'A password is required.';
    const failed = PASSWORD_RULES.filter(rule => !rule.met(value));
    return failed.length ? `Password must have ${failed.map(rule => rule.short).join(', ')}.` : '';
  }

  toggleMode(): void {
    this.registering.update(value => !value);
    this.error.set('');
    const name = this.form.controls.displayName;
    const password = this.form.controls.password;
    if (this.registering()) {
      name.setValidators([Validators.required, Validators.maxLength(150)]);
      password.setValidators([Validators.required, strongPasswordValidator]);
    } else {
      name.clearValidators();
      password.setValidators([Validators.required]);
    }
    name.updateValueAndValidity();
    password.updateValueAndValidity();
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    const value = this.form.getRawValue();

    try {
      if (this.registering()) {
        await this.auth.register({
          displayName: value.displayName.trim(),
          email: value.email.trim(),
          password: value.password
        });
      } else {
        await this.auth.login({
          email: value.email.trim(),
          password: value.password
        });
      }
      await this.router.navigate(['/dashboard']);
    } catch (error) {
      this.error.set(this.submitErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  private submitErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) return 'Cannot reach the server. Check your connection and try again.';
      if (error.status === 429) return 'Too many attempts. Wait a minute and try again.';
      if (this.registering() && (error.status === 400 || error.status === 409)) {
        return validationMessage(error) ?? 'Registration failed. Check your details and try again.';
      }
      if (error.status === 401 || error.status === 400) return 'Incorrect email or password.';
    }
    return this.registering()
      ? 'Registration failed. Check your details and try again.'
      : 'Sign-in failed. Check your details and API connection, then try again.';
  }
}

interface PasswordRule {
  label: string;
  short: string;
  met: (value: string) => boolean;
}

/** Mirrors the backend RegisterRequestValidator and ASP.NET Identity password options. */
const PASSWORD_RULES: PasswordRule[] = [
  { label: 'At least 12 characters', short: 'at least 12 characters', met: v => v.length >= 12 },
  { label: 'An uppercase letter (A–Z)', short: 'an uppercase letter', met: v => /[A-Z]/.test(v) },
  { label: 'A lowercase letter (a–z)', short: 'a lowercase letter', met: v => /[a-z]/.test(v) },
  { label: 'A digit (0–9)', short: 'a digit', met: v => /[0-9]/.test(v) },
  { label: 'A symbol (e.g. ! @ # $)', short: 'a symbol', met: v => /[^a-zA-Z0-9]/.test(v) }
];

const strongPasswordValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = (control.value as string) ?? '';
  if (!value) return null;
  return PASSWORD_RULES.every(rule => rule.met(value)) ? null : { weakPassword: true };
};
