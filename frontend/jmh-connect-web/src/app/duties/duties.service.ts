import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Duty, DutyAssignee, DutyAudit, DutyMutation } from './duties.models';

@Injectable({ providedIn: 'root' })
export class DutiesService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/duties`;
  list(): Observable<Duty[]> { return this.http.get<Duty[]>(this.url); }
  assignees(): Observable<DutyAssignee[]> { return this.http.get<DutyAssignee[]>(`${this.url}/assignees`); }
  create(request: DutyMutation): Observable<Duty> { return this.http.post<Duty>(this.url, request); }
  update(id: number, request: DutyMutation): Observable<void> { return this.http.put<void>(`${this.url}/${id}`, request); }
  cancel(id: number): Observable<void> { return this.http.post<void>(`${this.url}/${id}/cancel`, {}); }
  complete(id: number): Observable<void> { return this.http.post<void>(`${this.url}/${id}/complete`, {}); }
  audit(id: number): Observable<DutyAudit[]> { return this.http.get<DutyAudit[]>(`${this.url}/${id}/audit`); }
}
