import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Observable } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import {
  ArchivedMemberSummary, MemberDetails, MemberHistoryEntry, MemberMutation,
  MemberSummary, PhotoManagement, ResidentType,
} from './members.models';
import { MembersService } from './members.service';

@Component({
  selector: 'app-member-directory',
  imports: [DatePipe, FormsModule],
  templateUrl: './member-directory.component.html',
  styleUrl: './member-directory.component.scss',
})
export class MemberDirectoryComponent {
  private readonly members = inject(MembersService);
  protected readonly auth = inject(AuthService);
  protected readonly entries = signal<MemberSummary[]>([]);
  protected readonly archivedEntries = signal<ArchivedMemberSummary[]>([]);
  protected readonly selected = signal<MemberDetails | null>(null);
  protected readonly history = signal<MemberHistoryEntry[]>([]);
  protected readonly photoManagement = signal<PhotoManagement | null>(null);
  protected readonly showingArchive = signal(false);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly filter = signal<'All' | 'Student' | 'Worker'>('All');
  protected readonly photoVersion = signal(0);
  protected readonly filters: Array<'All' | 'Student' | 'Worker'> = ['All', 'Student', 'Worker'];
  protected readonly isPastor = computed(() => this.auth.currentUser()?.role === 'Pastor');
  protected readonly guardianContacts = computed(() =>
    (this.selected()?.contacts ?? []).filter(contact => contact.isGuardian && contact.id));
  protected readonly maximumDate = new Date().toISOString().slice(0, 10);
  protected search = '';
  protected editor: MemberMutation | null = null;
  protected editingId: number | null = null;
  protected photoFile: File | null = null;
  protected guardianContactId: number | null = null;
  protected consentEvidence = '';
  protected consentNotes = '';

  constructor() { this.load(); }

  protected displayName(member: MemberSummary): string {
    return member.preferredName?.trim() || `${member.givenName} ${member.familyName}`;
  }

  protected typeLabel(type?: ResidentType | null): string { return type === 1 ? 'Student' : 'Worker'; }
  protected photoUrl(member: MemberSummary): string { return this.members.photoUrl(member.id, this.photoVersion()); }
  protected actionLabel(action: string): string {
    const labels: Record<string, string> = {
      MemberCreated: 'Resident profile created', MemberUpdated: 'Profile details updated',
      MemberSelfUpdated: 'Member updated their profile',
      MemberArchived: 'Resident archived', MemberRestored: 'Resident restored',
      PhotoSubmitted: 'New profile photo submitted', PhotoApproved: 'Profile photo approved',
      PhotoRejected: 'Profile photo rejected', PhotoConsentGranted: 'Guardian photo consent recorded',
      PhotoConsentWithdrawn: 'Guardian photo consent withdrawn',
    };
    return labels[action] ?? action;
  }

  protected setFilter(value: 'All' | 'Student' | 'Worker'): void { this.filter.set(value); this.load(); }
  protected searchDirectory(): void { this.load(); }

  protected showMember(id: number, archived = false): void {
    this.clearMessages();
    (archived ? this.members.archivedDetail(id) : this.members.get(id)).subscribe({
      next: detail => {
        this.showingArchive.set(false);
        this.selected.set(detail);
        this.history.set([]);
        this.photoManagement.set(null);
        this.guardianContactId = this.guardianContacts()[0]?.id ?? null;
        this.loadProfileTools(detail);
      },
      error: error => this.handleError(error),
    });
  }

  protected beginCreate(): void {
    this.clearMessages();
    this.editingId = null;
    this.editor = {
      givenName: '', familyName: '', preferredName: '', homeTown: '', dateOfBirth: '',
      residentType: 'Student', studentDetails: {}, contacts: [],
    };
  }

  protected beginEdit(): void {
    const member = this.selected();
    if (!member) return;
    this.clearMessages();
    this.editingId = member.id;
    this.editor = {
      givenName: member.givenName, familyName: member.familyName, preferredName: member.preferredName ?? '',
      homeTown: member.homeTown ?? '', dateOfBirth: member.dateOfBirth ?? '',
      residentType: this.typeLabel(member.residentType) as 'Student' | 'Worker',
      studentDetails: member.studentDetails ? { ...member.studentDetails } : undefined,
      workerDetails: member.workerDetails ? { ...member.workerDetails } : undefined,
      contacts: (member.contacts ?? []).map(contact => ({ ...contact })),
    };
    this.selected.set(null);
  }

