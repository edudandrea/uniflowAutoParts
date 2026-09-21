import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

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

@Component({
  selector: 'app-inventory',
  imports: [ReactiveFormsModule],
  templateUrl: './inventory.component.html',
  styleUrl: './inventory.component.scss',
})
export class InventoryComponent {
  private readonly fb = inject(FormBuilder);

  protected readonly showProductModal = signal(false);
  protected readonly selectedProductCode = signal('');

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

  protected fecharNovoProduto(): void {
    this.showProductModal.set(false);
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
}
