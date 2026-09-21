import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Categoria, Marca } from '../../core/app-models';
import { ApiService } from '../../core/api.service';
import { AuthSessionService } from '../../core/auth-session.service';

interface CatalogMetric {
  icon: string;
  label: string;
  value: string;
  delta: string;
  tone: 'success' | 'danger';
}

interface CatalogProduct {
  codigo: string;
  codigoFabricante: string;
  descricao: string;
  marcaId: number | null;
  marca: string;
  categoriaId: number | null;
  categoria: string;
  precoCompra: number;
  precoVenda: number;
  status: 'Ativo' | 'Inativo';
  imagemUrl: string;
}

interface ImportHistory {
  arquivo: string;
  data: string;
  itens: string;
  status: 'Concluido' | 'Erro';
}

@Component({
  selector: 'app-products',
  imports: [ReactiveFormsModule],
  templateUrl: './products.component.html',
  styleUrl: './products.component.scss',
})
export class ProductsComponent {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly fb = inject(FormBuilder);

  protected readonly showProductModal = signal(false);
  protected readonly showImportModal = signal(false);
  protected readonly categorias = signal<Categoria[]>([]);
  protected readonly marcas = signal<Marca[]>([]);

  protected readonly products = signal<CatalogProduct[]>([]);

  protected readonly metrics = computed<CatalogMetric[]>(() => {
    const products = this.products();
    const active = products.filter((product) => product.status === 'Ativo').length;
    const inactive = products.length - active;
    const brands = this.marcas().length;

    return [
      { icon: 'deployed_code', label: 'Total de produtos', value: String(products.length), delta: 'base atual', tone: 'success' },
      { icon: 'check_circle', label: 'Produtos ativos', value: String(active), delta: this.percentual(active, products.length), tone: 'success' },
      { icon: 'cancel', label: 'Produtos inativos', value: String(inactive), delta: this.percentual(inactive, products.length), tone: 'danger' },
      { icon: 'sell', label: 'Marcas cadastradas', value: String(brands), delta: 'base atual', tone: 'success' },
    ];
  });

  protected readonly imports: ImportHistory[] = [];

  protected readonly categoriasPai = computed(() => {
    return this.categorias()
      .filter((categoria) => categoria.ativo && categoria.categoriaPaiId === null)
      .sort((a, b) => a.ordem - b.ordem || a.nome.localeCompare(b.nome));
  });

  protected readonly subcategorias = computed(() => {
    const parentId = Number(this.productForm.controls.categoriaPaiId.value);
    if (!parentId) {
      return [];
    }

    return this.categorias()
      .filter((categoria) => categoria.ativo && categoria.categoriaPaiId === parentId)
      .sort((a, b) => a.ordem - b.ordem || a.nome.localeCompare(b.nome));
  });

  protected readonly productForm = this.fb.nonNullable.group({
    codigo: ['', [Validators.required]],
    codigoFabricante: ['', [Validators.required]],
    descricao: ['', [Validators.required]],
    marcaId: ['', [Validators.required]],
    categoriaPaiId: [''],
    categoriaId: ['', [Validators.required]],
    precoCompra: [0, [Validators.required, Validators.min(0)]],
    precoVenda: [0, [Validators.required, Validators.min(0)]],
    status: ['Ativo' as 'Ativo' | 'Inativo', [Validators.required]],
    imagemUrl: [''],
  });

  constructor() {
    this.carregarCategorias();
    this.carregarMarcas();
  }

  protected abrirNovoProduto(): void {
    this.productForm.reset({
      codigo: '',
      codigoFabricante: '',
      descricao: '',
      marcaId: '',
      categoriaPaiId: '',
      categoriaId: '',
      precoCompra: 0,
      precoVenda: 0,
      status: 'Ativo',
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

    const form = this.productForm.getRawValue();
    const categoria = this.categorias().find((item) => item.id === Number(form.categoriaId)) ?? null;
    const marca = this.marcas().find((item) => item.id === Number(form.marcaId)) ?? null;
    this.products.update((products) => [
      {
        codigo: form.codigo,
        codigoFabricante: form.codigoFabricante,
        descricao: form.descricao,
        marcaId: marca?.id ?? null,
        marca: marca?.nome ?? '',
        categoriaId: categoria?.id ?? null,
        categoria: categoria?.nome ?? '',
        precoCompra: form.precoCompra,
        precoVenda: form.precoVenda,
        status: form.status,
        imagemUrl: form.imagemUrl,
      },
      ...products,
    ]);
    this.showProductModal.set(false);
  }

  protected selecionarCategoriaPai(): void {
    this.productForm.patchValue({ categoriaId: '' });
  }

  protected abrirImportacao(): void {
    this.showImportModal.set(true);
  }

  protected fecharImportacao(): void {
    this.showImportModal.set(false);
  }

  protected formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  private carregarCategorias(): void {
    const tenantId = this.auth.usuario()?.empresaId ?? 1;
    this.api.listarCategorias(tenantId).subscribe({
      next: (categorias) => this.categorias.set(categorias),
    });
  }

  private carregarMarcas(): void {
    const tenantId = this.auth.usuario()?.empresaId ?? 1;
    this.api.listarMarcas(tenantId).subscribe({
      next: (marcas) => this.marcas.set(marcas),
    });
  }

  private percentual(valor: number, total: number): string {
    if (!total) {
      return '0% do total';
    }

    return `${Math.round((valor / total) * 100)}% do total`;
  }
}