  protected addContact(): void {
    if (!this.editor) return;
    this.editor.contacts ??= [];
    this.editor.contacts.push({
      fullName: '', relationship: '', phoneNumber: '', email: '',
      isEmergencyContact: false, isGuardian: false, sortOrder: this.editor.contacts.length,
    });
  }

  protected removeContact(index: number): void {
    this.editor?.contacts?.splice(index, 1);
    this.editor?.contacts?.forEach((contact, order) => contact.sortOrder = order);
  }

  protected save(form: NgForm): void {
    if (!this.editor || !this.isPastor()) return;
    if (form.invalid || !this.contactsValid()) {
      form.control.markAllAsTouched();
      this.error.set('Please complete the highlighted fields. Every contact needs a phone number or email.');
      return;
    }
    this.submitting.set(true);
    this.clearMessages();
    const request = this.normalizedEditor();
    const operation: Observable<unknown> = this.editingId === null
      ? this.members.create(request)
      : this.members.update(this.editingId, request);
    operation.subscribe({
      next: () => {
        this.editor = null; this.editingId = null; this.submitting.set(false);
        this.notice.set('Resident profile saved.'); this.load();
      },
      error: error => { this.submitting.set(false); this.handleError(error); },
    });
  }

  protected archive(): void {
    const member = this.selected();
    if (!member || !this.isPastor() ||
        !window.confirm(`Archive ${this.displayName(member)}? Their profile will move to Pastor-only history.`)) return;
    this.submitting.set(true);
    this.members.archive(member.id).subscribe({
      next: () => { this.selected.set(null); this.submitting.set(false); this.notice.set('Resident moved to archive history.'); this.load(); },
      error: error => { this.submitting.set(false); this.handleError(error); },
    });
  }

  protected restore(id: number): void {
    if (!this.isPastor()) return;
    this.submitting.set(true);
    this.members.restore(id).subscribe({
      next: () => {
        this.submitting.set(false); this.selected.set(null);
        this.archivedEntries.update(entries => entries.filter(member => member.id !== id));
        this.notice.set('Resident restored to the active family directory.');
        this.showingArchive.set(true); this.load('restoreRefresh');
      },
      error: error => { this.submitting.set(false); this.handleError(error, 'restore'); },
    });
  }

  protected showArchived(): void { this.clearMessages(); this.showingArchive.set(true); this.loadArchived(); }
  protected closePanels(): void {
    this.selected.set(null); this.editor = null; this.editingId = null; this.photoFile = null;
    this.history.set([]); this.photoManagement.set(null);
  }

  protected onResidentTypeChanged(type: 'Student' | 'Worker'): void {
    if (!this.editor) return;
    this.editor.residentType = type;
    if (type === 'Student') { this.editor.studentDetails ??= {}; this.editor.workerDetails = undefined; }
    else { this.editor.workerDetails ??= {}; this.editor.studentDetails = undefined; }
  }

