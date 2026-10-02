import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Marca } from '../../core/app-models';
import { ApiService } from '../../core/api.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

@Component({
  selector: 'app-brands',
  imports: [ReactiveFormsModule, AppIconComponent],
  templateUrl: './brands.component.html',
  styleUrl: './brands.component.scss',
})
export class BrandsComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly tenantId = 1;

  protected readonly marcas = signal<Marca[]>([]);
  protected readonly busca = signal('');
  protected readonly showBrandModal = signal(false);
  protected readonly salvando = signal(false);
  protected readonly enviandoLogo = signal(false);
  protected readonly mensagem = signal('');
  protected readonly marcaSelecionada = signal<Marca | null>(null);
  protected readonly logoNomeArquivo = signal('');
  protected readonly logoPreview = signal('');

  protected readonly marcasFiltradas = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    const marcas = [...this.marcas()].sort((a, b) => a.nome.localeCompare(b.nome));

    if (!termo) {
      return marcas;
    }

    return marcas.filter((marca) => {
      return (
        marca.nome.toLowerCase().includes(termo) ||
        (marca.codigo?.toLowerCase().includes(termo) ?? false) ||
        (marca.descricao?.toLowerCase().includes(termo) ?? false) ||
        (marca.site?.toLowerCase().includes(termo) ?? false)
      );
    });
  });

  protected readonly metrics = computed(() => {
    const marcas = this.marcas();
    const ativas = marcas.filter((marca) => marca.ativo).length;
    const comLogo = marcas.filter((marca) => !!marca.logoUrl).length;
    const produtos = marcas.reduce((total, marca) => total + marca.produtos, 0);

    return [
      { icon: 'badge', label: 'Total de marcas', value: marcas.length, detail: 'base atual' },
      { icon: 'badge-check', label: 'Marcas ativas', value: ativas, detail: this.percentual(ativas, marcas.length) },
      { icon: 'eye', label: 'Com logo', value: comLogo, detail: this.percentual(comLogo, marcas.length) },
      { icon: 'package', label: 'Produtos vinculados', value: produtos, detail: 'base atual' },
    ];
  });

  protected readonly marcaForm = this.fb.nonNullable.group({
    nome: ['', [Validators.required]],
    codigo: [''],
    descricao: [''],
    logoUrl: [''],
    site: [''],
    observacao: [''],
    ativo: [true],
  });

  constructor() {
    this.carregarMarcas();
  }

  protected abrirNovaMarca(): void {
    this.mensagem.set('');
    this.marcaSelecionada.set(null);
    this.logoNomeArquivo.set('');
    this.logoPreview.set('');
    this.marcaForm.reset({
      nome: '',
      codigo: '',
      descricao: '',
      logoUrl: '',
      site: '',
      observacao: '',
      ativo: true,
    });
    this.showBrandModal.set(true);
  }

  protected abrirEdicaoMarca(marca: Marca): void {
    this.mensagem.set('');
    this.marcaSelecionada.set(marca);
    this.logoNomeArquivo.set('');
    this.logoPreview.set(marca.logoUrl ?? '');
    this.marcaForm.reset({
      nome: marca.nome,
      codigo: marca.codigo ?? '',
      descricao: marca.descricao ?? '',
      logoUrl: marca.logoUrl ?? '',
      site: marca.site ?? '',
      observacao: marca.observacao ?? '',
      ativo: marca.ativo,
    });
    this.showBrandModal.set(true);
  }

  protected fecharNovaMarca(): void {
    this.showBrandModal.set(false);
    this.marcaSelecionada.set(null);
    this.logoNomeArquivo.set('');
    this.logoPreview.set('');
  }

  protected salvarMarca(): void {
    if (this.marcaForm.invalid) {
      this.marcaForm.markAllAsTouched();
      return;
    }

    const form = this.marcaForm.getRawValue();
    this.salvando.set(true);
    this.mensagem.set('');

    const payload = {
      tenantId: this.tenantId,
      nome: form.nome,
      codigo: form.codigo || null,
      descricao: form.descricao || null,
      logoUrl: form.logoUrl || null,
      site: form.site || null,
      observacao: form.observacao || null,
      ativo: form.ativo,
    };

    const marcaAtual = this.marcaSelecionada();
    const request = marcaAtual ? this.api.atualizarMarca(marcaAtual.id, payload) : this.api.criarMarca(payload);

    request.subscribe({
      next: (marca) => {
        if (marcaAtual) {
          this.marcas.update((marcas) => marcas.map((item) => (item.id === marca.id ? marca : item)));
        } else {
          this.marcas.update((marcas) => [marca, ...marcas]);
        }

        this.salvando.set(false);
        this.showBrandModal.set(false);
        this.marcaSelecionada.set(null);
        this.logoNomeArquivo.set('');
      },
      error: (error) => {
        this.mensagem.set(error?.error || 'Nao foi possivel salvar a marca.');
        this.salvando.set(false);
      },
    });
  }

  protected selecionarLogo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    if (!file.type.startsWith('image/')) {
      this.mensagem.set('Selecione um arquivo de imagem para a logo.');
      input.value = '';
      return;
    }

    this.logoNomeArquivo.set(file.name);
    this.logoPreview.set(URL.createObjectURL(file));
    this.enviandoLogo.set(true);
    this.mensagem.set('');

    this.api.enviarLogoMarca(this.tenantId, file).subscribe({
      next: (documento) => {
        this.marcaForm.controls.logoUrl.setValue(documento.url);
        this.logoPreview.set(documento.url);
        this.enviandoLogo.set(false);
      },
      error: (error) => {
        this.mensagem.set(error?.error || 'Nao foi possivel enviar a logo.');
        this.logoPreview.set(this.marcaForm.controls.logoUrl.value);
        this.logoNomeArquivo.set('');
        this.enviandoLogo.set(false);
        input.value = '';
      },
    });
  }

  protected removerLogo(): void {
    this.marcaForm.controls.logoUrl.setValue('');
    this.logoNomeArquivo.set('');
    this.logoPreview.set('');
  }

  protected campoInvalido(campo: keyof typeof this.marcaForm.controls): boolean {
    const control = this.marcaForm.controls[campo];
    return control.invalid && (control.touched || control.dirty);
  }

  protected initials(nome: string): string {
    return nome
      .split(' ')
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  private carregarMarcas(): void {
    this.api.listarMarcas(this.tenantId, true).subscribe({
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
