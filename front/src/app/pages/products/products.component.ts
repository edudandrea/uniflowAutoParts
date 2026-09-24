import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Categoria, Marca, Produto } from '../../core/app-models';
import { ApiService } from '../../core/api.service';

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
  ncm: number;
  impostos: string;
  aliquotaIpi: number;
  aliquotaIcms: number;
  aliquotaMva: number;
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
  private readonly fb = inject(FormBuilder);
  private readonly tenantId = 1;

  protected readonly showProductModal = signal(false);
  protected readonly showImportModal = signal(false);
  protected readonly categorias = signal<Categoria[]>([]);
  protected readonly marcas = signal<Marca[]>([]);
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly importStatus = signal('');
  protected readonly importing = signal(false);
  protected readonly currentPage = signal(1);
  protected readonly pageSize = signal(50);
  protected readonly totalProducts = signal(0);
  protected readonly totalActiveProducts = signal(0);
  protected readonly totalInactiveProducts = signal(0);
  protected readonly searchTerm = signal('');
  protected readonly loadingProducts = signal(false);

  protected readonly products = signal<CatalogProduct[]>([]);

  protected readonly metrics = computed<CatalogMetric[]>(() => {
    const total = this.totalProducts();
    const active = this.totalActiveProducts();
    const inactive = this.totalInactiveProducts();
    const brands = this.marcas().length;

    return [
      { icon: 'deployed_code', label: 'Total de produtos', value: String(total), delta: 'base atual', tone: 'success' },
      { icon: 'check_circle', label: 'Produtos ativos', value: String(active), delta: this.percentual(active, total), tone: 'success' },
      { icon: 'cancel', label: 'Produtos inativos', value: String(inactive), delta: this.percentual(inactive, total), tone: 'danger' },
      { icon: 'sell', label: 'Marcas cadastradas', value: String(brands), delta: 'base atual', tone: 'success' },
    ];
  });

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalProducts() / this.pageSize())));

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
    ncm: [0, [Validators.min(0)]],
    cest: [0, [Validators.min(0)]],
    aliquotaIpi: [0, [Validators.min(0)]],
    aliquotaIcms: [0, [Validators.min(0)]],
    aliquotaMva: [0, [Validators.min(0)]],
    status: ['Ativo' as 'Ativo' | 'Inativo', [Validators.required]],
    imagemUrl: [''],
  });

  protected readonly importForm = this.fb.nonNullable.group({
    stellantis: [true],
    atualizarPrecos: [true],
    margemPreco: [0, [Validators.min(0)]],
  });

  constructor() {
    this.carregarCategorias();
    this.carregarMarcas();
    this.carregarProdutos();
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
      ncm: 0,
      cest: 0,
      aliquotaIpi: 0,
      aliquotaIcms: 0,
      aliquotaMva: 0,
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
    const sku = Number(form.codigo);
    const codFabricante = Number(form.codigoFabricante);

    this.api.criarProduto({
      empresaId: this.tenantId,
      sku,
      codintern: sku,
      codfabricante: codFabricante,
      descricao: form.descricao,
      categoriaId: Number(form.categoriaId),
      marcaId: Number(form.marcaId),
      fabricanteId: Number(form.marcaId),
      custo: form.precoCompra,
      preco: form.precoVenda,
      estoqueMinimo: 0,
      estoqueMaximo: 0,
      ncm: form.ncm,
      cest: form.cest,
      origemMercadoria: null,
      aliquotaIpi: form.aliquotaIpi,
      aliquotaIcms: form.aliquotaIcms,
      aliquotaMva: form.aliquotaMva,
      impostosFabricante: null,
      ativo: form.status === 'Ativo',
    }).subscribe({
      next: (produto) => {
        this.totalProducts.update((total) => total + 1);
        this.totalActiveProducts.update((total) => produto.ativo ? total + 1 : total);
        this.totalInactiveProducts.update((total) => produto.ativo ? total : total + 1);
        this.products.update((products) => [this.toCatalogProduct(produto), ...products].slice(0, this.pageSize()));
        this.showProductModal.set(false);
      },
    });
  }

  protected selecionarCategoriaPai(): void {
    this.productForm.patchValue({ categoriaId: '' });
  }

  protected abrirImportacao(): void {
    this.importStatus.set('');
    this.showImportModal.set(true);
  }

  protected fecharImportacao(): void {
    this.showImportModal.set(false);
  }

  protected selecionarArquivo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const arquivo = input.files?.item(0) ?? null;
    this.selectedFile.set(arquivo);
    this.importStatus.set(arquivo ? `${arquivo.name} pronto para importar.` : '');
  }

  protected importarProdutos(): void {
    const arquivo = this.selectedFile();
    const form = this.importForm.getRawValue();

    if (!arquivo) {
      this.importStatus.set('Selecione um arquivo antes de importar.');
      return;
    }

    if (!form.stellantis) {
      this.importStatus.set('Selecione o fabricante Stellantis para este arquivo.');
      return;
    }

    this.importing.set(true);
    this.importStatus.set('Importando arquivo...');
    this.api.importarProdutosFabricante(this.tenantId, 'STELLANTIS', form.atualizarPrecos, arquivo).subscribe({
      next: (response) => {
        this.currentPage.set(1);
        this.carregarProdutos(1);
        this.carregarCategorias();
        this.carregarMarcas();
        this.importStatus.set(
          `${response.produtosCriados} criados, ${response.produtosAtualizados} atualizados, ${response.linhasIgnoradas} ignorados.`,
        );
        this.importing.set(false);
      },
      error: () => {
        this.importStatus.set('Nao foi possivel importar o arquivo.');
        this.importing.set(false);
      },
    });
  }

  protected formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  protected formatPercent(value: number): string {
    return `${value.toLocaleString('pt-BR', { maximumFractionDigits: 4 })}%`;
  }

  protected buscarProdutos(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.currentPage.set(1);
    this.carregarProdutos(1);
  }

  protected paginaAnterior(): void {
    if (this.currentPage() <= 1) {
      return;
    }

    const page = this.currentPage() - 1;
    this.currentPage.set(page);
    this.carregarProdutos(page);
  }

  protected proximaPagina(): void {
    if (this.currentPage() >= this.totalPages()) {
      return;
    }

    const page = this.currentPage() + 1;
    this.currentPage.set(page);
    this.carregarProdutos(page);
  }

  private carregarCategorias(): void {
    this.api.listarCategorias(this.tenantId).subscribe({
      next: (categorias) => this.categorias.set(categorias),
    });
  }

  private carregarMarcas(): void {
    this.api.listarMarcas(this.tenantId).subscribe({
      next: (marcas) => this.marcas.set(marcas),
    });
  }

  private carregarProdutos(page = this.currentPage()): void {
    this.loadingProducts.set(true);
    this.api.listarProdutos(this.tenantId, page, this.pageSize(), this.searchTerm()).subscribe({
      next: (response) => {
        this.products.set(response.produtos.map((produto) => this.toCatalogProduct(produto)));
        this.totalProducts.set(response.total);
        this.totalActiveProducts.set(response.totalAtivos);
        this.totalInactiveProducts.set(response.totalInativos);
        this.currentPage.set(response.pagina);
        this.loadingProducts.set(false);
      },
      error: () => this.loadingProducts.set(false),
    });
  }

  private toCatalogProduct(produto: Produto): CatalogProduct {
    return {
      codigo: String(produto.sku),
      codigoFabricante: String(produto.codfabricante),
      descricao: produto.descricao,
      marcaId: produto.marcaId,
      marca: produto.marca,
      categoriaId: produto.categoriaId,
      categoria: produto.categoria,
      precoCompra: produto.custo,
      precoVenda: produto.preco,
      ncm: produto.ncm,
      impostos: produto.impostosFabricante,
      aliquotaIpi: produto.aliquotaIpi,
      aliquotaIcms: produto.aliquotaIcms,
      aliquotaMva: produto.aliquotaMva,
      status: produto.ativo ? 'Ativo' : 'Inativo',
      imagemUrl: '',
    };
  }

  private percentual(valor: number, total: number): string {
    if (!total) {
      return '0% do total';
    }

    return `${Math.round((valor / total) * 100)}% do total`;
  }
}
