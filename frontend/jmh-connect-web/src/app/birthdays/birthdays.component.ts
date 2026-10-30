import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { Birthday, BirthdayManagement } from './birthdays.models';
import { BirthdaysService } from './birthdays.service';
@Component({ selector: 'app-birthdays', imports: [DatePipe], templateUrl: './birthdays.component.html', styleUrl: './birthdays.component.scss' })
export class BirthdaysComponent {
  protected readonly auth = inject(AuthService); private readonly service = inject(BirthdaysService);
  protected readonly birthdays = signal<Birthday[]>([]); protected readonly managed = signal<BirthdayManagement[]>([]); protected readonly error = signal('');
  constructor() { this.reload(); }
  protected reload(): void { this.error.set(''); this.service.list().subscribe({ next: value => this.birthdays.set(value), error: () => this.error.set('Birthday reminders could not be loaded.') }); if (this.auth.currentUser()?.role === 'Pastor') this.service.manage().subscribe({ next: value => this.managed.set(value), error: () => this.error.set('Birthday visibility settings could not be loaded.') }); }
  protected change(item: BirthdayManagement, checked: boolean): void { this.service.setVisibility(item.memberProfileId, checked).subscribe({ next: () => this.reload(), error: () => { this.error.set('Birthday visibility could not be updated.'); this.reload(); } }); }
}
