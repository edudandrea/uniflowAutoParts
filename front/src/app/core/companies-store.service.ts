import { inject, Injectable, signal } from '@angular/core';

import { ApiService } from './api.service';
import { EmpresaResumoResponse } from './app-models';

@Injectable({ providedIn: 'root' })
export class CompaniesStoreService {
  private readonly api = inject(ApiService);

  readonly empresas = signal<EmpresaResumoResponse[]>([]);
  readonly loading = signal(false);
  readonly message = signal('');

  carregar() {
    this.loading.set(true);
    this.api.listarEmpresas().subscribe({
      next: (empresas) => {
        this.empresas.set(empresas);
        this.loading.set(false);
      },
      error: () => {
        this.message.set('Nao foi possivel carregar as empresas.');
        this.loading.set(false);
      },
    });
  }
}
