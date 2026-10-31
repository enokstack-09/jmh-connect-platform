import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { App } from './app';
import { AuthService } from './auth/auth.service';
import { Component } from '@angular/core';
import { signal } from '@angular/core';
import { AuthenticatedUser } from './auth/auth.models';

@Component({ template: '<p>page</p>' })
class StubPage {}

describe('App shell', () => {
  async function setup(user: AuthenticatedUser | null, startUrl: string) {
    const currentUser = signal<AuthenticatedUser | null>(user);
    const auth = { currentUser, logout: vi.fn(() => of(undefined)) };
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        { provide: AuthService, useValue: auth },
        provideRouter([
          { path: '', component: StubPage },
          { path: 'login', component: StubPage },
          { path: 'home', component: StubPage },
        ]),
      ],
    });
    const router = TestBed.inject(Router);
    await router.navigateByUrl(startUrl);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, auth, router, element: fixture.nativeElement as HTMLElement };
  }

  it('hides the sidebar on public pages', async () => {
    const { element } = await setup(null, '/');
    expect(element.querySelector('.sidebar')).toBeNull();
  });

  it('shows role-aware navigation for a signed-in Pastor', async () => {
    const { element } = await setup({ id: 'p', email: 'pastor@example.test', role: 'Pastor' }, '/home');
    const labels = Array.from(element.querySelectorAll('.nav a')).map((a) => a.textContent?.trim());
    expect(labels).toContain('Dashboard');
    expect(labels).toContain('Duty Planning');
    expect(labels).toContain('Reports');
    expect(labels).not.toContain('My Profile');
  });

  it('limits navigation for a Leader and shows My Profile for a linked Member', async () => {
    const leader = await setup({ id: 'l', email: 'leader@example.test', role: 'Leader' }, '/home');
    const leaderLabels = Array.from(leader.element.querySelectorAll('.nav a')).map((a) => a.textContent?.trim());
    expect(leaderLabels).not.toContain('Reports');
    expect(leaderLabels.some((label) => label?.includes('Dut'))).toBe(false);
  });

  it('shows My Profile and My Duties for a linked Member', async () => {
    const { element } = await setup({ id: 'm', email: 'member@example.test', role: 'Member', memberProfileId: 7 }, '/home');
    const labels = Array.from(element.querySelectorAll('.nav a')).map((a) => a.textContent?.trim());
    expect(labels).toContain('My Profile');
    expect(labels).toContain('My Duties');
  });

  it('signs out and returns to the login page', async () => {
    const { fixture, auth, router } = await setup({ id: 'p', email: 'pastor@example.test', role: 'Pastor' }, '/home');
    const spy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    (fixture.nativeElement.querySelector('.signout') as HTMLButtonElement).click();
    expect(auth.logout).toHaveBeenCalled();
    expect(spy).toHaveBeenCalledWith('/login');
  });
});
