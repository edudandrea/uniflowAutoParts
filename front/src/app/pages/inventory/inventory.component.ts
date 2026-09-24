import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ImportarNfeResponse } from '../../core/app-models';
import { ApiService } from '../../core/api.service';

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

interface InventoryProduct {
  codigo: string;
  descricao: string;
  marca: string;
  categoria: string;
  localizacao: string;
  estoque: number;
  minimo: number;
  valor: number;
  fabricante: string;
  barras: string;
  ncm: string;
  peso: string;
  imagemUrl: string;
}

interface XmlPrecheck {
  status: 'idle' | 'valid' | 'invalid';
  message: string;
}

@Component({
  selector: 'app-inventory',
  imports: [ReactiveFormsModule],
  templateUrl: './inventory.component.html',
  styleUrl: './inventory.component.scss',
})
export class InventoryComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly tenantId = 1;

  protected readonly showProductModal = signal(false);
  protected readonly showNfeImportModal = signal(false);
  protected readonly selectedProductCode = signal('');
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
    return this.products().find((product) => product.codigo === this.selectedProductCode()) ?? null;
  });

  protected readonly metrics = computed<InventoryMetric[]>(() => {
    const products = this.products();
    const totalItems = products.reduce((total, product) => total + product.estoque, 0);
    const stockValue = products.reduce((total, product) => total + product.estoque * product.valor, 0);
    const lowStock = products.filter((product) => product.estoque > 0 && product.estoque <= product.minimo).length;
    const noStock = products.filter((product) => product.estoque === 0).length;

    return [
      { icon: 'inventory_2', tone: 'success', label: 'Total de itens', value: this.formatNumber(totalItems), delta: 'base atual', caption: '' },
      { icon: 'paid', tone: 'success', label: 'Valor em estoque', value: this.formatCurrency(stockValue), delta: 'base atual', caption: '' },
      { icon: 'warning', tone: 'danger', label: 'Itens com estoque baixo', value: String(lowStock), delta: 'base atual', caption: '' },
      { icon: 'cancel', tone: 'danger', label: 'Itens sem estoque', value: String(noStock), delta: 'base atual', caption: '' },
    ];
  });

  protected readonly categories = computed<InventoryCategory[]>(() => {
    const counts = new Map<string, number>();
    this.products().forEach((product) => counts.set(product.categoria, (counts.get(product.categoria) ?? 0) + 1));

    return Array.from(counts.entries()).map(([label, total]) => ({ icon: 'category', label, total: `${total} itens` }));
  });

  protected readonly productForm = this.fb.nonNullable.group({
    codigo: ['', [Validators.required]],
    descricao: ['', [Validators.required]],
    marca: ['', [Validators.required]],
    categoria: ['Filtros', [Validators.required]],
    localizacao: ['', [Validators.required]],
    estoque: [0, [Validators.required, Validators.min(0)]],
    minimo: [0, [Validators.required, Validators.min(0)]],
    valor: [0, [Validators.required, Validators.min(0)]],
    fabricante: [''],
    barras: [''],
    ncm: [''],
    peso: [''],
    imagemUrl: [''],
  });

  protected abrirNovoProduto(): void {
    this.productForm.reset({
      codigo: '',
      descricao: '',
      marca: '',
      categoria: 'Filtros',
      localizacao: '',
      estoque: 0,
      minimo: 0,
      valor: 0,
      fabricante: '',
      barras: '',
      ncm: '',
      peso: '',
      imagemUrl: '',
    });
    this.showProductModal.set(true);
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

  protected fecharNovoProduto(): void {
    this.showProductModal.set(false);
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
      },
      error: (error) => {
        this.importMessage.set(error?.error || 'Nao foi possivel importar o XML da NF-e.');
        this.importingNfe.set(false);
      },
    });
  }

  protected cadastrarProduto(): void {
    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      return;
    }

    const product = this.productForm.getRawValue();
    this.products.update((products) => [product, ...products]);
    this.selectedProductCode.set(product.codigo);
    this.showProductModal.set(false);
  }

  protected selecionarProduto(codigo: string): void {
    this.selectedProductCode.set(codigo);
  }

  protected statusFor(product: InventoryProduct): 'Sem estoque' | 'Estoque baixo' | 'Em estoque' {
    if (product.estoque === 0) {
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
