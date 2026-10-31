import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

interface Feature {
  title: string;
  text: string;
  icon: string;
  tone: 'mint' | 'blue' | 'soft';
}

@Component({
  selector: 'app-landing',
  imports: [RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent {
  protected readonly auth = inject(AuthService);
  protected readonly year = new Date().getFullYear();

  protected readonly features: Feature[] = [
    { title: 'Family Directory', text: 'A private, welcoming directory of our boarding community.', tone: 'blue', icon: 'M9 11a3.2 3.2 0 100-6.4A3.2 3.2 0 009 11zM17 11.5a2.6 2.6 0 100-5.2 2.6 2.6 0 000 5.2zM3 20c0-3.4 2.7-6 6-6s6 2.6 6 6M15 14.4c3 .2 6 1.8 6 5.6' },
    { title: 'Devotions & Attendance', text: 'Schedule devotions and keep a clear, private record of participation.', tone: 'mint', icon: 'M4 5.5C4 4.7 4.7 4 5.5 4H12v16H5.5C4.7 20 4 19.3 4 18.5zM20 5.5c0-.8-.7-1.5-1.5-1.5H12v16h6.5c.8 0 1.5-.7 1.5-1.5z' },
    { title: 'Duties', text: 'Assign responsibilities and follow them through to completion.', tone: 'soft', icon: 'M6.5 4h11A2.5 2.5 0 0120 6.5v11a2.5 2.5 0 01-2.5 2.5h-11A2.5 2.5 0 014 17.5v-11A2.5 2.5 0 016.5 4zM8.5 12.5l2.3 2.3 4.7-4.8' },
    { title: 'Community Events', text: 'One shared calendar for gatherings, activities and special moments.', tone: 'blue', icon: 'M6 5h12a2.5 2.5 0 012.5 2.5v11A2.5 2.5 0 0118 21H6a2.5 2.5 0 01-2.5-2.5v-11A2.5 2.5 0 016 5zM3.5 10h17M8 3v4M16 3v4' },
    { title: 'Birthdays', text: 'Gentle reminders for the birthdays our family chooses to share.', tone: 'mint', icon: 'M4 11h16v9H4zM12 11v9M12 11c-3 0-4.5-1.2-4.5-2.7S9 6 10.3 6C11.6 6 12 8 12 11zM12 11c3 0 4.5-1.2 4.5-2.7S15 6 13.7 6C12.4 6 12 8 12 11z' },
    { title: 'Announcements', text: 'Important updates delivered to the people they are meant for.', tone: 'soft', icon: 'M4 10v4l3 .5L14 19V5L7 9.5zM17 9a4 4 0 010 6' },
  ];

  constructor() {
    // Quietly detect an existing session so the header can offer the dashboard.
    this.auth.loadCurrentUser().subscribe({ error: () => undefined });
  }
}
