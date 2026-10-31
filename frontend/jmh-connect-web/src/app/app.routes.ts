import { Routes } from '@angular/router';
import { LandingComponent } from './landing/landing.component';
import { AccountSetupComponent } from './auth/account-setup.component';
import { authGuard } from './auth/auth.guard';
import { LoginComponent } from './auth/login.component';
import { HomeComponent } from './home/home.component';
import { MemberDirectoryComponent } from './members/member-directory.component';
import { MyProfileComponent } from './members/my-profile.component';
import { DevotionsComponent } from './devotions/devotions.component';
import { DutiesComponent } from './duties/duties.component';
import { EventsComponent } from './events/events.component';
import { BirthdaysComponent } from './birthdays/birthdays.component';
import { AnnouncementsComponent } from './announcements/announcements.component';
import { ReportsComponent } from './reports/reports.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: LandingComponent },
  { path: 'login', component: LoginComponent },
  { path: 'account/setup', component: AccountSetupComponent },
  { path: 'home', component: HomeComponent, canActivate: [authGuard] },
  { path: 'members', component: MemberDirectoryComponent, canActivate: [authGuard] },
  { path: 'my-profile', component: MyProfileComponent, canActivate: [authGuard] },
  { path: 'devotions', component: DevotionsComponent, canActivate: [authGuard] },
  { path: 'duties', component: DutiesComponent, canActivate: [authGuard] },
  { path: 'events', component: EventsComponent, canActivate: [authGuard] },
  { path: 'birthdays', component: BirthdaysComponent, canActivate: [authGuard] },
  { path: 'announcements', component: AnnouncementsComponent, canActivate: [authGuard] },
  { path: 'reports', component: ReportsComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'home' },
];
