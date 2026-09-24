import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { CadastroEndereco } from '../../core/app-models';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-addresses',
  imports: [ReactiveFormsModule],
  templateUrl: './addresses.component.html',
  styleUrl: './addresses.component.scss',
})
export class AddressesComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly empresaId = 1;

  protected readonly enderecos = signal<CadastroEndereco[]>([]);
  protected readonly busca = signal('');
  protected readonly salvando = signal(false);
  protected readonly mensagem = signal('');
  protected readonly activeTab = signal<'pesquisa' | 'cadastro'>('pesquisa');
  protected readonly showAddressModal = signal(false);
  protected readonly tipoEnderecoFiltro = signal('Todos');
  protected readonly cidadeFiltro = signal('Todas');
  protected readonly statusFiltro = signal('Todos');

  protected readonly metrics = computed(() => {
    const enderecos = this.enderecos();
    const total = enderecos.length;
    const principal = enderecos.filter((endereco) => this.isPrincipal(endereco)).length;
    const entrega = enderecos.filter((endereco) => this.tipoPorEndereco(endereco) === 'Entrega').length;
    const cobranca = enderecos.filter((endereco) => this.tipoPorEndereco(endereco) === 'Cobranca').length;

    return [
      { icon: 'description', label: 'Total de enderecos', value: total, detail: this.percentual(total, total) },
      { icon: 'home', label: 'Enderecos principais', value: principal, detail: this.percentual(principal, total) },
      { icon: 'local_shipping', label: 'Enderecos de entrega', value: entrega, detail: this.percentual(entrega, total) },
      { icon: 'receipt_long', label: 'Enderecos de cobranca', value: cobranca, detail: this.percentual(cobranca, total) },
    ];
  });

  protected readonly cidades = computed(() => {
    return Array.from(new Set(this.enderecos().map((endereco) => endereco.municipio).filter(Boolean))).sort();
  });

  protected readonly enderecosFiltrados = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    const cep = termo.replace(/\D/g, '');
    return this.enderecos().filter((endereco) => {
      const tipo = this.tipoPorEndereco(endereco);
      const matchesSearch =
        !termo ||
        endereco.nome.toLowerCase().includes(termo) ||
        endereco.logradouro.toLowerCase().includes(termo) ||
        endereco.bairro.toLowerCase().includes(termo) ||
        endereco.municipio.toLowerCase().includes(termo) ||
        (cep && endereco.cep.includes(cep));

      return (
        matchesSearch &&
        (this.tipoEnderecoFiltro() === 'Todos' || tipo === this.tipoEnderecoFiltro()) &&
        (this.cidadeFiltro() === 'Todas' || endereco.municipio === this.cidadeFiltro()) &&
        (this.statusFiltro() === 'Todos' || (this.statusFiltro() === 'Ativo' ? endereco.ativo : !endereco.ativo))
      );
    });
  });

  protected readonly enderecoForm = this.fb.nonNullable.group({
    fornecedor: [''],
    tipoEndereco: ['', [Validators.required]],
    nome: [''],
    cep: ['', [Validators.required]],
    logradouro: ['', [Validators.required]],
    numero: ['', [Validators.required]],
    complemento: [''],
    bairro: ['', [Validators.required]],
    codigoIbgeMunicipio: ['0000000'],
    municipio: ['', [Validators.required]],
    uf: ['RS', [Validators.required]],
    pais: ['Brasil'],
    referencia: [''],
    enderecoPrincipal: [true],
    recebimentoMercadorias: [false],
    entrega: [true],
    correspondencias: [false],
    ativo: [true],
  });

  constructor() {
    this.carregarEnderecos();
  }

  protected carregarEnderecos(): void {
    this.api.listarEnderecos(this.empresaId).subscribe({
      next: (enderecos) => {
        this.enderecos.set(enderecos);
      },
    });
  }

  protected limparFiltros(): void {
    this.busca.set('');
    this.tipoEnderecoFiltro.set('Todos');
    this.cidadeFiltro.set('Todas');
    this.statusFiltro.set('Todos');
  }

  protected abrirNovoEndereco(): void {
    this.mensagem.set('');
    this.enderecoForm.reset({
      nome: '',
      fornecedor: '',
      tipoEndereco: '',
      cep: '',
      logradouro: '',
      numero: '',
      complemento: '',
      bairro: '',
      codigoIbgeMunicipio: '0000000',
      municipio: '',
      uf: 'RS',
      pais: 'Brasil',
      referencia: '',
      enderecoPrincipal: true,
      recebimentoMercadorias: false,
      entrega: true,
      correspondencias: false,
      ativo: true,
    });
    this.showAddressModal.set(true);
  }

  protected fecharNovoEndereco(): void {
    this.showAddressModal.set(false);
  }

  protected salvarEndereco(): void {
    if (this.enderecoForm.invalid) {
      this.enderecoForm.markAllAsTouched();
      return;
    }

    const form = this.enderecoForm.getRawValue();
    this.salvando.set(true);
    this.mensagem.set('');

    this.api
      .criarEndereco({
        empresaId: this.empresaId,
        nome: form.nome || form.tipoEndereco || `${form.logradouro}, ${form.numero}`,
        cep: form.cep,
        logradouro: form.logradouro,
        numero: form.numero,
        complemento: form.complemento || null,
        bairro: form.bairro,
        codigoIbgeMunicipio: form.codigoIbgeMunicipio || null,
        municipio: form.municipio,
        uf: form.uf,
        pais: form.pais || 'Brasil',
        referencia:
          form.referencia ||
          [
            form.tipoEndereco,
            form.enderecoPrincipal ? 'Endereco principal' : '',
            form.recebimentoMercadorias ? 'Recebimento de mercadorias' : '',
            form.entrega ? 'Entrega' : '',
            form.correspondencias ? 'Correspondencias' : '',
          ]
            .filter(Boolean)
            .join(' | ') ||
          null,
        ativo: form.ativo,
      })
      .subscribe({
        next: (endereco) => {
          this.enderecos.update((enderecos) => [endereco, ...enderecos]);
          this.enderecoForm.reset({
            nome: '',
            fornecedor: '',
            tipoEndereco: '',
            cep: '',
            logradouro: '',
            numero: '',
            complemento: '',
            bairro: '',
            codigoIbgeMunicipio: '0000000',
            municipio: '',
            uf: 'RS',
            pais: 'Brasil',
            referencia: '',
            enderecoPrincipal: true,
            recebimentoMercadorias: false,
            entrega: true,
            correspondencias: false,
            ativo: true,
          });
          this.mensagem.set('Endereco cadastrado com sucesso.');
          this.salvando.set(false);
          this.showAddressModal.set(false);
          this.activeTab.set('pesquisa');
        },
        error: (error) => {
          this.mensagem.set(error?.error || 'Nao foi possivel cadastrar o endereco.');
          this.salvando.set(false);
        },
      });
  }

  protected formatCep(cep: string): string {
    const clean = cep.replace(/\D/g, '');
    return clean.length === 8 ? `${clean.slice(0, 5)}-${clean.slice(5)}` : cep;
  }

  protected tipoPorEndereco(endereco: CadastroEndereco): string {
    const referencia = endereco.referencia?.toLowerCase() ?? '';
    if (referencia.includes('cobranca')) {
      return 'Cobranca';
    }
    if (referencia.includes('entrega')) {
      return 'Entrega';
    }
    if (referencia.includes('distribuicao')) {
      return 'Centro de Distribuicao';
    }
    return endereco.nome || 'Matriz';
  }

  protected isPrincipal(endereco: CadastroEndereco): boolean {
    return endereco.nome.toLowerCase().includes('matriz') || (endereco.referencia?.toLowerCase().includes('principal') ?? false);
  }

  protected campoInvalido(campo: keyof typeof this.enderecoForm.controls): boolean {
    const control = this.enderecoForm.controls[campo];
    return control.invalid && (control.touched || control.dirty);
  }

  private percentual(valor: number, total: number): string {
    if (!total) {
      return '0% do total';
    }

    return `${Math.round((valor / total) * 100)}% do total`;
  }
}
