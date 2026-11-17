import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
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
            [attr.autocomplete]="registering() ? 'new-password' : 'current-password'" />

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
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  toggleMode(): void {
    this.registering.update(value => !value);
    this.error.set('');
    const name = this.form.controls.displayName;
    if (this.registering()) {
      name.addValidators(Validators.required);
    } else {
      name.clearValidators();
    }
    name.updateValueAndValidity();
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
    } catch {
      this.error.set('Sign-in failed. Check your details and API connection, then try again.');
    } finally {
      this.loading.set(false);
    }
  }
}
