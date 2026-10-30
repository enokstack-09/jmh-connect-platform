import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Birthday, BirthdayManagement } from './birthdays.models';
@Injectable({ providedIn: 'root' })
export class BirthdaysService {
  private readonly http = inject(HttpClient); private readonly url = `${environment.apiBaseUrl}/birthdays`;
  list(): Observable<Birthday[]> { return this.http.get<Birthday[]>(this.url); }
  manage(): Observable<BirthdayManagement[]> { return this.http.get<BirthdayManagement[]>(`${this.url}/manage`); }
  setVisibility(id: number, isBirthdayVisible: boolean): Observable<void> { return this.http.put<void>(`${this.url}/${id}/visibility`, { isBirthdayVisible }); }
}
