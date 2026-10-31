import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { LandingComponent } from './landing.component';

describe('LandingComponent', () => {
  function create(user: { id: string; email: string; role: 'Pastor' | 'Leader' | 'Member' } | null) {
    const auth = {
      currentUser: vi.fn(() => user),
      loadCurrentUser: vi.fn(() => of(user)),
    };
    TestBed.configureTestingModule({
      imports: [LandingComponent],
      providers: [{ provide: AuthService, useValue: auth }, provideRouter([])],
    });
    const fixture = TestBed.createComponent(LandingComponent);
    fixture.detectChanges();
    return { fixture, auth, element: fixture.nativeElement as HTMLElement };
  }

  it('offers a Sign in button that leads to the login page for visitors', () => {
    const { element, auth } = create(null);

    const signIn = element.querySelector('header a.btn') as HTMLAnchorElement;
    expect(signIn.textContent).toContain('Sign in');
    expect(signIn.getAttribute('href')).toBe('/login');
    expect(element.querySelector('a[href="/home"]')).toBeNull();
    expect(auth.loadCurrentUser).toHaveBeenCalled();
  });

  it('offers the dashboard instead when a session already exists', () => {
    const { element } = create({ id: 'dummy', email: 'member@example.test', role: 'Member' });

    const open = element.querySelector('header a.btn') as HTMLAnchorElement;
    expect(open.textContent).toContain('Open dashboard');
    expect(open.getAttribute('href')).toBe('/home');
  });

  it('lists every community module', () => {
    const { element } = create(null);

    const text = element.textContent ?? '';
    for (const title of ['Family Directory', 'Devotions & Attendance', 'Duties', 'Community Events', 'Birthdays', 'Announcements']) {
      expect(text).toContain(title);
    }
  });
});
