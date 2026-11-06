import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionService } from '../../core/session.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

@Component({
  selector: 'app-hub-login',
  imports: [AppIconComponent],
  template: `
    <main class="login-page"><section class="login-card">
      <img src="/logo-uniflow-autoparts.png" alt="UniFlow AutoParts" />
      <span class="eyebrow">ACESSO INTEGRADO UNIFLOW</span>
      <h1>{{ starting() ? 'Conectando ao HUB...' : 'Entre com sua conta UniFlow' }}</h1>
      <p>Use sua conta do HUB para acessar a empresa e os sistemas contratados.</p>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (unavailable()) { <p role="status">A integração com o HUB ainda não está configurada. Contate o administrador.</p> }
      <button class="primary-button" type="button" [disabled]="starting() || unavailable()" (click)="login()"><app-icon name="lock" [size]="18" />Entrar pelo HUB</button>
      <small>Seus cadastros e operações ficam vinculados à empresa autenticada.</small>
    </section></main>
  `,
  styles: [`
    .login-page { min-height:100dvh;display:grid;place-items:center;padding:24px;background:radial-gradient(ellipse at 80% 15%,#caf1e3,transparent 60%),#f2f8fc; }
    .login-card { display:grid;gap:20px;justify-items:center;width:min(100%,460px);padding:40px;text-align:center;border-radius:18px;background:#fff;box-shadow:0 20px 80px #123b5810; }
    img { width:110px;height:95px;object-fit:contain; }.eyebrow { font-size:.65rem;letter-spacing:.16em;color:#07976b;font-weight:700; }
    h1 { margin:0;font-size:1.6rem;letter-spacing:-.035em; }p { margin:0;color:#647c91;font-size:.9rem;line-height:1.6; }button { display:flex;align-items:center;justify-content:center;gap:10px;width:100%; }small { color:#8b9eae;line-height:1.7;font-size:.75rem; }.error { color:#b83348;background:#fff1f4;padding:12px;border-radius:8px; }
  `],
})
export class HubLoginComponent implements OnInit {
  private readonly session = inject(SessionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly starting = signal(false);
  protected readonly unavailable = signal(false);
  protected readonly error = signal('');

  ngOnInit(): void {
    if (this.route.snapshot.routeConfig?.path === 'sso') { this.login(); return; }
    if (this.route.snapshot.queryParamMap.has('erro'))
      this.error.set('Não foi possível concluir a entrada. Verifique a permissão para o AutoParts no HUB ou tente novamente.');
    this.session.load().subscribe({
      next: value => {
        this.unavailable.set(!value.available);
        if (value.authenticated) void this.router.navigateByUrl('/app/dashboard');
      },
      error: () => this.error.set('Não foi possível consultar sua sessão. Verifique a conexão e tente novamente.'),
    });
  }

  protected login(): void {
    this.starting.set(true);
    this.session.login(this.route.snapshot.queryParamMap.get('returnTo') ?? '/app/dashboard');
  }
}
