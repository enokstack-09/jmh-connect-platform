import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { CsrfTokenStore } from './csrf-token.store';

const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

export const cookieAuthInterceptor: HttpInterceptorFn = (request, next) => {
  const apiBaseUrl = new URL(environment.apiBaseUrl, window.location.origin);
  const requestUrl = new URL(request.url, window.location.origin);
  const isApiPath = requestUrl.pathname === apiBaseUrl.pathname ||
    requestUrl.pathname.startsWith(`${apiBaseUrl.pathname}/`);
  if (requestUrl.origin !== apiBaseUrl.origin || !isApiPath) {
    return next(request);
  }

  const csrfToken = inject(CsrfTokenStore).get();
  let apiRequest = request.clone({ withCredentials: true });
  if (unsafeMethods.has(request.method) && csrfToken) {
    apiRequest = apiRequest.clone({ setHeaders: { 'X-CSRF-TOKEN': csrfToken } });
  }

  return next(apiRequest);
};
