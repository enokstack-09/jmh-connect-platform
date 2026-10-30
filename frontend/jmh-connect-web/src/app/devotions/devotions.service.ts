import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AttendanceRosterItem, DevotionAudit, DevotionMutation, DevotionSession, MyAttendance } from './devotions.models';

@Injectable({ providedIn: 'root' })
export class DevotionsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/devotions`;
  list(): Observable<DevotionSession[]> { return this.http.get<DevotionSession[]>(this.url); }
  create(request: DevotionMutation): Observable<DevotionSession> { return this.http.post<DevotionSession>(this.url, request); }
  update(id: number, request: DevotionMutation): Observable<void> { return this.http.put<void>(`${this.url}/${id}`, request); }
  cancel(id: number): Observable<void> { return this.http.post<void>(`${this.url}/${id}/cancel`, {}); }
  attendance(id: number): Observable<AttendanceRosterItem[]> { return this.http.get<AttendanceRosterItem[]>(`${this.url}/${id}/attendance`); }
  saveAttendance(id: number, entries: AttendanceRosterItem[]): Observable<void> { return this.http.put<void>(`${this.url}/${id}/attendance`, { entries }); }
  myAttendance(): Observable<MyAttendance[]> { return this.http.get<MyAttendance[]>(`${this.url}/my-attendance`); }
  audit(id: number): Observable<DevotionAudit[]> { return this.http.get<DevotionAudit[]>(`${this.url}/${id}/audit`); }
}
