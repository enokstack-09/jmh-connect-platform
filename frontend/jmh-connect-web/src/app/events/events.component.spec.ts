import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { EventsComponent } from './events.component';
import { EventsService } from './events.service';

const event = { id: 2, title: 'JMH Family Evening', startsUtc: '2026-11-06T12:30:00Z', endsUtc: '2026-11-06T14:30:00Z', location: 'JMH Hall', status: 1 as const };

describe('EventsComponent', () => {
  it('provides authenticated Members list and calendar views without management controls', () => {
    TestBed.configureTestingModule({ imports: [EventsComponent], providers: [
      { provide: AuthService, useValue: { currentUser: signal({ id: 'member', email: 'member@example.test', role: 'Member', memberProfileId: 7 }) } },
      { provide: EventsService, useValue: { list: vi.fn(() => of([event])) } },
    ] });
    const fixture = TestBed.createComponent(EventsComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('JMH Family Evening');
    expect(fixture.nativeElement.textContent).toContain('Calendar');
    expect(fixture.nativeElement.textContent).not.toContain('Create event');
    expect(fixture.nativeElement.textContent).not.toContain('Audit');
  });

  it('shows Pastor event management controls', () => {
    TestBed.configureTestingModule({ imports: [EventsComponent], providers: [
      { provide: AuthService, useValue: { currentUser: signal({ id: 'pastor', email: 'pastor@example.test', role: 'Pastor', memberProfileId: null }) } },
      { provide: EventsService, useValue: { list: vi.fn(() => of([event])) } },
    ] });
    const fixture = TestBed.createComponent(EventsComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Create event');
    expect(fixture.nativeElement.textContent).toContain('Edit');
    expect(fixture.nativeElement.textContent).toContain('Cancel');
    expect(fixture.nativeElement.textContent).toContain('Audit');
  });
});
