import { Component, HostListener, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from './auth/auth.service';

interface NavItem {
  label: string;
  path: string;
  icon: string;
}

const PUBLIC_PATHS = new Set(['/', '/login', '/account/setup']);

const ICONS = {
  dashboard: 'M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z',
  profile: 'M12 12a4 4 0 100-8 4 4 0 000 8zM4 21c0-4 3.6-7 8-7s8 3 8 7',
  directory: 'M9 11a3.2 3.2 0 100-6.4A3.2 3.2 0 009 11zM17 11.5a2.6 2.6 0 100-5.2 2.6 2.6 0 000 5.2zM3 20c0-3.4 2.7-6 6-6s6 2.6 6 6M15 14.4c3 .2 6 1.8 6 5.6',
  devotions: 'M4 5.5C4 4.7 4.7 4 5.5 4H12v16H5.5C4.7 20 4 19.3 4 18.5zM20 5.5c0-.8-.7-1.5-1.5-1.5H12v16h6.5c.8 0 1.5-.7 1.5-1.5z',
  duties: 'M6.5 4h11A2.5 2.5 0 0120 6.5v11a2.5 2.5 0 01-2.5 2.5h-11A2.5 2.5 0 014 17.5v-11A2.5 2.5 0 016.5 4zM8.5 12.5l2.3 2.3 4.7-4.8',
  events: 'M6 5h12a2.5 2.5 0 012.5 2.5v11A2.5 2.5 0 0118 21H6a2.5 2.5 0 01-2.5-2.5v-11A2.5 2.5 0 016 5zM3.5 10h17M8 3v4M16 3v4',
  birthdays: 'M4 11h16v9H4zM12 11v9M12 11c-3 0-4.5-1.2-4.5-2.7S9 6 10.3 6C11.6 6 12 8 12 11zM12 11c3 0 4.5-1.2 4.5-2.7S15 6 13.7 6C12.4 6 12 8 12 11z',
  announcements: 'M4 10v4l3 .5L14 19V5L7 9.5zM17 9a4 4 0 010 6',
  reports: 'M5 20V10M12 20V4M19 20v-7',
};

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly menuOpen = signal(false);
  protected readonly signingOut = signal(false);

  private readonly path = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects.split(/[?#]/)[0]),
    ),
    { initialValue: this.router.url.split(/[?#]/)[0] },
  );

  /** The sidebar shell is only used for signed-in users on app pages. */
  protected readonly showShell = computed(() => !!this.auth.currentUser() && !PUBLIC_PATHS.has(this.path()));

  protected readonly navItems = computed<NavItem[]>(() => {
    const user = this.auth.currentUser();
    if (!user) {
      return [];
    }
    const items: NavItem[] = [{ label: 'Dashboard', path: '/home', icon: ICONS.dashboard }];
    if (user.role === 'Member' && user.memberProfileId) {
      items.push({ label: 'My Profile', path: '/my-profile', icon: ICONS.profile });
    }
    items.push(
      { label: 'Family Directory', path: '/members', icon: ICONS.directory },
      { label: 'Devotions', path: '/devotions', icon: ICONS.devotions },
    );
    if (user.role === 'Pastor' || user.role === 'Member') {
      items.push({ label: user.role === 'Pastor' ? 'Duty Planning' : 'My Duties', path: '/duties', icon: ICONS.duties });
    }
    items.push(
      { label: 'Events', path: '/events', icon: ICONS.events },
      { label: 'Birthdays', path: '/birthdays', icon: ICONS.birthdays },
      { label: 'Announcements', path: '/announcements', icon: ICONS.announcements },
    );
    if (user.role === 'Pastor') {
      items.push({ label: 'Reports', path: '/reports', icon: ICONS.reports });
    }
    return items;
  });

  protected readonly initials = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return (email.split('@')[0].replace(/[^a-zA-Z]/g, '').slice(0, 2) || 'JM').toUpperCase();
  });

  constructor() {
    effect(() => {
      this.path();
      this.menuOpen.set(false);
    });
  }

  @HostListener('document:keydown.escape')
  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected logout(): void {
    this.signingOut.set(true);
    this.auth.logout().subscribe({
      next: () => {
        this.signingOut.set(false);
        void this.router.navigateByUrl('/login');
      },
      error: () => this.signingOut.set(false),
    });
  }
}
