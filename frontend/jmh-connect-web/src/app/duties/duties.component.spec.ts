import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { DutiesComponent } from './duties.component';
import { DutiesService } from './duties.service';

const duty = { id: 4, title: 'Prepare the hall', assignedMemberProfileId: 7, assignedMemberDisplayName: 'Test Member', scheduledForUtc: '2026-11-03T01:00:00Z', status: 1 as const };

describe('DutiesComponent', () => {
  it('shows a Member only personal completion controls', () => {
    TestBed.configureTestingModule({ imports: [DutiesComponent], providers: [
      { provide: AuthService, useValue: { currentUser: signal({ id: 'member', email: 'member@example.test', role: 'Member', memberProfileId: 7 }) } },
      { provide: DutiesService, useValue: { list: vi.fn(() => of([duty])), complete: vi.fn(() => of(undefined)) } },
    ] });
    const fixture = TestBed.createComponent(DutiesComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('My duties');
    expect(fixture.nativeElement.textContent).toContain('Mark complete');
    expect(fixture.nativeElement.textContent).not.toContain('Assign duty');
    expect(fixture.nativeElement.textContent).not.toContain('Audit history');
  });

  it('shows Pastor assignment and audit controls', () => {
    TestBed.configureTestingModule({ imports: [DutiesComponent], providers: [
      { provide: AuthService, useValue: { currentUser: signal({ id: 'pastor', email: 'pastor@example.test', role: 'Pastor', memberProfileId: null }) } },
      { provide: DutiesService, useValue: { list: vi.fn(() => of([duty])), assignees: vi.fn(() => of([{ id: 7, displayName: 'Test Member' }])) } },
    ] });
    const fixture = TestBed.createComponent(DutiesComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Duty planning');
    expect(fixture.nativeElement.textContent).toContain('Assign duty');
    expect(fixture.nativeElement.textContent).toContain('Audit history');
    expect(fixture.nativeElement.textContent).not.toContain('Mark complete');
  });
});
