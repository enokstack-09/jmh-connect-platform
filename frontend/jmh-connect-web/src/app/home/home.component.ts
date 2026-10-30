import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { BirthdaysService } from '../birthdays/birthdays.service';
import { AnnouncementsService } from '../announcements/announcements.service';
import { Birthday } from '../birthdays/birthdays.models';
import { Announcement } from '../announcements/announcements.models';
import { DutiesService } from '../duties/duties.service';
import { EventsService } from '../events/events.service';
import { Duty } from '../duties/duties.models';
import { CommunityEvent } from '../events/events.models';

@Component({ selector: 'app-home', imports: [RouterLink, DatePipe], templateUrl: './home.component.html', styleUrl: './home.component.scss' })
export class HomeComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly birthdaysService = inject(BirthdaysService);
  private readonly announcementsService = inject(AnnouncementsService);
  private readonly dutiesService = inject(DutiesService);
  private readonly eventsService = inject(EventsService);
  protected busy = false;
  protected readonly birthdays = signal<Birthday[]>([]);
  protected readonly announcements = signal<Announcement[]>([]);
  protected readonly upcomingDuties = signal<Duty[]>([]);
  protected readonly upcomingEvents = signal<CommunityEvent[]>([]);
  constructor() {
    this.birthdaysService.list().subscribe({ next: value => this.birthdays.set(value.slice(0, 3)) });
    this.announcementsService.list().subscribe({ next: value => this.announcements.set(value.slice(0, 3)) });
    const now = Date.now(); const soon = now + 14 * 86400000;
    if (this.auth.currentUser()?.role !== 'Leader') this.dutiesService.list().subscribe({ next: value => this.upcomingDuties.set(value.filter(item => item.status === 1 && new Date(item.scheduledForUtc).getTime() >= now && new Date(item.scheduledForUtc).getTime() <= soon).slice(0, 3)) });
    this.eventsService.list().subscribe({ next: value => this.upcomingEvents.set(value.filter(item => item.status === 1 && new Date(item.startsUtc).getTime() >= now && new Date(item.startsUtc).getTime() <= soon).slice(0, 3)) });
  }
  protected logout(): void {
    this.busy = true;
    this.auth.logout().subscribe({ next: () => void this.router.navigateByUrl('/login'), error: () => this.busy = false });
  }
}
