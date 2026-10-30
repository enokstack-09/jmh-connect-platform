import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Observable } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Duty, DutyAssignee, DutyAudit, DutyMutation, DutyStatus } from './duties.models';
import { DutiesService } from './duties.service';

@Component({ selector: 'app-duties', imports: [DatePipe, FormsModule], templateUrl: './duties.component.html', styleUrl: './duties.component.scss' })
export class DutiesComponent {
  private readonly service = inject(DutiesService);
  protected readonly auth = inject(AuthService);
  protected readonly duties = signal<Duty[]>([]);
  protected readonly assignees = signal<DutyAssignee[]>([]);
  protected readonly audit = signal<DutyAudit[]>([]);
  protected readonly selected = signal<Duty | null>(null);
  protected readonly filter = signal<'all' | DutyStatus>('all');
  protected readonly visibleDuties = computed(() => this.filter() === 'all' ? this.duties() : this.duties().filter(duty => duty.status === this.filter()));
  protected readonly isPastor = computed(() => this.auth.currentUser()?.role === 'Pastor');
  protected readonly isMember = computed(() => this.auth.currentUser()?.role === 'Member');
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected editor: (DutyMutation & { localScheduled: string; localDue: string }) | null = null;

  constructor() { this.load(); if (this.isPastor()) this.loadAssignees(); }
  protected statusLabel(status: DutyStatus): string { return status === 1 ? 'Assigned' : status === 2 ? 'Completed' : 'Cancelled'; }
  protected beginCreate(): void { this.selected.set(null); this.editor = { title: '', description: '', assignedMemberProfileId: 0, scheduledForUtc: '', dueUtc: null, localScheduled: '', localDue: '' }; }
  protected beginEdit(duty: Duty): void { this.selected.set(duty); this.editor = { title: duty.title, description: duty.description ?? '', assignedMemberProfileId: duty.assignedMemberProfileId, scheduledForUtc: duty.scheduledForUtc, dueUtc: duty.dueUtc, localScheduled: this.toLocal(duty.scheduledForUtc), localDue: duty.dueUtc ? this.toLocal(duty.dueUtc) : '' }; }
  protected save(form: NgForm): void {
    if (!this.editor || !this.isPastor() || form.invalid || !this.editor.localScheduled) { form.control.markAllAsTouched(); return; }
    const request: DutyMutation = { title: this.editor.title, description: this.editor.description, assignedMemberProfileId: Number(this.editor.assignedMemberProfileId), scheduledForUtc: new Date(this.editor.localScheduled).toISOString(), dueUtc: this.editor.localDue ? new Date(this.editor.localDue).toISOString() : null };
    if (request.dueUtc && new Date(request.dueUtc) < new Date(request.scheduledForUtc)) { this.error.set('The due time cannot be before the scheduled time.'); return; }
    this.busy.set(true); this.clear();
    const operation: Observable<unknown> = this.selected() ? this.service.update(this.selected()!.id, request) : this.service.create(request);
    operation.subscribe({ next: () => { this.busy.set(false); this.closePanel(); this.notice.set('Duty assignment saved.'); this.load(false); }, error: error => this.fail(error) });
  }
  protected complete(duty: Duty): void { if (!this.isMember() || !window.confirm(`Mark “${duty.title}” complete?`)) return; this.busy.set(true); this.service.complete(duty.id).subscribe({ next: () => { this.busy.set(false); this.notice.set('Duty marked complete.'); this.load(false); }, error: error => this.fail(error) }); }
  protected cancel(duty: Duty): void { if (!this.isPastor() || !window.confirm(`Cancel “${duty.title}”?`)) return; this.busy.set(true); this.service.cancel(duty.id).subscribe({ next: () => { this.busy.set(false); this.notice.set('Duty cancelled.'); this.load(false); }, error: error => this.fail(error) }); }
  protected showAudit(duty: Duty): void { this.selected.set(duty); this.busy.set(true); this.service.audit(duty.id).subscribe({ next: entries => { this.audit.set(entries); this.busy.set(false); }, error: error => this.fail(error) }); }
  protected closePanel(): void { this.editor = null; this.selected.set(null); this.audit.set([]); }
  private load(showLoading = true): void { if (showLoading) this.loading.set(true); this.error.set(null); this.service.list().subscribe({ next: duties => { this.duties.set(duties); this.loading.set(false); }, error: error => { this.loading.set(false); this.fail(error); } }); }
  private loadAssignees(): void { this.service.assignees().subscribe({ next: assignees => this.assignees.set(assignees), error: error => this.fail(error) }); }
  private toLocal(value: string): string { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); }
  private clear(): void { this.error.set(null); this.notice.set(null); }
  private fail(error: HttpErrorResponse): void { this.busy.set(false); const details = error.error?.errors as Record<string, string[]> | undefined; this.error.set(details ? Object.values(details).flat()[0] : error.error?.message || (error.status === 403 ? 'You do not have permission for that duty action.' : `The duty request failed${error.status ? ` (HTTP ${error.status})` : ''}.`)); }
}
