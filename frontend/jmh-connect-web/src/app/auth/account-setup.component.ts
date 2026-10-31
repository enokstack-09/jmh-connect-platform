import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { take } from 'rxjs';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-account-setup',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="auth-shell">
      <section class="auth-brand" aria-label="About JMH Connect">
        <a class="logo" routerLink="/"><span>JMH</span>Connect</a>
        <div class="tagline">
          <h2>Welcome to the JMH family</h2>
          <p>Choose a strong password to finish setting up your account.</p>
        </div>
        <a class="back" routerLink="/">← Back to home</a>
      </section>

      <section class="auth-panel">
        <div class="auth-card" aria-labelledby="setup-title">
          <p class="eyebrow">JMH Connect</p>
          <h1 id="setup-title">Set your password</h1>
          <p class="sub">This link can only be used once.</p>

          @if (complete()) {
            <p class="signed-in" role="status">Your password is set. You can now sign in.</p>
            <p class="account-policy"><a class="link" routerLink="/login">Go to sign in</a></p>
          } @else if (invalidLink()) {
            <p class="error" role="alert">This setup link is invalid or has expired. Ask a Pastor to send a new link.</p>
          } @else {
            <form (ngSubmit)="submit()" #setupForm="ngForm">
              <label for="password">New password</label>
              <input id="password" name="password" type="password" autocomplete="new-password" required minlength="12"
                [(ngModel)]="password" [disabled]="busy()" />
              <p class="hint">Use at least 12 characters, including uppercase, lowercase, a number, and a symbol.</p>

              <label for="confirmPassword">Confirm password</label>
              <input id="confirmPassword" name="confirmPassword" type="password" autocomplete="new-password" required
                [(ngModel)]="confirmPassword" [disabled]="busy()" />

              @if (errorMessage()) {
                <p class="error" role="alert">{{ errorMessage() }}</p>
              }

              <button type="submit" [disabled]="busy() || setupForm.invalid || password !== confirmPassword">
                {{ busy() ? 'Saving…' : 'Set password' }}
              </button>
            </form>
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
export class AccountSetupComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  protected readonly busy = signal(false);
  protected readonly complete = signal(false);
  protected readonly invalidLink = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected userId = '';
  protected token = '';
  protected password = '';
  protected confirmPassword = '';

  ngOnInit(): void {
    this.route.queryParamMap.pipe(take(1)).subscribe((params) => {
      this.userId = params.get('userId') ?? '';
      this.token = params.get('token') ?? '';
      this.invalidLink.set(!this.userId || !this.token);
      void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    });
  }

  protected submit(): void {
    if (this.password !== this.confirmPassword || !this.userId || !this.token) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    this.auth.completeAccountSetup({
      userId: this.userId,
      token: this.token,
      password: this.password,
    }).subscribe({
      next: () => {
        this.password = '';
        this.confirmPassword = '';
        this.token = '';
        this.complete.set(true);
        this.busy.set(false);
      },
      error: () => {
        this.invalidLink.set(true);
        this.busy.set(false);
      },
    });
  }
}
