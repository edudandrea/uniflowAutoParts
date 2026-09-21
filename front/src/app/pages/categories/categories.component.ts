import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Categoria } from '../../core/app-models';
import { ApiService } from '../../core/api.service';
import { AuthSessionService } from '../../core/auth-session.service';

@Component({
  selector: 'app-categories',
  imports: [ReactiveFormsModule],
  templateUrl: './categories.component.html',
  styleUrl: './categories.component.scss',
})
export class CategoriesComponent {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly fb = inject(FormBuilder);

  protected readonly categorias = signal<Categoria[]>([]);
  protected readonly busca = signal('');
  protected readonly showCategoryModal = signal(false);
  protected readonly salvando = signal(false);
  protected readonly mensagem = signal('');

  protected readonly iconOptions = ['category', 'album', 'settings', 'filter_alt', 'build', 'bolt', 'water_drop', 'directions_car'];

  protected readonly categoriasPai = computed(() => {
    return this.categorias()
      .filter((categoria) => categoria.ativo && categoria.categoriaPaiId === null)
      .sort((a, b) => a.ordem - b.ordem || a.nome.localeCompare(b.nome));
  });

  protected readonly categoriasFiltradas = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    const categorias = [...this.categorias()].sort((a, b) => {
      const parentCompare = (a.categoriaPaiNome ?? '').localeCompare(b.categoriaPaiNome ?? '');
      return parentCompare || a.ordem - b.ordem || a.nome.localeCompare(b.nome);
    });

    if (!termo) {
      return categorias;
    }

    return categorias.filter((categoria) => {
      return (
        categoria.nome.toLowerCase().includes(termo) ||
        (categoria.descricao?.toLowerCase().includes(termo) ?? false) ||
        (categoria.categoriaPaiNome?.toLowerCase().includes(termo) ?? false)
      );
    });
  });

  protected readonly metrics = computed(() => {
    const categorias = this.categorias();
    const principais = categorias.filter((categoria) => categoria.categoriaPaiId === null).length;
    const subcategorias = categorias.length - principais;
    const ativas = categorias.filter((categoria) => categoria.ativo).length;
    const produtos = categorias.reduce((total, categoria) => total + categoria.produtos, 0);

    return [
      { icon: 'category', label: 'Total de categorias', value: categorias.length, detail: 'base atual' },
      { icon: 'account_tree', label: 'Categorias principais', value: principais, detail: `${subcategorias} subcategorias` },
      { icon: 'verified', label: 'Ativas', value: ativas, detail: this.percentual(ativas, categorias.length) },
      { icon: 'deployed_code', label: 'Produtos vinculados', value: produtos, detail: 'categorias finais' },
    ];
  });

  protected readonly categoriaForm = this.fb.nonNullable.group({
    nome: ['', [Validators.required]],
    categoriaPaiId: [''],
    descricao: [''],
    icone: ['category'],
    ordem: [10],
    ativo: [true],
  });

  constructor() {
    this.carregarCategorias();
  }

  protected abrirNovaCategoria(): void {
    this.mensagem.set('');
    this.categoriaForm.reset({
      nome: '',
      categoriaPaiId: '',
      descricao: '',
      icone: 'category',
      ordem: 10,
      ativo: true,
    });
    this.showCategoryModal.set(true);
  }

  protected fecharNovaCategoria(): void {
    this.showCategoryModal.set(false);
  }

  protected salvarCategoria(): void {
    if (this.categoriaForm.invalid) {
      this.categoriaForm.markAllAsTouched();
      return;
    }

    const form = this.categoriaForm.getRawValue();
    this.salvando.set(true);
    this.mensagem.set('');

    this.api
      .criarCategoria({
        tenantId: this.tenantId(),
        nome: form.nome,
        descricao: form.descricao || null,
        categoriaPaiId: form.categoriaPaiId ? Number(form.categoriaPaiId) : null,
        icone: form.icone || null,
        ordem: form.ordem,
        ativo: form.ativo,
      })
      .subscribe({
        next: (categoria) => {
          this.categorias.update((categorias) => [categoria, ...categorias]);
          this.salvando.set(false);
          this.showCategoryModal.set(false);
        },
        error: (error) => {
          this.mensagem.set(error?.error || 'Nao foi possivel cadastrar a categoria.');
          this.salvando.set(false);
        },
      });
  }

  protected campoInvalido(campo: keyof typeof this.categoriaForm.controls): boolean {
    const control = this.categoriaForm.controls[campo];
    return control.invalid && (control.touched || control.dirty);
  }

  private carregarCategorias(): void {
    this.api.listarCategorias(this.tenantId(), true).subscribe({
      next: (categorias) => this.categorias.set(categorias),
    });
  }

  private tenantId(): number {
    return this.auth.usuario()?.empresaId ?? 1;
  }

  private percentual(valor: number, total: number): string {
    if (!total) {
      return '0% do total';
    }

    return `${Math.round((valor / total) * 100)}% do total`;
  }
}
