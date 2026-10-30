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
      <section class="auth-card" aria-labelledby="setup-title">
        <p class="eyebrow">JMH Connect</p>
        <h1 id="setup-title">Set your password</h1>

        @if (complete()) {
          <p role="status">Your password is set. You can now sign in.</p>
          <a routerLink="/login">Go to sign in</a>
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
      </section>
    </main>
  `,
  styles: `
    :host { display: block; min-height: 100vh; }
    .auth-shell { display: grid; min-height: 100vh; place-items: center; padding: 1.5rem; background: #f2f2ed; }
    .auth-card { width: min(100%, 30rem); padding: 2rem; border: 1px solid #e4e4df; border-radius: 1rem; background: #fff; box-shadow: 0 1rem 2.5rem rgb(35 39 58 / 8%); }
    .eyebrow { color: #118ab2; font-size: .75rem; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 0 0 1.5rem; color: #464b71; font-size: 2rem; }
    form { display: grid; gap: .7rem; }
    label { margin-top: .5rem; color: #292c3b; font-weight: 600; }
    input { min-height: 2.8rem; padding: .65rem .75rem; border: 1px solid #cfd0d7; border-radius: .5rem; font: inherit; }
    input:focus-visible, button:focus-visible, a:focus-visible { outline: 3px solid #7cd5c7; outline-offset: 2px; }
    button { min-height: 2.8rem; margin-top: .6rem; border: 0; border-radius: .5rem; background: #464b71; color: #fff; font: inherit; font-weight: 700; cursor: pointer; }
    button:disabled { cursor: not-allowed; opacity: .7; }
    a { color: #118ab2; font-weight: 600; }
    .hint { margin: 0; color: #626574; font-size: .875rem; line-height: 1.5; }
    .error { color: #8b2d2d; line-height: 1.5; }
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
