import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <main class="auth-shell">
      <section class="auth-card" aria-labelledby="login-title">
        <p class="eyebrow">JMH Connect</p>
        <h1 id="login-title">Staff sign in</h1>

        @if (auth.currentUser(); as user) {
          <p class="signed-in" role="status">Signed in as {{ user.email }} · {{ user.role }}</p>
          <button type="button" [disabled]="busy()" (click)="logout()">Sign out</button>
        } @else {
          <form (ngSubmit)="login()" #loginForm="ngForm">
            <label for="email">Email</label>
            <input id="email" name="email" type="email" autocomplete="username" required email
              [(ngModel)]="email" [disabled]="busy()" />

            <label for="password">Password</label>
            <input id="password" name="password" type="password" autocomplete="current-password" required
              [(ngModel)]="password" [disabled]="busy()" />

            @if (errorMessage()) {
              <p class="error" role="alert">{{ errorMessage() }}</p>
            }

            <button type="submit" [disabled]="busy() || loginForm.invalid">
              {{ busy() ? 'Signing in…' : 'Sign in' }}
            </button>
          </form>
          <p class="account-policy">Accounts are created by a Pastor. Public sign-up is unavailable.</p>
        }
      </section>
    </main>
  `,
  styles: `
    :host { display: block; min-height: 100vh; }
    .auth-shell { display: grid; min-height: 100vh; place-items: center; padding: 1.5rem; background: #f2f2ed; }
    .auth-card { width: min(100%, 28rem); padding: 2rem; border: 1px solid #e4e4df; border-radius: 1rem; background: #fff; box-shadow: 0 1rem 2.5rem rgb(35 39 58 / 8%); }
    .eyebrow { color: #118ab2; font-size: .75rem; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 0 0 1.5rem; color: #464b71; font-size: 2rem; }
    form { display: grid; gap: .7rem; }
    label { margin-top: .5rem; color: #292c3b; font-weight: 600; }
    input { min-height: 2.8rem; padding: .65rem .75rem; border: 1px solid #cfd0d7; border-radius: .5rem; font: inherit; }
    input:focus-visible, button:focus-visible { outline: 3px solid #7cd5c7; outline-offset: 2px; }
    button { min-height: 2.8rem; margin-top: .6rem; border: 0; border-radius: .5rem; background: #464b71; color: #fff; font: inherit; font-weight: 700; cursor: pointer; }
    button:disabled { cursor: wait; opacity: .7; }
    .error { color: #8b2d2d; }
    .account-policy { margin: 1.25rem 0 0; color: #626574; font-size: .875rem; line-height: 1.5; }
    .signed-in { color: #292c3b; }
  `,
})
export class LoginComponent implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected email = '';
  protected password = '';

  ngOnInit(): void {
    this.auth.loadCurrentUser().subscribe({
      next: (user) => {
        if (user) void this.router.navigateByUrl(this.returnUrl());
      },
      error: () => this.errorMessage.set('Could not check the current session.'),
    });
  }

  protected login(): void {
    this.busy.set(true);
    this.errorMessage.set(null);
    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.password = '';
        this.busy.set(false);
        void this.router.navigateByUrl(this.returnUrl());
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.status === 401
          ? 'The email or password is incorrect.'
          : 'Sign in is unavailable. Please try again.');
        this.busy.set(false);
      },
    });
  }

  protected logout(): void {
    this.busy.set(true);
    this.auth.logout().subscribe({
      next: () => {
        this.busy.set(false);
        void this.router.navigateByUrl('/login');
      },
      error: () => {
        this.errorMessage.set('Sign out could not be completed. Please try again.');
        this.busy.set(false);
      },
    });
  }

  private returnUrl(): string {
    const value = this.route.snapshot.queryParamMap.get('returnUrl');
    return value && value.startsWith('/') && !value.startsWith('//') ? value : '/home';
  }
}
