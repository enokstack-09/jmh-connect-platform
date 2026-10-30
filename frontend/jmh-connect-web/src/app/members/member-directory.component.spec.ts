import { HttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../auth/auth.service';
import { MemberDirectoryComponent } from './member-directory.component';
import { MembersService } from './members.service';

describe('MemberDirectoryComponent', () => {
  it('shows the family directory without Pastor-only controls for a Leader', async () => {
    await TestBed.configureTestingModule({
      imports: [MemberDirectoryComponent],
      providers: [
        { provide: HttpClient, useValue: {} },
        { provide: AuthService, useValue: { currentUser: signal({ id: 'leader', email: 'leader@example.test', role: 'Leader' }) } },
        { provide: MembersService, useValue: { list: vi.fn(() => of([{ id: 1, givenName: 'Nila', familyName: 'Student', homeTown: 'Jaffna', residentType: 1, hasApprovedPhoto: false }])) } },
      ],
    }).compileComponents();

    const fixture: ComponentFixture<MemberDirectoryComponent> = TestBed.createComponent(MemberDirectoryComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Our JMH Family');
    expect(fixture.nativeElement.textContent).toContain('Nila Student');
    expect(fixture.nativeElement.textContent).not.toContain('Add resident');
  });

  it('gives a Pastor the complete resident form and audit history', async () => {
    const member = {
      id: 1, givenName: 'Nila', familyName: 'Student', homeTown: 'Jaffna', residentType: 1 as const,
      dateOfBirth: '2010-03-04', residencyStatus: 1 as const, hasApprovedPhoto: false, canManagePhoto: true,
      studentDetails: { schoolName: 'Jaffna College', educationLevel: 'Grade 10', studentIdentifier: 'ST-01' },
      contacts: [{ id: 7, fullName: 'Guardian One', relationship: 'Parent', phoneNumber: '0770000000',
        isEmergencyContact: true, isGuardian: true, sortOrder: 0 }],
    };
    const service = {
      list: vi.fn(() => of([member])), get: vi.fn(() => of(member)),
      history: vi.fn(() => of([{ id: 9, actionType: 'MemberUpdated', occurredUtc: '2026-10-30T05:00:00Z', actorEmail: 'pastor@example.test' }])),
      photoManagement: vi.fn(() => of({ requiresGuardianConsent: true, activeConsent: null })),
      photoUrl: vi.fn(() => '/api/members/1/photo'),
    };
    await TestBed.configureTestingModule({
      imports: [MemberDirectoryComponent],
      providers: [
        { provide: HttpClient, useValue: {} },
        { provide: AuthService, useValue: { currentUser: signal({ id: 'pastor', email: 'pastor@example.test', role: 'Pastor' }) } },
        { provide: MembersService, useValue: service },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(MemberDirectoryComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    (fixture.nativeElement.querySelector('.member-card') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Audit history');
    expect(fixture.nativeElement.textContent).toContain('Profile details updated');
    expect(fixture.nativeElement.textContent).toContain('Active guardian consent is required');

    (fixture.nativeElement.querySelector('.close') as HTMLButtonElement).click();
    fixture.detectChanges();
    (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[])
      .find(button => button.textContent?.includes('Add resident'))!.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Date of birth');
    expect(fixture.nativeElement.textContent).toContain('Student ID');
    expect(fixture.nativeElement.textContent).toContain('Add contact');
  });

  it('keeps restore success and refreshes the active list without a redundant archive request', async () => {
    const restored = { id: 1, givenName: 'Restored', familyName: 'Resident', homeTown: 'Jaffna', residentType: 1 as const, hasApprovedPhoto: false };
    const service = {
      list: vi.fn(() => of([restored])), archived: vi.fn(() => of([])), restore: vi.fn(() => of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [MemberDirectoryComponent],
      providers: [
        { provide: HttpClient, useValue: {} },
        { provide: AuthService, useValue: { currentUser: signal({ id: 'pastor', email: 'pastor@example.test', role: 'Pastor' }) } },
        { provide: MembersService, useValue: service },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(MemberDirectoryComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance as any;
    component.archivedEntries.set([{ ...restored, archivedUtc: '2026-10-30T08:00:00Z' }]);

    component.restore(1);
    fixture.detectChanges();

    expect(service.restore).toHaveBeenCalledWith(1);
    expect(service.archived).not.toHaveBeenCalled();
    expect(component.archivedEntries()).toEqual([]);
    expect(component.notice()).toContain('restored');
    expect(component.error()).toBeNull();
  });

  it('reports session and post-restore refresh failures with the HTTP context', async () => {
    const service = {
      list: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 401 }))),
      restore: vi.fn(() => of(undefined)), archived: vi.fn(() => of([])),
    };
    await TestBed.configureTestingModule({
      imports: [MemberDirectoryComponent],
      providers: [
        { provide: HttpClient, useValue: {} },
        { provide: AuthService, useValue: { currentUser: signal({ id: 'pastor', email: 'pastor@example.test', role: 'Pastor' }) } },
        { provide: MembersService, useValue: service },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(MemberDirectoryComponent);
    const component = fixture.componentInstance as any;
    component.restore(1);

    expect(component.notice()).toContain('restored');
    expect(component.error()).toContain('resident was restored');
    expect(component.error()).toContain('session has expired');
  });
});
