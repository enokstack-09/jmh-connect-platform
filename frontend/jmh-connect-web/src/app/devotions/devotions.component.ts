import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Observable } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AttendanceRosterItem, AttendanceStatus, DevotionAudit, DevotionMutation, DevotionSession, MyAttendance } from './devotions.models';
import { DevotionsService } from './devotions.service';

@Component({ selector: 'app-devotions', imports: [DatePipe, FormsModule], templateUrl: './devotions.component.html', styleUrl: './devotions.component.scss' })
export class DevotionsComponent {
  private readonly service = inject(DevotionsService);
  protected readonly auth = inject(AuthService);
  protected readonly sessions = signal<DevotionSession[]>([]);
  protected readonly myHistory = signal<MyAttendance[]>([]);
  protected readonly roster = signal<AttendanceRosterItem[]>([]);
  protected readonly audit = signal<DevotionAudit[]>([]);
  protected readonly selected = signal<DevotionSession | null>(null);
  protected readonly isPastor = computed(() => this.auth.currentUser()?.role === 'Pastor');
  protected readonly isMember = computed(() => this.auth.currentUser()?.role === 'Member');
  protected readonly busy = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected editor: (DevotionMutation & { localStart: string }) | null = null;

  constructor() { this.load(); }
  protected statusLabel(status?: AttendanceStatus | null): string { return status === 1 ? 'Present' : status === 2 ? 'Absent' : status === 3 ? 'Excused' : 'Not recorded'; }
  protected beginCreate(): void { this.editor = { title: '', localStart: '', scheduledStartUtc: '', durationMinutes: 45, location: '', notes: '' }; }
  protected beginEdit(session: DevotionSession): void {
    const local = new Date(new Date(session.scheduledStartUtc).getTime() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    this.editor = { title: session.title, localStart: local, scheduledStartUtc: session.scheduledStartUtc, durationMinutes: session.durationMinutes, location: session.location ?? '', notes: session.notes ?? '' };
    this.selected.set(session);
  }
  protected save(form: NgForm): void {
    if (!this.editor || !this.isPastor() || form.invalid || !this.editor.localStart) { form.control.markAllAsTouched(); return; }
    this.busy.set(true); this.clear();
    const request: DevotionMutation = { ...this.editor, scheduledStartUtc: new Date(this.editor.localStart).toISOString() };
    const operation: Observable<unknown> = this.selected() ? this.service.update(this.selected()!.id, request) : this.service.create(request);
    operation.subscribe({ next: () => { this.busy.set(false); this.editor = null; this.selected.set(null); this.notice.set('Devotion session saved.'); this.load(false); }, error: error => this.fail(error) });
  }
  protected cancel(session: DevotionSession): void {
    if (!this.isPastor() || !window.confirm(`Cancel ${session.title}?`)) return;
    this.busy.set(true); this.service.cancel(session.id).subscribe({ next: () => { this.busy.set(false); this.notice.set('Devotion session cancelled.'); this.load(false); }, error: error => this.fail(error) });
  }
  protected manageAttendance(session: DevotionSession): void {
    this.selected.set(session); this.busy.set(true); this.clear();
    this.service.attendance(session.id).subscribe({ next: roster => { this.roster.set(roster); this.busy.set(false); this.loadAudit(session.id); }, error: error => this.fail(error) });
  }
  protected setAttendance(item: AttendanceRosterItem, value: string): void { item.status = Number(value) as AttendanceStatus; this.roster.set([...this.roster()]); }
  protected saveAttendance(): void {
    const session = this.selected(); if (!session || !this.isPastor()) return;
    const entries = this.roster();
    this.busy.set(true); this.service.saveAttendance(session.id, entries).subscribe({ next: () => { this.busy.set(false); this.notice.set('Attendance saved.'); this.closePanel(); this.load(false); }, error: error => this.fail(error) });
  }
  protected closePanel(): void { this.selected.set(null); this.roster.set([]); this.audit.set([]); this.editor = null; }
  private load(showLoading = true): void {
    if (showLoading) this.loading.set(true); this.error.set(null);
    this.service.list().subscribe({ next: sessions => { this.sessions.set(sessions); this.loading.set(false); if (this.isMember() && this.auth.currentUser()?.memberProfileId) this.loadMine(); }, error: error => { this.loading.set(false); this.fail(error); } });
  }
  private loadMine(): void { this.service.myAttendance().subscribe({ next: history => this.myHistory.set(history), error: error => this.fail(error) }); }
  private loadAudit(id: number): void { this.service.audit(id).subscribe({ next: audit => this.audit.set(audit), error: error => this.fail(error) }); }
  private clear(): void { this.error.set(null); this.notice.set(null); }
  private fail(error: HttpErrorResponse): void {
    this.busy.set(false); const details = error.error?.errors as Record<string, string[]> | undefined;
    this.error.set(details ? Object.values(details).flat()[0] : error.error?.message || (error.status === 401
      ? 'Your session expired. Sign in again.'
      : error.status === 403
        ? 'You do not have permission for that devotion action.'
        : error.status === 404
          ? 'The Devotions API is not available in the running backend. Restart the API from the current build.'
          : `The devotion request failed${error.status ? ` (HTTP ${error.status})` : ''}.`));
  }
}
