import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

interface Supplier {
  id: string;
  nome: string;
  documento: string;
  segmento: string;
  telefone: string;
  cidade: string;
  totalComprado: number;
  prazoEntrega: number;
  status: 'Ativo' | 'Inativo';
  relacionamento: 'Fornecedor' | 'Ambos';
}

@Component({
  selector: 'app-suppliers',
  imports: [ReactiveFormsModule],
  templateUrl: './suppliers.component.html',
  styleUrl: './suppliers.component.scss',
})
export class SuppliersComponent {
  private readonly fb = inject(FormBuilder);

  protected readonly showSupplierModal = signal(false);

  protected readonly suppliers = signal<Supplier[]>([]);

  protected readonly metrics = computed(() => {
    const suppliers = this.suppliers();
    const ativos = suppliers.filter((supplier) => supplier.status === 'Ativo').length;
    const preferenciais = suppliers.filter((supplier) => supplier.prazoEntrega > 0).length;

    return [
      { icon: 'apartment', label: 'Total de fornecedores', value: suppliers.length, detail: `${suppliers.filter((supplier) => supplier.relacionamento === 'Ambos').length} tambem clientes`, tone: '' },
      { icon: 'verified', label: 'Ativos', value: ativos, detail: this.percentual(ativos, suppliers.length), tone: '' },
      { icon: 'star', label: 'Preferenciais', value: preferenciais, detail: 'base atual', tone: '' },
      { icon: 'warning', label: 'Pendencias', value: 0, detail: 'documentos fiscais', tone: 'danger' },
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
    this.suppliers.update((suppliers) => [
      {
        id: String(Date.now()),
        nome: form.nomeFantasia || form.razaoSocial,
        documento: form.documento,
        segmento: form.segmento || 'Nao informado',
        telefone: '-',
        cidade: '-',
        totalComprado: 0,
        prazoEntrega: form.prazoEntrega,
        status: form.ativo ? 'Ativo' : 'Inativo',
        relacionamento: form.tambemCliente ? 'Ambos' : 'Fornecedor',
      },
      ...suppliers,
    ]);
    this.showSupplierModal.set(false);
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
