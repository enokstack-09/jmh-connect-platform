import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { HomeComponent } from './home.component';
import { BirthdaysService } from '../birthdays/birthdays.service';
import { AnnouncementsService } from '../announcements/announcements.service';
import { DutiesService } from '../duties/duties.service';
import { EventsService } from '../events/events.service';

const dashboardProviders = [
  { provide: BirthdaysService, useValue: { list: vi.fn(() => of([])) } },
  { provide: AnnouncementsService, useValue: { list: vi.fn(() => of([])) } },
  { provide: DutiesService, useValue: { list: vi.fn(() => of([])) } },
  { provide: EventsService, useValue: { list: vi.fn(() => of([])) } },
];

describe('HomeComponent', () => {
  it('shows the authenticated role and redirects after logout', () => {
    const auth = {
      currentUser: vi.fn(() => ({ id: 'dummy', email: 'pastor@example.test', role: 'Pastor' })),
      logout: vi.fn(() => of(undefined)),
    };
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        provideRouter([]),
        ...dashboardProviders,
      ],
    });
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Pastor');
    expect(fixture.nativeElement.textContent).toContain('Family reminders');
    expect(fixture.nativeElement.querySelector('a[href="/duties"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/events"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Open My Profile');
    (fixture.componentInstance as any).logout();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('shows working profile and directory links to Members', () => {
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        { provide: AuthService, useValue: { currentUser: vi.fn(() => ({ id: 'member', email: 'member@example.test', role: 'Member', memberProfileId: 7 })), logout: vi.fn(() => of(undefined)) } },
        provideRouter([]),
        ...dashboardProviders,
      ],
    });
    const fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();
    const links = Array.from(fixture.nativeElement.querySelectorAll('a')).map((link: any) => link.getAttribute('href'));
    expect(links).toContain('/my-profile');
    expect(links).toContain('/members');
    expect(links).toContain('/duties');
    expect(links).toContain('/events');
    expect(links).toContain('/birthdays');
    expect(links).toContain('/announcements');
  });

  it('does not offer a My Profile route when the signed-in Member has no link', () => {
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        { provide: AuthService, useValue: { currentUser: vi.fn(() => ({ id: 'member', email: 'member@example.test', role: 'Member', memberProfileId: null })), logout: vi.fn(() => of(undefined)) } },
        provideRouter([]),
        ...dashboardProviders,
      ],
    });
    const fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('My Profile unavailable');
    expect(fixture.nativeElement.querySelector('a[href="/my-profile"]')).toBeNull();
  });
});