  protected selectPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.photoFile = input.files?.[0] ?? null;
  }

  protected uploadPhoto(): void {
    const member = this.selected();
    if (!member || !this.photoFile) return;
    if (this.photoFile.size > 5 * 1024 * 1024) { this.error.set('Choose an image no larger than 5 MB.'); return; }
    this.submitting.set(true); this.clearMessages();
    this.members.uploadPhoto(member.id, this.photoFile).subscribe({
      next: () => {
        this.photoFile = null; this.submitting.set(false);
        this.notice.set('Photo submitted securely. A Pastor must approve it before it becomes visible.');
        this.loadPhotoManagement(member.id);
      },
      error: error => { this.submitting.set(false); this.handleError(error); },
    });
  }

  protected approvePhoto(): void { this.reviewPhoto(true); }
  protected rejectPhoto(): void { this.reviewPhoto(false); }

  protected grantConsent(): void {
    const member = this.selected();
    if (!member || !this.guardianContactId || !this.isPastor()) return;
    this.submitting.set(true); this.clearMessages();
    this.members.grantPhotoConsent(member.id, {
      guardianContactId: this.guardianContactId,
      evidenceReference: this.consentEvidence.trim() || undefined,
      notes: this.consentNotes.trim() || undefined,
    }).subscribe({
      next: () => { this.submitting.set(false); this.notice.set('Guardian photo consent recorded.'); this.loadProfileTools(member); },
      error: error => { this.submitting.set(false); this.handleError(error); },
    });
  }

  protected withdrawConsent(): void {
    const member = this.selected();
    if (!member || !this.isPastor() || !window.confirm('Withdraw the active guardian photo consent?')) return;
    this.submitting.set(true); this.clearMessages();
    this.members.withdrawPhotoConsent(member.id).subscribe({
      next: () => {
        this.submitting.set(false); this.notice.set('Guardian photo consent withdrawn. The approved photo is no longer visible.');
        const updated = { ...member, hasApprovedPhoto: false };
        this.selected.set(updated); this.photoVersion.update(value => value + 1); this.loadProfileTools(updated); this.load();
      },
      error: error => { this.submitting.set(false); this.handleError(error); },
    });
  }

  private reviewPhoto(approve: boolean): void {
    const member = this.selected();
    const pending = this.photoManagement()?.pendingPhoto;
    if (!member || !pending || !this.isPastor()) return;
    this.submitting.set(true); this.clearMessages();
    const operation = approve ? this.members.approvePhoto(member.id, pending.id) : this.members.rejectPhoto(member.id, pending.id);
    operation.subscribe({
      next: () => {
        this.submitting.set(false); this.notice.set(approve ? 'Profile photo approved.' : 'Profile photo rejected.');
        this.photoVersion.update(value => value + 1); this.showMember(member.id, member.residencyStatus === 2); this.load();
      },
      error: error => { this.submitting.set(false); this.handleError(error); },
    });
  }

  private load(context: 'directory' | 'restoreRefresh' = 'directory'): void {
    this.loading.set(true); this.error.set(null);
    const activeFilter = this.filter();
    const residentType: 'Student' | 'Worker' | undefined = activeFilter === 'All' ? undefined : activeFilter;
    this.members.list({ residentType, search: this.search }).subscribe({
      next: entries => { this.entries.set(entries); this.loading.set(false); },
      error: error => { this.loading.set(false); this.handleError(error, context); },
    });
  }

  private loadArchived(): void {
    this.members.archived().subscribe({ next: entries => this.archivedEntries.set(entries), error: error => this.handleError(error) });
  }

  private loadProfileTools(member: MemberDetails): void {
    if (this.isPastor()) {
      this.members.history(member.id).subscribe({ next: history => this.history.set(history), error: error => this.handleError(error) });
    }
    if (member.canManagePhoto || this.isPastor()) this.loadPhotoManagement(member.id);
  }

  private loadPhotoManagement(id: number): void {
    this.members.photoManagement(id).subscribe({ next: state => this.photoManagement.set(state), error: error => this.handleError(error) });
  }

  private contactsValid(): boolean {
    return (this.editor?.contacts ?? []).every(contact =>
      !!contact.fullName.trim() && !!contact.relationship.trim() && !!(contact.phoneNumber?.trim() || contact.email?.trim()));
  }

  private normalizedEditor(): MemberMutation {
    const value = this.editor!;
    const contacts = (value.contacts ?? []).map((contact, sortOrder) => ({ ...contact, sortOrder }));
    return value.residentType === 'Student'
      ? { ...value, contacts, workerDetails: undefined, studentDetails: value.studentDetails ?? {} }
      : { ...value, contacts, studentDetails: undefined, workerDetails: value.workerDetails ?? {} };
  }

  private clearMessages(): void { this.error.set(null); this.notice.set(null); }

  private handleError(
    error: HttpErrorResponse,
    context: 'directory' | 'restore' | 'restoreRefresh' = 'directory'): void {
    const details = error.error?.errors as Record<string, string[]> | undefined;
    const serverMessage = details ? Object.values(details).flat()[0] : error.error?.message;
    if (serverMessage) { this.error.set(serverMessage); return; }

    const statusMessages: Record<number, string> = {
      0: 'The JMH API could not be reached. Check that the local API is running, then try again.',
      400: 'The request could not be verified. Refresh the page, sign in again, and retry.',
      401: 'Your session has expired. Sign in again before continuing.',
      403: 'You do not have permission for that member action.',
      404: 'The requested resident profile could not be found.',
      409: 'The resident profile changed before this action completed. Refresh and try again.',
    };
    const statusMessage = statusMessages[error.status] ?? `The request failed with HTTP ${error.status}. Please try again.`;
    this.error.set(context === 'restoreRefresh'
      ? `The resident was restored, but the active directory could not be refreshed. ${statusMessage}`
      : context === 'restore'
        ? `The resident was not restored. ${statusMessage}`
        : statusMessage);
  }
}
