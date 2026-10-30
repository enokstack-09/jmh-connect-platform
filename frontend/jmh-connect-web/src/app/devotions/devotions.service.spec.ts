import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, it } from 'vitest';
import { DevotionsService } from './devotions.service';

describe('DevotionsService API contract', () => {
  let service: DevotionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(DevotionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists sessions from the ASP.NET controller route', () => {
    service.list().subscribe();
    const request = http.expectOne('http://localhost:5080/api/devotions');
    request.flush([]);
  });

  it('creates sessions at the same controller route', () => {
    const session = {
      title: 'Dummy route test',
      scheduledStartUtc: '2026-11-05T00:30:00.000Z',
      durationMinutes: 30,
    };
    service.create(session).subscribe();
    const request = http.expectOne('http://localhost:5080/api/devotions');
    if (request.request.method !== 'POST') throw new Error('Expected POST for devotion creation.');
    request.flush({ id: 1, ...session, status: 1 });
  });
});
