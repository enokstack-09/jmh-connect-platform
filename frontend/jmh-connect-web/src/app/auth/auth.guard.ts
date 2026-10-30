import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const redirectToLogin = () => router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });

  if (auth.currentUser()) {
    return true;
  }

  return auth.loadCurrentUser().pipe(
    map((user) => user ? true : redirectToLogin()),
    catchError(() => of(redirectToLogin())),
  );
};
