import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it, vi } from 'vitest';
import { MembersService } from './members.service';
import { SelfProfileMutation } from './members.models';
import { MyProfileComponent } from './my-profile.component';

describe('MyProfileComponent', () => {
  function create() {
    const members = {
      getMyProfile: vi.fn(() => of({
        id: 7, givenName: 'Official', familyName: 'Member', preferredName: 'Mimi', homeTown: 'Jaffna',
        dateOfBirth: '2010-03-04', residentType: 1, residencyStatus: 1, hasApprovedPhoto: false, canManagePhoto: true,
        contacts: [{ id: 4, fullName: 'Guardian One', relationship: 'Parent', phoneNumber: '0770000000', email: null, isEmergencyContact: true, isGuardian: true, sortOrder: 0 }],
      })),
      updateMyProfile: vi.fn((_request: SelfProfileMutation) => of(undefined)),
      photoManagement: vi.fn(() => of({ requiresGuardianConsent: true, activeConsent: null, pendingPhoto: null })),
      photoUrl: vi.fn(() => '/api/members/7/photo'),
      uploadPhoto: vi.fn(() => of({ id: 1, approvalStatus: 1, createdUtc: '2026-10-30T00:00:00Z', isCurrent: false })),
    };
    TestBed.configureTestingModule({ imports: [MyProfileComponent], providers: [{ provide: MembersService, useValue: members }] });
    const fixture = TestBed.createComponent(MyProfileComponent);
    fixture.detectChanges();
    return { fixture, members };
  }

  it('shows official fields read-only and protects guardian identity in the UI', () => {
    const { fixture } = create();
    expect(fixture.nativeElement.textContent).toContain('Official details remain Pastor-managed');
    expect(fixture.nativeElement.textContent).toContain('Guardian');
    expect(fixture.nativeElement.querySelector('.contact-editor input').readOnly).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('record guardian consent');
  });

  it('sends only the self-service profile contract', () => {
    const { fixture, members } = create();
    const component = fixture.componentInstance as any;
    component.editor.preferredName = 'New Preferred';
    component.save({ invalid: false, control: { markAllAsTouched: vi.fn() } });
    const request = members.updateMyProfile.mock.calls[0]![0] as SelfProfileMutation & Record<string, unknown>;
    expect(request.preferredName).toBe('New Preferred');
    expect(request['givenName']).toBeUndefined();
    expect(request['homeTown']).toBeUndefined();
    expect((request.contacts[0] as unknown as Record<string, unknown>)['isGuardian']).toBeUndefined();
  });

  it('shows a clear missing-link error without rendering another profile', () => {
    const members = {
      getMyProfile: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 404 }))),
    };
    TestBed.configureTestingModule({ imports: [MyProfileComponent], providers: [{ provide: MembersService, useValue: members }] });
    const fixture = TestBed.createComponent(MyProfileComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No member profile is linked');
    expect(fixture.nativeElement.querySelector('.profile-card')).toBeNull();
  });

  it('does not misreport a photo-control 404 as a missing profile link', () => {
    const members = {
      getMyProfile: vi.fn(() => of({ id: 7, givenName: 'Linked', familyName: 'Member', homeTown: 'Jaffna', residentType: 1, residencyStatus: 1, hasApprovedPhoto: false, contacts: [] })),
      photoManagement: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 404 }))),
      photoUrl: vi.fn(() => '/api/members/7/photo'),
    };
    TestBed.configureTestingModule({ imports: [MyProfileComponent], providers: [{ provide: MembersService, useValue: members }] });
    const fixture = TestBed.createComponent(MyProfileComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.profile-card')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('photo controls are unavailable');
    expect(fixture.nativeElement.textContent).not.toContain('No member profile is linked');
  });

  it('shows readable placeholders for missing official card values', () => {
    const members = {
      getMyProfile: vi.fn(() => of({ id: 7, givenName: '', familyName: '', homeTown: null, dateOfBirth: null, residentType: null, residencyStatus: 1, hasApprovedPhoto: false, contacts: [] })),
      photoManagement: vi.fn(() => of({ requiresGuardianConsent: true, activeConsent: null })),
      photoUrl: vi.fn(() => '/api/members/7/photo'),
    };
    TestBed.configureTestingModule({ imports: [MyProfileComponent], providers: [{ provide: MembersService, useValue: members }] });
    const fixture = TestBed.createComponent(MyProfileComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Name not recorded');
    expect(fixture.nativeElement.textContent).toContain('Official name not recorded');
    expect(fixture.nativeElement.textContent).toContain('Resident type not recorded');
    expect(fixture.nativeElement.textContent.match(/Not recorded/g)?.length).toBeGreaterThanOrEqual(2);
  });
});
