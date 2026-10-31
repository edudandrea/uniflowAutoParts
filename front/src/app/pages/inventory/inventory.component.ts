import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { EstoqueProduto, ImportarNfeResponse } from '../../core/app-models';
import { ApiService } from '../../core/api.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

interface InventoryMetric {
  icon: string;
  tone: 'success' | 'danger';
  label: string;
  value: string;
  delta: string;
  caption: string;
}

interface InventoryCategory {
  icon: string;
  label: string;
  total: string;
}

interface InventoryProduct extends EstoqueProduto {
  localizacao: string;
  barras: string;
  peso: string;
  imagemUrl: string;
}

type InventoryTab = 'search' | 'details';

interface XmlPrecheck {
  status: 'idle' | 'valid' | 'invalid';
  message: string;
}

@Component({
  selector: 'app-inventory',
  imports: [FormsModule, AppIconComponent],
  templateUrl: './inventory.component.html',
  styleUrl: './inventory.component.scss',
})
export class InventoryComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly tenantId = 1;

  protected readonly loading = signal(false);
  protected readonly loadError = signal('');
  protected readonly showNfeImportModal = signal(false);
  protected readonly activeTab = signal<InventoryTab>('search');
  protected readonly selectedProductCode = signal('');
  protected readonly checkedProductCodes = signal<ReadonlySet<string>>(new Set());
  protected readonly searchTerm = signal('');
  protected readonly categoryFilter = signal('Todas');
  protected readonly brandFilter = signal('Todas');
  protected readonly statusFilter = signal('Todas');
  protected readonly selectedXmlFile = signal<File | null>(null);
  protected readonly importingNfe = signal(false);
  protected readonly importMessage = signal('');
  protected readonly importedNfe = signal<ImportarNfeResponse | null>(null);
  protected readonly xmlPrecheck = signal<XmlPrecheck>({
    status: 'idle',
    message: 'Selecione um XML para validar a estrutura da NF-e.',
  });

  protected readonly products = signal<InventoryProduct[]>([]);

  protected readonly selectedProduct = computed(() => {
    return this.products().find((product) => product.codigo === this.selectedProductCode()) ?? this.products()[0] ?? null;
  });

  protected readonly filteredProducts = computed(() => {
    const term = this.normalize(this.searchTerm());

    return this.products().filter((product) => {
      const haystack = this.normalize(`${product.codigo} ${product.descricao} ${product.marca} ${product.categoria} ${product.fabricante} ${product.barras}`);
      const matchesTerm = !term || haystack.includes(term);
      const matchesCategory = this.categoryFilter() === 'Todas' || product.categoria === this.categoryFilter();
      const matchesBrand = this.brandFilter() === 'Todas' || product.marca === this.brandFilter();
      const matchesStatus = this.statusFilter() === 'Todas' || this.statusFor(product) === this.statusFilter();

      return matchesTerm && matchesCategory && matchesBrand && matchesStatus;
    });
  });

  protected readonly categoryOptions = computed(() => this.unique(this.products().map((product) => product.categoria)));
  protected readonly checkedVisibleCount = computed(() =>
    this.filteredProducts().filter((product) => this.checkedProductCodes().has(product.codigo)).length,
  );
  protected readonly allVisibleChecked = computed(() =>
    this.filteredProducts().length > 0 && this.checkedVisibleCount() === this.filteredProducts().length,
  );
  protected readonly someVisibleChecked = computed(() =>
    this.checkedVisibleCount() > 0 && !this.allVisibleChecked(),
  );
  protected readonly brandOptions = computed(() => this.unique(this.products().map((product) => product.marca)));

  protected readonly metrics = computed<InventoryMetric[]>(() => {
    const products = this.products();
    const totalItems = products.reduce((total, product) => total + product.estoque, 0);
    const stockValue = products.reduce((total, product) => total + product.estoque * product.valor, 0);
    const lowStock = products.filter((product) => product.estoque > 0 && product.estoque <= product.minimo).length;
    const noStock = products.filter((product) => product.estoque <= 0).length;

    return [
      { icon: 'boxes', tone: 'success', label: 'Total de itens', value: this.formatNumber(totalItems), delta: 'base atual', caption: '' },
      { icon: 'circle-dollar-sign', tone: 'success', label: 'Valor em estoque', value: this.formatCurrency(stockValue), delta: 'base atual', caption: '' },
      { icon: 'info', tone: 'danger', label: 'Itens com estoque baixo', value: String(lowStock), delta: 'base atual', caption: '' },
      { icon: 'x', tone: 'danger', label: 'Itens sem estoque', value: String(noStock), delta: 'base atual', caption: '' },
    ];
  });

  protected readonly categories = computed<InventoryCategory[]>(() => {
    const counts = new Map<string, number>();
    this.products().forEach((product) => counts.set(product.categoria, (counts.get(product.categoria) ?? 0) + 1));

    return Array.from(counts.entries()).map(([label, total]) => ({ icon: 'tags', label, total: `${total} itens` }));
  });

  ngOnInit(): void {
    this.carregarEstoque();
  }

  protected carregarEstoque(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.loadError.set('');
    this.api.listarEstoque(this.tenantId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (products) => {
        this.products.set(products.map((product) => ({
          ...product,
          localizacao: '',
          barras: '',
          peso: '',
          imagemUrl: '',
        })));
        this.checkedProductCodes.set(new Set());
        this.loading.set(false);
      },
      error: () => {
        this.products.set([]);
        this.loadError.set('Nao foi possivel carregar o estoque. Tente novamente.');
        this.loading.set(false);
      },
    });
  }

  protected abrirNovoProduto(): void {
    void this.router.navigate(['/app/produtos']);
  }

  protected abrirImportacaoNfe(): void {
    this.selectedXmlFile.set(null);
    this.importedNfe.set(null);
    this.importMessage.set('');
    this.xmlPrecheck.set({
      status: 'idle',
      message: 'Selecione um XML para validar a estrutura da NF-e.',
    });
    this.showNfeImportModal.set(true);
  }

  protected fecharImportacaoNfe(): void {
    this.showNfeImportModal.set(false);
  }

  protected async selecionarXml(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedXmlFile.set(file);
    this.importMessage.set('');
    this.importedNfe.set(null);

    if (!file) {
      this.xmlPrecheck.set({
        status: 'idle',
        message: 'Selecione um XML para validar a estrutura da NF-e.',
      });
      return;
    }

    this.xmlPrecheck.set(await this.validarXmlSelecionado(file));
  }

  protected importarXmlNfe(): void {
    const file = this.selectedXmlFile();
    if (!file) {
      this.importMessage.set('Selecione o XML autorizado da NF-e de compra.');
      return;
    }

    if (this.xmlPrecheck().status === 'invalid') {
      this.importMessage.set(this.xmlPrecheck().message);
      return;
    }

    this.importingNfe.set(true);
    this.importMessage.set('');
    this.importedNfe.set(null);

    this.api.importarXmlNfeCompra(this.tenantId, file).subscribe({
      next: (result) => {
        this.importedNfe.set(result);
        this.importMessage.set('NF-e importada e pronta para conferencia.');
        this.importingNfe.set(false);
        this.carregarEstoque();
      },
      error: (error) => {
        this.importMessage.set(error?.error || 'Nao foi possivel importar o XML da NF-e.');
        this.importingNfe.set(false);
      },
    });
  }

  protected selecionarProduto(codigo: string): void {
    this.selectedProductCode.set(codigo);
    this.activeTab.set('details');
  }

  protected marcarProduto(codigo: string, checked: boolean): void {
    this.checkedProductCodes.update((current) => {
      const next = new Set(current);
      if (checked) next.add(codigo);
      else next.delete(codigo);
      return next;
    });
  }

  protected marcarTodosVisiveis(checked: boolean): void {
    this.checkedProductCodes.update((current) => {
      const next = new Set(current);
      for (const product of this.filteredProducts()) {
        if (checked) next.add(product.codigo);
        else next.delete(product.codigo);
      }
      return next;
    });
  }

  protected voltarPesquisa(): void {
    this.activeTab.set('search');
  }

  protected limparFiltros(): void {
    this.searchTerm.set('');
    this.categoryFilter.set('Todas');
    this.brandFilter.set('Todas');
    this.statusFilter.set('Todas');
  }

  protected statusFor(product: InventoryProduct): 'Sem estoque' | 'Estoque baixo' | 'Em estoque' {
    if (product.estoque <= 0) {
      return 'Sem estoque';
    }

    if (product.estoque <= product.minimo) {
      return 'Estoque baixo';
    }

    return 'Em estoque';
  }

  protected statusClass(product: InventoryProduct): string {
    const status = this.statusFor(product);
    return status === 'Em estoque' ? 'ok' : status === 'Estoque baixo' ? 'low' : 'empty';
  }

  protected formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  protected formatNumber(value: number): string {
    return value.toLocaleString('pt-BR');
  }

  protected formatDate(value: string | null): string {
    if (!value) {
      return '-';
    }

    return new Date(value).toLocaleDateString('pt-BR');
  }

  private unique(values: string[]): string[] {
    return [...new Set(values)].sort((a, b) => a.localeCompare(b));
  }

  private normalize(value: string): string {
    return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }

  private async validarXmlSelecionado(file: File): Promise<XmlPrecheck> {
    try {
      const content = await file.text();
      const document = new DOMParser().parseFromString(content, 'application/xml');

      if (document.querySelector('parsererror')) {
        return {
          status: 'invalid',
          message: 'O arquivo selecionado nao e um XML valido.',
        };
      }

      const elements = Array.from(document.getElementsByTagName('*'));
      const byName = (name: string) => elements.find((element) => element.localName === name);
      const allByName = (name: string) => elements.filter((element) => element.localName === name);
      const infNfe = byName('infNFe');

      if (!byName('NFe') || !infNfe) {
        return {
          status: 'invalid',
          message: 'Este XML nao e uma NF-e. Notas de servico ou outros documentos nao entram pelo estoque.',
        };
      }

      const modelo = byName('mod')?.textContent?.trim();
      if (modelo !== '55') {
        return {
          status: 'invalid',
          message: 'Somente NF-e modelo 55 de compra de itens pode ser importada no estoque.',
        };
      }

      const possuiItensProduto = allByName('det').some((det) => {
        return Array.from(det.children).some((child) => child.localName === 'prod');
      });

      if (!possuiItensProduto) {
        return {
          status: 'invalid',
          message: 'A nota fiscal selecionada nao possui itens de produto para entrada em estoque.',
        };
      }

      const numero = byName('nNF')?.textContent?.trim();
      const serie = byName('serie')?.textContent?.trim();

      return {
        status: 'valid',
        message: `NF-e ${numero || '-'}${serie ? `/${serie}` : ''} com itens de produto encontrada.`,
      };
    } catch {
      return {
        status: 'invalid',
        message: 'Nao foi possivel ler o XML selecionado.',
      };
    }
  }
}
