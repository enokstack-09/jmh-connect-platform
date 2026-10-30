import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { DevotionsComponent } from './devotions.component';
import { DevotionsService } from './devotions.service';

const session = { id: 3, title: 'Morning Devotion', scheduledStartUtc: '2026-11-02T00:30:00Z', durationMinutes: 30, location: 'JMH Hall', status: 1 as const };

describe('DevotionsComponent', () => {
  it('shows a Member only the shared schedule and their own attendance history', () => {
    const service = {
      list: vi.fn(() => of([session])),
      myAttendance: vi.fn(() => of([{ sessionId: 3, title: 'Morning Devotion', scheduledStartUtc: session.scheduledStartUtc, sessionStatus: 1, status: 1 }])),
    };
    TestBed.configureTestingModule({ imports: [DevotionsComponent], providers: [
      { provide: AuthService, useValue: { currentUser: signal({ id: 'member', email: 'member@example.test', role: 'Member', memberProfileId: 7 }) } },
      { provide: DevotionsService, useValue: service },
    ] });
    const fixture = TestBed.createComponent(DevotionsComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Morning Devotion');
    expect(fixture.nativeElement.textContent).toContain('My attendance history');
    expect(fixture.nativeElement.textContent).toContain('Present');
    expect(fixture.nativeElement.textContent).not.toContain('Schedule devotion');
    expect(fixture.nativeElement.textContent).not.toContain('Audit history');
  });

  it('shows Pastor attendance summaries and management controls', () => {
    const service = { list: vi.fn(() => of([{ ...session, attendanceSummary: { present: 1, absent: 2, excused: 0, unrecorded: 3 } }])) };
    TestBed.configureTestingModule({ imports: [DevotionsComponent], providers: [
      { provide: AuthService, useValue: { currentUser: signal({ id: 'pastor', email: 'pastor@example.test', role: 'Pastor', memberProfileId: null }) } },
      { provide: DevotionsService, useValue: service },
    ] });
    const fixture = TestBed.createComponent(DevotionsComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Schedule devotion');
    expect(fixture.nativeElement.textContent).toContain('1 Present');
    expect(fixture.nativeElement.textContent).toContain('3 Unrecorded');
    expect(fixture.nativeElement.textContent).toContain('Attendance');
  });
});
