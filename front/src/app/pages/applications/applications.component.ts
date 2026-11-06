import { SessionService } from '../../core/session.service';
﻿import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { EstoqueProduto } from '../../core/app-models';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

@Component({
  selector: 'app-applications',
  imports: [FormsModule, AppIconComponent],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss',
})
export class ApplicationsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  private readonly destroyRef = inject(DestroyRef);
  private get tenantId(): number { return this.session.empresaId; }

  protected readonly activeTab = signal<'search' | 'details'>('search');
  protected readonly searchTerm = signal('');
  protected readonly selectedPartId = signal<number | null>(null);
  protected readonly parts = signal<EstoqueProduto[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal('');

  protected readonly selectedPart = computed(() =>
    this.parts().find((part) => part.id === this.selectedPartId()) ?? null,
  );

  protected readonly searchResults = computed(() => {
    const term = this.normalize(this.searchTerm());
    return this.parts().filter((part) => this.normalize(
      `${part.codigo} ${part.fabricante} ${part.descricao} ${part.marca} ${part.categoria}`,
    ).includes(term));
  });

  ngOnInit(): void {
    this.carregarProdutos();
  }

  protected carregarProdutos(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.loadError.set('');
    // Retorna todos os produtos cadastrados, inclusive sem saldo em estoque.
    this.api.listarEstoque(this.tenantId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (parts) => {
        this.parts.set(parts);
        this.selectedPartId.set(parts[0]?.id ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.parts.set([]);
        this.selectedPartId.set(null);
        this.loadError.set('Nao foi possivel carregar os produtos. Tente novamente.');
        this.loading.set(false);
      },
    });
  }

  protected selectPart(part: EstoqueProduto): void {
    this.selectedPartId.set(part.id);
    this.activeTab.set('details');
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
  }

  private normalize(value: string): string {
    return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }
}
