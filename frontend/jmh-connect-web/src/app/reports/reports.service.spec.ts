import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ReportsService } from './reports.service';

describe('ReportsService', () => {
  const fromUtc = '2026-10-01T00:00:00Z'; const toUtc = '2026-10-31T00:00:00Z';
  let http: HttpTestingController;
  afterEach(() => http.verify());
  it('uses a Pastor-only report API contract with date filters', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ReportsService); http = TestBed.inject(HttpTestingController);
    service.summary(fromUtc, toUtc).subscribe(value => expect(value.activeResidents).toBe(2));
    const request = http.expectOne(item => item.url.endsWith('/api/reports/summary'));
    expect(request.request.params.get('fromUtc')).toBe(fromUtc); expect(request.request.params.get('toUtc')).toBe(toUtc);
    request.flush({activeResidents:2,archivedResidents:0,devotionSessions:1,presentAttendance:1,assignedDuties:1,completedDuties:0,scheduledEvents:1,fromUtc,toUtc});
  });
});
