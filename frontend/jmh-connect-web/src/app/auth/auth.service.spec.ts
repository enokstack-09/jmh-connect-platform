import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { routes } from '../app.routes';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('stores the authenticated user after login', () => {
    auth.login({ email: 'pastor@example.test', password: 'Dummy-password!2026' }).subscribe();
    httpTesting.expectOne('http://localhost:5080/api/auth/antiforgery').flush({ requestToken: 'dummy-csrf-token' });
    httpTesting.expectOne('http://localhost:5080/api/auth/login').flush({
      id: 'dummy-user-id', email: 'pastor@example.test', role: 'Pastor',
    });

    expect(auth.currentUser()).toEqual({ id: 'dummy-user-id', email: 'pastor@example.test', role: 'Pastor' });
  });

  it('clears authentication state after logout', () => {
    auth.currentUser.set({ id: 'dummy-user-id', email: 'pastor@example.test', role: 'Pastor' });
    auth.logout().subscribe();
    httpTesting.expectOne('http://localhost:5080/api/auth/antiforgery').flush({ requestToken: 'dummy-csrf-token' });
    httpTesting.expectOne('http://localhost:5080/api/auth/logout').flush(null);

    expect(auth.currentUser()).toBeNull();
  });

  it('clears authentication state when the current session returns 401', () => {
    auth.currentUser.set({ id: 'dummy-user-id', email: 'pastor@example.test', role: 'Pastor' });
    auth.loadCurrentUser().subscribe();
    httpTesting.expectOne('http://localhost:5080/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.currentUser()).toBeNull();
  });

  it('refreshes the antiforgery token when restoring an existing session', () => {
    auth.loadCurrentUser().subscribe();
    httpTesting.expectOne('http://localhost:5080/api/auth/me').flush({
      id: 'dummy-user-id', email: 'member@example.test', role: 'Member',
    });
    httpTesting.expectOne('http://localhost:5080/api/auth/antiforgery').flush({ requestToken: 'restored-session-token' });

    expect(auth.currentUser()?.email).toBe('member@example.test');
  });

  it('keeps login and account setup routes explicit and redirects unknown routes home', () => {
    expect(routes.find((route) => route.path === 'login')?.component).toBeDefined();
    expect(routes.find((route) => route.path === 'account/setup')?.component).toBeDefined();
    expect(routes.find((route) => route.path === '**')?.redirectTo).toBe('home');
  });
});
