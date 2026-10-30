import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MemberDetails, PhotoManagement, SelfProfileMutation } from './members.models';
import { MembersService } from './members.service';

@Component({ selector: 'app-my-profile', imports: [DatePipe, FormsModule], templateUrl: './my-profile.component.html', styleUrls: ['./my-profile.component.scss', './my-profile-contrast.scss'] })
export class MyProfileComponent {
  private readonly members = inject(MembersService);
  protected readonly profile = signal<MemberDetails | null>(null);
  protected readonly photoManagement = signal<PhotoManagement | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly photoVersion = signal(0);
  protected editor: SelfProfileMutation | null = null;
  protected photoFile: File | null = null;

  constructor() { this.load(); }

  protected displayName(): string {
    const profile = this.profile();
    return profile?.preferredName?.trim() || `${profile?.givenName ?? ''} ${profile?.familyName ?? ''}`.trim();
  }
  protected photoUrl(): string { return this.members.photoUrl(this.profile()!.id, this.photoVersion()); }
  protected isGuardian(id?: number): boolean { return !!id && !!this.profile()?.contacts?.find(contact => contact.id === id)?.isGuardian; }
  protected addContact(): void { this.editor?.contacts.push({ fullName: '', relationship: '', phoneNumber: '', email: '', isEmergencyContact: false, sortOrder: this.editor.contacts.length }); }
  protected removeContact(index: number): void {
    if (!this.editor || this.isGuardian(this.editor.contacts[index]?.id)) return;
    this.editor.contacts.splice(index, 1); this.editor.contacts.forEach((contact, order) => contact.sortOrder = order);
  }
  protected save(form: NgForm): void {
    if (!this.editor || form.invalid || this.editor.contacts.some(contact => !contact.fullName.trim() || !contact.relationship.trim() || !(contact.phoneNumber?.trim() || contact.email?.trim()))) {
      form.control.markAllAsTouched(); this.error.set('Complete each contact and provide a phone number or email.'); return;
    }
    this.saving.set(true); this.clearMessages();
    this.members.updateMyProfile({ ...this.editor, contacts: this.editor.contacts.map((contact, sortOrder) => ({ ...contact, sortOrder })) }).subscribe({
      next: () => { this.saving.set(false); this.notice.set('Your profile details were updated.'); this.load(false); },
      error: error => { this.saving.set(false); this.handleError(error, 'profileUpdate'); },
    });
  }
  protected selectPhoto(event: Event): void { this.photoFile = (event.target as HTMLInputElement).files?.[0] ?? null; }
  protected uploadPhoto(): void {
    const profile = this.profile(); if (!profile || !this.photoFile) return;
    if (this.photoFile.size > 5 * 1024 * 1024) { this.error.set('Choose an image no larger than 5 MB.'); return; }
    this.saving.set(true); this.clearMessages();
    this.members.uploadPhoto(profile.id, this.photoFile).subscribe({
      next: () => { this.photoFile = null; this.saving.set(false); this.notice.set('Photo submitted securely for Pastor approval.'); this.loadPhoto(profile.id); },
      error: error => { this.saving.set(false); this.handleError(error, 'photoUpload'); },
    });
  }
  private load(showLoading = true): void {
    if (showLoading) this.loading.set(true);
    this.members.getMyProfile().subscribe({
      next: profile => {
        this.profile.set(profile);
        this.editor = { preferredName: profile.preferredName ?? '', contacts: (profile.contacts ?? []).map(contact => ({ id: contact.id, fullName: contact.fullName, relationship: contact.relationship, phoneNumber: contact.phoneNumber, email: contact.email, isEmergencyContact: contact.isEmergencyContact, sortOrder: contact.sortOrder })) };
        this.loading.set(false); this.loadPhoto(profile.id);
      },
      error: error => { this.loading.set(false); this.handleError(error, 'profileLoad'); },
    });
  }
  private loadPhoto(id: number): void { this.members.photoManagement(id).subscribe({ next: state => this.photoManagement.set(state), error: error => this.handleError(error, 'photoLoad') }); }
  private clearMessages(): void { this.error.set(null); this.notice.set(null); }
  private handleError(
    error: HttpErrorResponse,
    context: 'profileLoad' | 'profileUpdate' | 'photoLoad' | 'photoUpload'): void {
    const details = error.error?.errors as Record<string, string[]> | undefined;
    const serverMessage = details ? Object.values(details).flat()[0] : error.error?.message;
    if (serverMessage) { this.error.set(serverMessage); return; }
    if (context === 'profileLoad' && error.status === 404) {
      this.error.set('No member profile is linked to this account. Ask a Pastor to link the correct resident profile.');
      return;
    }
    if (context === 'photoLoad') {
      this.error.set(error.status === 401
        ? 'Your session expired while loading photo controls. Sign in again.'
        : `Your profile loaded, but photo controls are unavailable${error.status ? ` (HTTP ${error.status})` : ''}.`);
      return;
    }
    const messages: Record<number, string> = {
      0: 'The JMH API could not be reached. Check that the local API is running, then try again.',
      400: 'The request could not be verified. Refresh the page, sign in again, and retry.',
      401: 'Your session has expired. Sign in again before continuing.',
      403: 'Your linked profile is not available for self-service. Ask a Pastor for help.',
      404: 'The linked profile or photo resource could not be found. Refresh and try again.',
      409: 'Your profile changed before the update completed. Refresh and try again.',
    };
    this.error.set(messages[error.status] ?? `Your profile request failed with HTTP ${error.status}. Please try again.`);
  }
}
