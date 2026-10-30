import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Observable } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { CommunityEvent, CommunityEventAudit, CommunityEventMutation } from './events.models';
import { EventsService } from './events.service';

interface CalendarDay { date: Date; inMonth: boolean; events: CommunityEvent[]; }

@Component({ selector: 'app-events', imports: [DatePipe, FormsModule], templateUrl: './events.component.html', styleUrl: './events.component.scss' })
export class EventsComponent {
  private readonly service = inject(EventsService);
  protected readonly auth = inject(AuthService);
  protected readonly events = signal<CommunityEvent[]>([]);
  protected readonly audit = signal<CommunityEventAudit[]>([]);
  protected readonly selected = signal<CommunityEvent | null>(null);
  protected readonly view = signal<'list' | 'calendar'>('list');
  protected readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  protected readonly isPastor = computed(() => this.auth.currentUser()?.role === 'Pastor');
  protected readonly calendarDays = computed<CalendarDay[]>(() => {
    const month = this.month(); const first = new Date(month.getFullYear(), month.getMonth(), 1); const start = new Date(first); start.setDate(1 - first.getDay());
    return Array.from({ length: 42 }, (_, offset) => { const date = new Date(start); date.setDate(start.getDate() + offset); return { date, inMonth: date.getMonth() === month.getMonth(), events: this.events().filter(item => { const eventDate = new Date(item.startsUtc); return eventDate.getFullYear() === date.getFullYear() && eventDate.getMonth() === date.getMonth() && eventDate.getDate() === date.getDate(); }) }; });
  });
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected editor: (CommunityEventMutation & { localStart: string; localEnd: string }) | null = null;

  constructor() { this.load(); }
  protected beginCreate(): void { this.selected.set(null); this.editor = { title: '', description: '', location: '', startsUtc: '', endsUtc: '', localStart: '', localEnd: '' }; }
  protected beginEdit(item: CommunityEvent): void { this.selected.set(item); this.editor = { title: item.title, description: item.description ?? '', location: item.location ?? '', startsUtc: item.startsUtc, endsUtc: item.endsUtc, localStart: this.toLocal(item.startsUtc), localEnd: this.toLocal(item.endsUtc) }; }
  protected save(form: NgForm): void {
    if (!this.editor || !this.isPastor() || form.invalid || !this.editor.localStart || !this.editor.localEnd) { form.control.markAllAsTouched(); return; }
    const request: CommunityEventMutation = { title: this.editor.title, description: this.editor.description, location: this.editor.location, startsUtc: new Date(this.editor.localStart).toISOString(), endsUtc: new Date(this.editor.localEnd).toISOString() };
    if (new Date(request.endsUtc) <= new Date(request.startsUtc)) { this.error.set('The event end must be after its start.'); return; }
    this.busy.set(true); this.clear(); const operation: Observable<unknown> = this.selected() ? this.service.update(this.selected()!.id, request) : this.service.create(request);
    operation.subscribe({ next: () => { this.busy.set(false); this.closePanel(); this.notice.set('Community event saved.'); this.load(false); }, error: error => this.fail(error) });
  }
  protected cancel(item: CommunityEvent): void { if (!this.isPastor() || !window.confirm(`Cancel “${item.title}”?`)) return; this.busy.set(true); this.service.cancel(item.id).subscribe({ next: () => { this.busy.set(false); this.notice.set('Community event cancelled.'); this.load(false); }, error: error => this.fail(error) }); }
  protected showAudit(item: CommunityEvent): void { this.selected.set(item); this.service.audit(item.id).subscribe({ next: entries => this.audit.set(entries), error: error => this.fail(error) }); }
  protected shiftMonth(change: number): void { const value = this.month(); this.month.set(new Date(value.getFullYear(), value.getMonth() + change, 1)); }
  protected closePanel(): void { this.editor = null; this.selected.set(null); this.audit.set([]); }
  private load(showLoading = true): void { if (showLoading) this.loading.set(true); this.error.set(null); this.service.list().subscribe({ next: events => { this.events.set(events); this.loading.set(false); }, error: error => { this.loading.set(false); this.fail(error); } }); }
  private toLocal(value: string): string { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); }
  private clear(): void { this.error.set(null); this.notice.set(null); }
  private fail(error: HttpErrorResponse): void { this.busy.set(false); const details = error.error?.errors as Record<string, string[]> | undefined; this.error.set(details ? Object.values(details).flat()[0] : error.error?.message || (error.status === 403 ? 'You do not have permission for that event action.' : `The event request failed${error.status ? ` (HTTP ${error.status})` : ''}.`)); }
}
