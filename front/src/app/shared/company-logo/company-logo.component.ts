import { Component, computed, inject, input, signal } from '@angular/core';
import { SessionService } from '../../core/session.service';

@Component({
  selector: 'app-company-logo',
  host: { '[class.with-name]': 'showName()' },
  template: `
    @let url = logoUrl();
    @let hasLogo = url !== null && failedUrl() !== url;
    @if (url && hasLogo) {
      <img [src]="url" [alt]="showName() ? '' : companyName()" (error)="failedUrl.set(url)" />
    }
    @if (showName() || !hasLogo) {
      <span class="company-name">{{ companyName() }}</span>
    }
  `,
  styles: `
    :host { display: flex; align-items: center; justify-content: center; min-width: 0; }
    img { display: block; width: 100%; height: 100%; object-fit: contain; }
    .company-name { color: #203e55; font-size: 1rem; font-weight: 700; text-align: center; overflow-wrap: anywhere; }
    :host(.with-name) { justify-content: flex-start; gap: 9px; }
    :host(.with-name) img { flex: 0 0 38px; width: 38px; height: 38px; }
    :host(.with-name) .company-name { color: var(--ink); line-height: 1.2; text-align: left; }
  `,
})
export class CompanyLogoComponent {
  readonly showName = input(false);
  private readonly session = inject(SessionService);
  protected readonly logoUrl = computed(() => this.session.session()?.empresa?.logoUrl?.trim() || null);
  protected readonly companyName = computed(() => this.session.session()?.empresa?.nome || 'Empresa');
  protected readonly failedUrl = signal<string | null>(null);
}
