import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

interface Supplier {
  id: string;
  tipo: 'PJ' | 'PF';
  nome: string;
  nomeFantasia: string;
  documento: string;
  inscricaoEstadual: string;
  inscricaoMunicipal: string;
  suframa: string;
  cnae: string;
  segmento: string;
  email: string;
  telefone: string;
  celular: string;
  site: string;
  cidade: string;
  uf: string;
  endereco: string;
  bairro: string;
  cep: string;
  totalComprado: number;
  quantidadeNotas: number;
  ultimaCompra: string;
  prazoEntrega: number;
  status: 'Ativo' | 'Inativo';
  relacionamento: 'Fornecedor' | 'Ambos';
  categoria: string;
  marcas: string[];
  observacoes: string;
}

@Component({
  selector: 'app-suppliers',
  imports: [ReactiveFormsModule, AppIconComponent],
  templateUrl: './suppliers.component.html',
  styleUrl: './suppliers.component.scss',
})
export class SuppliersComponent {
  private readonly fb = inject(FormBuilder);

  protected readonly showSupplierModal = signal(false);
  protected readonly activePageTab = signal<'Pesquisa' | 'Detalhes'>('Pesquisa');
  protected readonly supplierSearch = signal('');
  protected readonly selectedSupplierId = signal('');

  protected readonly suppliers = signal<Supplier[]>([]);

  protected readonly filteredSuppliers = computed(() => {
    const term = this.supplierSearch().trim().toLocaleLowerCase('pt-BR');
    const suppliers = this.suppliers();

    if (!term) {
      return suppliers;
    }

    return suppliers.filter((supplier) =>
      [
        supplier.nome,
        supplier.nomeFantasia,
        supplier.documento,
        supplier.segmento,
        supplier.telefone,
        supplier.email,
        supplier.cidade,
        supplier.uf,
      ]
        .filter(Boolean)
        .join(' ')
        .toLocaleLowerCase('pt-BR')
        .includes(term)
    );
  });

  protected readonly selectedSupplier = computed(() =>
    this.suppliers().find((supplier) => supplier.id === this.selectedSupplierId()) ?? null
  );

  protected readonly metrics = computed(() => {
    const suppliers = this.suppliers();
    const ativos = suppliers.filter((supplier) => supplier.status === 'Ativo').length;
    const preferenciais = suppliers.filter((supplier) => supplier.prazoEntrega > 0).length;

    return [
      { icon: 'truck', label: 'Total de fornecedores', value: suppliers.length, detail: `${suppliers.filter((supplier) => supplier.relacionamento === 'Ambos').length} tambem clientes`, tone: '' },
      { icon: 'badge-check', label: 'Ativos', value: ativos, detail: this.percentual(ativos, suppliers.length), tone: '' },
      { icon: 'badge', label: 'Preferenciais', value: preferenciais, detail: 'base atual', tone: '' },
      { icon: 'info', label: 'Pendencias', value: 0, detail: 'documentos fiscais', tone: 'danger' },
    ];
  });

  protected readonly supplierForm = this.fb.nonNullable.group({
    tipo: ['PJ' as 'PJ' | 'PF', [Validators.required]],
    documento: ['', [Validators.required]],
    inscricaoEstadual: [''],
    inscricaoMunicipal: [''],
    razaoSocial: ['', [Validators.required]],
    nomeFantasia: [''],
    codigoFornecedor: [''],
    segmento: [''],
    ativo: [true],
    condicaoPagamento: [''],
    prazoEntrega: [0],
    preferencial: [true],
    limiteCredito: [0],
    indicadorIe: [''],
    regimeTributario: [''],
    suframa: [''],
    cnae: [''],
    simples: [true],
    observacoes: [''],
    tambemCliente: [true],
  });

  protected abrirNovoFornecedor(): void {
    this.supplierForm.reset({
      tipo: 'PJ',
      documento: '',
      inscricaoEstadual: '',
      inscricaoMunicipal: '',
      razaoSocial: '',
      nomeFantasia: '',
      codigoFornecedor: '',
      segmento: '',
      ativo: true,
      condicaoPagamento: '',
      prazoEntrega: 0,
      preferencial: true,
      limiteCredito: 0,
      indicadorIe: '',
      regimeTributario: '',
      suframa: '',
      cnae: '',
      simples: true,
      observacoes: '',
      tambemCliente: true,
    });
    this.showSupplierModal.set(true);
  }

  protected fecharNovoFornecedor(): void {
    this.showSupplierModal.set(false);
  }

  protected salvarFornecedor(): void {
    if (this.supplierForm.invalid) {
      this.supplierForm.markAllAsTouched();
      return;
    }

    const form = this.supplierForm.getRawValue();
    const supplier: Supplier = {
      id: String(Date.now()),
      tipo: form.tipo,
      nome: form.razaoSocial,
      nomeFantasia: form.nomeFantasia || form.razaoSocial,
      documento: form.documento,
      inscricaoEstadual: form.inscricaoEstadual || '-',
      inscricaoMunicipal: form.inscricaoMunicipal || '-',
      suframa: form.suframa || '-',
      cnae: form.cnae || '-',
      segmento: form.segmento || 'Nao informado',
      email: 'comercial@fornecedor.com.br',
      telefone: '-',
      celular: '-',
      site: '-',
      cidade: '-',
      uf: '-',
      endereco: 'Endereco nao informado',
      bairro: '-',
      cep: '-',
      totalComprado: 0,
      quantidadeNotas: 0,
      ultimaCompra: '-',
      prazoEntrega: form.prazoEntrega,
      status: form.ativo ? 'Ativo' : 'Inativo',
      relacionamento: form.tambemCliente ? 'Ambos' : 'Fornecedor',
      categoria: form.segmento || 'Nao informado',
      marcas: ['Bosch', 'Mann Filter'],
      observacoes: form.observacoes || 'Fornecedor cadastrado para compras, pedidos e recebimento de notas fiscais.',
    };

    this.suppliers.update((suppliers) => [supplier, ...suppliers]);
    this.selectedSupplierId.set(supplier.id);
    this.activePageTab.set('Detalhes');
    this.showSupplierModal.set(false);
  }

  protected selecionarFornecedor(id: string): void {
    this.selectedSupplierId.set(id);
    this.activePageTab.set('Detalhes');
  }

  protected abrirPesquisa(): void {
    this.activePageTab.set('Pesquisa');
  }

  protected abrirDetalhes(): void {
    if (this.selectedSupplier()) {
      this.activePageTab.set('Detalhes');
    }
  }

  protected initials(nome: string): string {
    return nome
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  protected formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  private percentual(valor: number, total: number): string {
    if (!total) {
      return '0% da base';
    }

    return `${Math.round((valor / total) * 100)}% da base`;
  }
}
