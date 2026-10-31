import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="auth-shell">
      <section class="auth-brand" aria-label="About JMH Connect">
        <a class="logo" routerLink="/"><span>JMH</span>Connect</a>
        <div class="tagline">
          <h2>Welcome back to our JMH home</h2>
          <p>Sign in to see your community, duties, events and announcements.</p>
        </div>
        <a class="back" routerLink="/">← Back to home</a>
      </section>

      <section class="auth-panel">
        <div class="auth-card" aria-labelledby="login-title">
          <p class="eyebrow">JMH Connect</p>
          <h1 id="login-title">Sign in</h1>
          <p class="sub">Use the account your Pastor created for you.</p>

          @if (auth.currentUser(); as user) {
            <p class="signed-in" role="status">Signed in as {{ user.email }} · {{ user.role }}</p>
            <button type="button" [disabled]="busy()" (click)="logout()">Sign out</button>
          } @else {
            <form (ngSubmit)="login()" #loginForm="ngForm">
              <label for="email">Email</label>
              <input id="email" name="email" type="email" autocomplete="username" required email
                placeholder="you@example.com" [(ngModel)]="email" [disabled]="busy()" />

              <label for="password">Password</label>
              <input id="password" name="password" type="password" autocomplete="current-password" required
                placeholder="Your password" [(ngModel)]="password" [disabled]="busy()" />

              @if (errorMessage()) {
                <p class="error" role="alert">{{ errorMessage() }}</p>
              }

              <button type="submit" [disabled]="busy() || loginForm.invalid">
                {{ busy() ? 'Signing in…' : 'Sign in' }}
              </button>
            </form>
            <p class="account-policy">Accounts are created by a Pastor. Public sign-up is unavailable.</p>
          }
        </div>
      </section>
    </main>
  `,
  styles: `
    :host { display: block; min-height: 100vh; }
    .auth-shell { display: grid; grid-template-columns: 1.05fr 1fr; min-height: 100vh; background: #f2f2ed; }
    .auth-brand { position: relative; display: flex; flex-direction: column; justify-content: space-between; gap: 2rem; overflow: hidden; padding: clamp(2rem, 5vw, 4rem); color: #fff; background: radial-gradient(34rem 22rem at 100% 0%, rgb(17 138 178 / 55%), transparent 65%), radial-gradient(24rem 18rem at 0% 110%, rgb(124 213 199 / 22%), transparent 65%), linear-gradient(135deg, #464b71, #2f3454); }
    .auth-brand::after { content: ""; position: absolute; right: -7rem; bottom: -7rem; width: 22rem; height: 22rem; border-radius: 50%; border: 1px solid rgb(124 213 199 / 28%); box-shadow: 0 0 0 2.4rem rgb(124 213 199 / 5%), 0 0 0 4.8rem rgb(124 213 199 / 3%); pointer-events: none; }
    .auth-brand > * { position: relative; z-index: 1; }
    .logo { display: inline-flex; align-items: center; gap: .65rem; color: inherit; font-family: "Cormorant Garamond", Georgia, serif; font-size: 1.6rem; font-weight: 600; text-decoration: none; }
    .logo span { display: grid; width: 2.5rem; height: 2.5rem; place-items: center; border-radius: .8rem; color: #2b2f4a; background: #7cd5c7; font-family: Inter, sans-serif; font-size: .72rem; font-weight: 900; letter-spacing: .08em; }
    .tagline h2 { max-width: 13ch; margin: 0 0 1rem; font-family: "Cormorant Garamond", Georgia, serif; font-size: clamp(2.2rem, 4vw, 3.4rem); font-weight: 600; line-height: 1; }
    .tagline p { max-width: 26rem; margin: 0; color: rgb(255 255 255 / 76%); line-height: 1.65; }
    .back { color: rgb(255 255 255 / 70%); font-size: .85rem; font-weight: 600; text-decoration: none; }
    .back:hover { color: #fff; }
    .auth-panel { display: grid; place-items: center; padding: clamp(1.25rem, 4vw, 3rem); }
    .auth-card { width: min(100%, 26rem); }
    .eyebrow { margin: 0 0 .5rem; color: #118ab2; font-size: .72rem; font-weight: 800; letter-spacing: .18em; text-transform: uppercase; }
    h1 { margin: 0 0 .4rem; color: #464b71; font-family: "Cormorant Garamond", Georgia, serif; font-size: 2.6rem; font-weight: 700; line-height: 1; }
    .sub { margin: 0 0 1.6rem; color: #6b7084; line-height: 1.55; }
    form { display: grid; gap: .5rem; padding: 1.6rem; border: 1px solid rgb(70 75 113 / 12%); border-radius: 1.3rem; background: #fff; box-shadow: 0 .6rem 2rem rgb(35 39 58 / 8%); }
    label { margin-top: .4rem; color: #292d42; font-size: .85rem; font-weight: 700; }
    input { min-height: 2.9rem; padding: .65rem .85rem; border: 1px solid #cfd0d7; border-radius: .75rem; color: #232740; background: #fcfcfa; font: inherit; transition: border-color .2s, box-shadow .2s; }
    input:focus { border-color: #118ab2; outline: 0; box-shadow: 0 0 0 4px rgb(17 138 178 / 14%); }
    button { min-height: 2.9rem; margin-top: .8rem; border: 0; border-radius: .8rem; color: #fff; background: linear-gradient(135deg, #464b71, #118ab2 160%); font: inherit; font-weight: 750; cursor: pointer; box-shadow: 0 .6rem 1.4rem rgb(70 75 113 / 24%); transition: transform .18s, box-shadow .2s; }
    button:hover:not(:disabled) { transform: translateY(-1px); }
    button:focus-visible, a:focus-visible { outline: 3px solid #7cd5c7; outline-offset: 3px; }
    button:disabled { cursor: not-allowed; opacity: .65; }
    .error { margin: .3rem 0 0; padding: .7rem .85rem; border: 1px solid #e5bcbc; border-radius: .7rem; color: #8b2d2d; background: #fdf4f4; font-size: .88rem; line-height: 1.5; }
    .hint { margin: 0; color: #6b7084; font-size: .82rem; line-height: 1.5; }
    .account-policy { margin: 1.2rem 0 0; color: #6b7084; font-size: .85rem; line-height: 1.5; text-align: center; }
    .signed-in { padding: 1rem; border-radius: .9rem; color: #292d42; background: #fff; }
    a.link { color: #118ab2; font-weight: 700; }
    @media (max-width: 52rem) {
      .auth-shell { grid-template-columns: 1fr; }
      .auth-brand { gap: 1.2rem; padding: 1.4rem 1.25rem 1.8rem; }
      .tagline h2 { font-size: 2rem; }
      .tagline p, .back { display: none; }
    }
    @media (prefers-reduced-motion: reduce) { * { transition: none !important; } }
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
