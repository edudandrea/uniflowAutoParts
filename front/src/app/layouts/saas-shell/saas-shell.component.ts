import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthSessionService } from '../../core/auth-session.service';
import { CompaniesStoreService } from '../../core/companies-store.service';

@Component({
  selector: 'app-saas-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './saas-shell.component.html',
  styleUrl: './saas-shell.component.scss',
})
export class SaasShellComponent {
  protected readonly auth = inject(AuthSessionService);
  protected readonly companiesStore = inject(CompaniesStoreService);
  protected readonly logoUnavailable = signal(false);
  private readonly router = inject(Router);

  protected sair() {
    this.auth.sair();
    this.router.navigateByUrl('/login');
  }
}
