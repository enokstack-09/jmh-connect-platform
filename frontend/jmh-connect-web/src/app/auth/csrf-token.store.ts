import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class CsrfTokenStore {
  private readonly requestToken = signal<string | null>(null);

  get(): string | null {
    return this.requestToken();
  }

  set(token: string): void {
    this.requestToken.set(token);
  }
}
