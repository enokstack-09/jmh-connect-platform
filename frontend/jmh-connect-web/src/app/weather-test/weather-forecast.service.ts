import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { WeatherForecast } from './weather-forecast.model';

@Injectable({ providedIn: 'root' })
export class WeatherForecastService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = 'http://localhost:5080/weatherforecast';

  getForecast(): Observable<WeatherForecast[]> {
    return this.http.get<WeatherForecast[]>(this.endpoint);
  }
}
