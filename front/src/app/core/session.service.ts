import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

export interface HubSession {
  authenticated: boolean;
  available: boolean;
  portalUrl: string | null;
  csrfToken?: string;
  empresa?: { id: number; nome: string; razaoSocial: string; cnpj: string | null; hubTenantId: string };
  usuario?: { id: string; sujeito: string; nome: string; email: string; perfil: string };
}

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly session = signal<HubSession | null>(null);
  private pending: Observable<HubSession> | null = null;

  constructor() {
    const changed = (event: StorageEvent) => {
      if (event.key !== 'uniflow:session:identity' || !this.session()?.authenticated) return;
      if (event.newValue !== this.fingerprint(this.session()!)) window.location.replace('/entrar');
    };
    window.addEventListener('storage', changed);
    this.destroyRef.onDestroy(() => window.removeEventListener('storage', changed));
  }

  get empresaId(): number {
    const session = this.session();
    if (!session?.authenticated || !session.empresa?.id) throw new Error('Sessao de empresa indisponivel. Entre pelo HUB.');
    return session.empresa.id;
  }

  load(): Observable<HubSession> {
    if (!this.pending) {
      this.pending = this.http.get<HubSession>(`${environment.apiUrl}/auth/session`).pipe(
        tap(session => {
          this.session.set(session);
          try {
            const identity = this.fingerprint(session);
            if (identity) localStorage.setItem('uniflow:session:identity', identity);
            else localStorage.removeItem('uniflow:session:identity');
          } catch { /* O acesso continua com a sessao validada no servidor. */ }
        }),
        finalize(() => this.pending = null),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }
    return this.pending;
  }

  login(returnTo = '/app/dashboard'): void {
    const destination = returnTo.startsWith('/app/') && !returnTo.includes('://') ? returnTo : '/app/dashboard';
    window.location.assign(`${environment.apiUrl}/auth/login?returnTo=${encodeURIComponent(destination)}`);
  }

  logout(): Observable<{ portalUrl: string | null }> {
    return this.http.post<{ portalUrl: string | null }>(`${environment.apiUrl}/auth/logout`, {}).pipe(
      tap(() => {
        this.session.set(null);
        try { localStorage.removeItem('uniflow:session:identity'); } catch { }
      }),
    );
  }

  storageKey(feature: string): string {
    const subject = this.session()?.usuario?.sujeito;
    if (!subject) throw new Error('Usuario nao autenticado.');
    return `uniflow:${feature}:empresa-${this.empresaId}:usuario-${encodeURIComponent(subject)}`;
  }

  private fingerprint(session: HubSession): string | null {
    return session.authenticated && session.empresa && session.usuario
      ? `${session.empresa.id}:${session.usuario.sujeito}` : null;
  }
}

export const companySessionGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);
  const router = inject(Router);
  return session.load().pipe(
    map(value => value.authenticated && value.empresa && value.usuario ? true : router.createUrlTree(['/entrar'], { queryParams: { returnTo: state.url } })),
    catchError(() => of(router.createUrlTree(['/entrar']))),
  );
};

export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  // Nao transmite tokens CSRF para URLs externas, inclusive logos de marcas.
  if (!request.url.startsWith('/api/')) return next(request);
  const session = inject(SessionService);
  const csrf = session.session()?.csrfToken;
  const unsafe = !['GET', 'HEAD', 'OPTIONS'].includes(request.method);
  if (unsafe && csrf) request = request.clone({ setHeaders: { 'X-Parts-CSRF': csrf } });
  return next(request).pipe(catchError(error => {
    if (error.status === 401 && !request.url.includes('/auth/session')) {
      session.session.set(null);
      // O reload descarta dados, rascunhos em memoria e componentes da empresa anterior.
      window.location.replace('/entrar');
    }
    return throwError(() => error);
  }));
};
