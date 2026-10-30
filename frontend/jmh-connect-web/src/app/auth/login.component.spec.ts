import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  function create(returnUrl = '/home') {
    const auth = {
      currentUser: vi.fn(() => null),
      loadCurrentUser: vi.fn(() => of(null)),
      login: vi.fn(() => of({ id: 'dummy', email: 'pastor@example.test', role: 'Pastor' as const })),
      logout: vi.fn(() => of(undefined)),
    };
    const router = { navigateByUrl: vi.fn(() => Promise.resolve(true)) };
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ returnUrl }) } } },
      ],
    });
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return { fixture, auth, router };
  }

  it('navigates to the protected return URL after login', () => {
    const { fixture, router } = create('/home');
    const component = fixture.componentInstance as any;
    component.email = 'pastor@example.test';
    component.password = 'Dummy-password!2026';

    component.login();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('redirects an unsafe return URL to the protected home route', () => {
    const { fixture, router } = create('//untrusted.example');
    const component = fixture.componentInstance as any;
    component.login();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('navigates to login after logout', () => {
    const { fixture, router } = create();
    const component = fixture.componentInstance as any;

    component.logout();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });
});
