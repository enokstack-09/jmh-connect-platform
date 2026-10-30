import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ArchivedMemberSummary, MemberDetails, MemberHistoryEntry, MemberMutation, MemberSummary, PhotoConsent, PhotoManagement, PhotoState, SelfProfileMutation } from './members.models';

@Injectable({ providedIn: 'root' })
export class MembersService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/members`;

  list(filter?: { residentType?: 'Student' | 'Worker'; search?: string }): Observable<MemberSummary[]> {
    let params = new HttpParams();
    if (filter?.residentType) params = params.set('residentType', filter.residentType);
    if (filter?.search?.trim()) params = params.set('search', filter.search.trim());
    return this.http.get<MemberSummary[]>(this.apiUrl, { params });
  }

  get(id: number): Observable<MemberDetails> { return this.http.get<MemberDetails>(`${this.apiUrl}/${id}`); }
  getMyProfile(): Observable<MemberDetails> { return this.http.get<MemberDetails>(`${this.apiUrl}/me`); }
  updateMyProfile(request: SelfProfileMutation): Observable<void> { return this.http.put<void>(`${this.apiUrl}/me`, request); }
  create(request: MemberMutation): Observable<MemberDetails> { return this.http.post<MemberDetails>(this.apiUrl, request); }
  update(id: number, request: MemberMutation): Observable<void> { return this.http.put<void>(`${this.apiUrl}/${id}`, request); }
  archive(id: number): Observable<void> { return this.http.post<void>(`${this.apiUrl}/${id}/archive`, {}); }
  restore(id: number): Observable<void> { return this.http.post<void>(`${this.apiUrl}/${id}/restore`, {}); }
  archived(): Observable<ArchivedMemberSummary[]> { return this.http.get<ArchivedMemberSummary[]>(`${this.apiUrl}/archived`); }
  archivedDetail(id: number): Observable<MemberDetails> { return this.http.get<MemberDetails>(`${this.apiUrl}/archived/${id}`); }
  history(id: number): Observable<MemberHistoryEntry[]> { return this.http.get<MemberHistoryEntry[]>(`${this.apiUrl}/${id}/history`); }
  photoUrl(id: number, version = 0): string { return `${this.apiUrl}/${id}/photo?v=${version}`; }
  photoManagement(id: number): Observable<PhotoManagement> { return this.http.get<PhotoManagement>(`${this.apiUrl}/${id}/photo-management`); }
  uploadPhoto(id: number, file: File): Observable<PhotoState> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<PhotoState>(`${this.apiUrl}/${id}/photo`, body);
  }
  approvePhoto(id: number, photoId: number): Observable<void> { return this.http.post<void>(`${this.apiUrl}/${id}/photos/${photoId}/approve`, {}); }
  rejectPhoto(id: number, photoId: number): Observable<void> { return this.http.post<void>(`${this.apiUrl}/${id}/photos/${photoId}/reject`, {}); }
  grantPhotoConsent(id: number, request: { guardianContactId: number; evidenceReference?: string; notes?: string }): Observable<PhotoConsent> {
    return this.http.post<PhotoConsent>(`${this.apiUrl}/${id}/photo-consent`, request);
  }
  withdrawPhotoConsent(id: number): Observable<void> { return this.http.post<void>(`${this.apiUrl}/${id}/photo-consent/withdraw`, {}); }
}
