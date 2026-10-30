import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { WeatherForecast } from './weather-forecast.model';
import { WeatherForecastService } from './weather-forecast.service';

@Component({
  selector: 'app-weather-test',
  templateUrl: './weather-test.component.html',
  styleUrl: './weather-test.component.scss',
})
export class WeatherTestComponent implements OnInit {
  private readonly weatherService = inject(WeatherForecastService);

  protected readonly forecasts = signal<WeatherForecast[]>([]);
  protected readonly loading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadForecast();
  }

  protected loadForecast(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.weatherService.getForecast().subscribe({
      next: (forecasts) => {
        this.forecasts.set(forecasts);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(
          error.status === 0
            ? 'Could not reach the API. Check that it is running at http://localhost:5080.'
            : `The API request failed (${error.status}). Please try again.`,
        );
        this.loading.set(false);
      },
    });
  }
}
