import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CommunityEvent, CommunityEventAudit, CommunityEventMutation } from './events.models';

@Injectable({ providedIn: 'root' })
export class EventsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/events`;
  list(): Observable<CommunityEvent[]> { return this.http.get<CommunityEvent[]>(this.url); }
  create(request: CommunityEventMutation): Observable<CommunityEvent> { return this.http.post<CommunityEvent>(this.url, request); }
  update(id: number, request: CommunityEventMutation): Observable<void> { return this.http.put<void>(`${this.url}/${id}`, request); }
  cancel(id: number): Observable<void> { return this.http.post<void>(`${this.url}/${id}/cancel`, {}); }
  audit(id: number): Observable<CommunityEventAudit[]> { return this.http.get<CommunityEventAudit[]>(`${this.url}/${id}/audit`); }
}
