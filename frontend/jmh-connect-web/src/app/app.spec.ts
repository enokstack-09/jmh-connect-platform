import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { WeatherTestComponent } from './weather-test/weather-test.component';

describe('App', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WeatherTestComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('shows loading before the API responds and renders the forecast on success', () => {
    const fixture = TestBed.createComponent(WeatherTestComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading forecast');

    httpTesting.expectOne('http://localhost:5080/weatherforecast').flush([
      {
        date: '2026-10-14',
        temperatureC: 20,
        temperatureF: 68,
        summary: 'Mild',
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Connected successfully');
    expect(fixture.nativeElement.textContent).toContain('2026-10-14');
    expect(fixture.nativeElement.textContent).toContain('20°C');
  });

  it('shows an actionable message when the API request fails', () => {
    const fixture = TestBed.createComponent(WeatherTestComponent);
    fixture.detectChanges();

    httpTesting.expectOne('http://localhost:5080/weatherforecast').flush('Unavailable', {
      status: 503,
      statusText: 'Service Unavailable',
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Couldn’t load the forecast');
    expect(fixture.nativeElement.textContent).toContain('503');
    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Try again');
  });
});
