import { signal } from '@angular/core';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { authGuard } from './auth.guard';

describe('authGuard', () => {
  function configure(user: ReturnType<typeof signal>, loadCurrentUser: ReturnType<typeof vi.fn>) {
    const loginTree = {} as UrlTree;
    const router = { createUrlTree: vi.fn(() => loginTree) };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: user, loadCurrentUser } },
        { provide: Router, useValue: router },
      ],
    });
    return { loginTree, router };
  }

  it('allows navigation when authenticated state is already loaded', () => {
    const user = signal({ id: 'dummy', email: 'pastor@example.test', role: 'Pastor' as const });
    const loadCurrentUser = vi.fn();
    configure(user, loadCurrentUser);

    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/home' } as RouterStateSnapshot));

    expect(result).toBe(true);
    expect(loadCurrentUser).not.toHaveBeenCalled();
  });

  it('loads a session before allowing navigation', async () => {
    const user = signal(null);
    const loadCurrentUser = vi.fn(() => of({ id: 'dummy', email: 'member@example.test', role: 'Member' as const }));
    configure(user, loadCurrentUser);

    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/home' } as RouterStateSnapshot));

    expect(await firstValueFrom(result as ReturnType<typeof of>)).toBe(true);
    expect(loadCurrentUser).toHaveBeenCalledOnce();
  });

  it('redirects an expired or unavailable session to login', async () => {
    const user = signal(null);
    const loadCurrentUser = vi.fn(() => throwError(() => new Error('Session expired')));
    const { loginTree, router } = configure(user, loadCurrentUser);

    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/home' } as RouterStateSnapshot));

    expect(await firstValueFrom(result as ReturnType<typeof of>)).toBe(loginTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/home' } });
  });
});
