import { Component, computed, inject, signal } from '@angular/core';
import { SessionService } from '../../core/session.service';

@Component({
  selector: 'app-company-logo',
  template: `
    @if (logoUrl(); as url) {
      @if (failedUrl() !== url) {
        <img [src]="url" [alt]="companyName()" (error)="failedUrl.set(url)" />
      } @else {
        <span class="company-name">{{ companyName() }}</span>
      }
    } @else {
      <span class="company-name">{{ companyName() }}</span>
    }
  `,
  styles: `
    :host { display: flex; align-items: center; justify-content: center; min-width: 0; }
    img { display: block; width: 100%; height: 100%; object-fit: contain; }
    .company-name { color: #203e55; font-size: 1rem; font-weight: 700; text-align: center; overflow-wrap: anywhere; }
  `,
})
export class CompanyLogoComponent {
  private readonly session = inject(SessionService);
  protected readonly logoUrl = computed(() => this.session.session()?.empresa?.logoUrl?.trim() || null);
  protected readonly companyName = computed(() => this.session.session()?.empresa?.nome || 'Empresa');
  protected readonly failedUrl = signal<string | null>(null);
}
