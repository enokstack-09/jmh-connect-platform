import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AccountSetupRequest,
  AntiforgeryResponse,
  AuthenticatedUser,
  CreateAccountRequest,
  LoginCredentials,
} from './auth.models';
import { CsrfTokenStore } from './csrf-token.store';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly csrfTokenStore = inject(CsrfTokenStore);
  private readonly apiUrl = `${environment.apiBaseUrl}/auth`;

  readonly currentUser = signal<AuthenticatedUser | null>(null);

  loadCurrentUser(): Observable<AuthenticatedUser | null> {
    return this.http.get<AuthenticatedUser>(`${this.apiUrl}/me`).pipe(
      switchMap((user) => this.prepareAntiforgery().pipe(map(() => user))),
      tap((user) => this.currentUser.set(user)),
      catchError((error: HttpErrorResponse) => {
        if (error.status !== 401 && error.status !== 403) {
          throw error;
        }
        this.currentUser.set(null);
        return of(null);
      }),
    );
  }

  login(credentials: LoginCredentials): Observable<AuthenticatedUser> {
    return this.prepareAntiforgery().pipe(
      switchMap(() => this.http.post<AuthenticatedUser>(`${this.apiUrl}/login`, credentials)),
      tap((user) => this.currentUser.set(user)),
    );
  }

  logout(): Observable<void> {
    return this.prepareAntiforgery().pipe(
      switchMap(() => this.http.post<void>(`${this.apiUrl}/logout`, {})),
      tap(() => this.currentUser.set(null)),
    );
  }

  createAccount(request: CreateAccountRequest): Observable<void> {
    return this.prepareAntiforgery().pipe(
      switchMap(() => this.http.post<void>(`${environment.apiBaseUrl}/accounts`, request)),
    );
  }

  changeRole(userId: string, role: AuthenticatedUser['role']): Observable<void> {
    return this.prepareAntiforgery().pipe(
      switchMap(() => this.http.put<void>(`${environment.apiBaseUrl}/accounts/${encodeURIComponent(userId)}/role`, { role })),
    );
  }

  completeAccountSetup(request: AccountSetupRequest): Observable<void> {
    return this.prepareAntiforgery().pipe(
      switchMap(() => this.http.post<void>(`${this.apiUrl}/complete-setup`, request)),
    );
  }

  private prepareAntiforgery(): Observable<void> {
    return this.http.get<AntiforgeryResponse>(`${this.apiUrl}/antiforgery`).pipe(
      tap(({ requestToken }) => this.csrfTokenStore.set(requestToken)),
      map(() => undefined),
    );
  }
}
