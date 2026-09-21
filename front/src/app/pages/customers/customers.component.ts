import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { CadastroEndereco } from '../../core/app-models';
import { ApiService } from '../../core/api.service';
import { AuthSessionService } from '../../core/auth-session.service';

interface CustomerMetric {
  icon: string;
  label: string;
  value: string;
  delta: string;
  tone: 'success' | 'danger';
}

interface Customer {
  id: string;
  nome: string;
  subtitulo: string;
  tipo: 'PJ' | 'PF';
  documento: string;
  telefone: string;
  email: string;
  site: string;
  cidade: string;
  uf: string;
  ultimaCompra: string;
  totalComprado: number;
  status: 'Ativo' | 'Inativo';
  inscricaoEstadual: string;
  inscricaoMunicipal: string;
  razaoSocial: string;
  nomeFantasia: string;
  logradouro: string;
  bairro: string;
  cep: string;
  observacoes: string;
  relacionamento: 'Cliente' | 'Fornecedor' | 'Ambos';
}

@Component({
  selector: 'app-customers',
  imports: [ReactiveFormsModule],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss',
})
export class CustomersComponent {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly fb = inject(FormBuilder);

  protected readonly showCustomerModal = signal(false);
  protected readonly selectedCustomerId = signal('');
  protected readonly activeCustomerModalTab = signal('Dados gerais');
  protected readonly savingCustomer = signal(false);
  protected readonly customerMessage = signal('');
  protected readonly customerModalTabs = ['Dados gerais', 'Enderecos', 'Contatos', 'Dados comerciais', 'Observacoes'];
  protected readonly enderecos = signal<CadastroEndereco[]>([]);

  protected readonly customers = signal<Customer[]>([]);

  protected readonly selectedCustomer = computed(() => {
    return this.customers().find((customer) => customer.id === this.selectedCustomerId()) ?? null;
  });

  protected readonly enderecosFiltrados = computed(() => {
    const termo = this.customerForm.controls.enderecoBusca.value.trim().toLowerCase();
    if (!termo) {
      return this.enderecos().filter((endereco) => endereco.ativo);
    }

    const cep = termo.replace(/\D/g, '');
    return this.enderecos().filter((endereco) => {
      return (
        endereco.ativo &&
        (endereco.nome.toLowerCase().includes(termo) ||
          endereco.logradouro.toLowerCase().includes(termo) ||
          endereco.bairro.toLowerCase().includes(termo) ||
          endereco.municipio.toLowerCase().includes(termo) ||
          (cep && endereco.cep.includes(cep)))
      );
    });
  });

  protected readonly enderecoSelecionado = computed(() => {
    const id = this.customerForm.controls.enderecoId.value;
    return this.enderecos().find((endereco) => endereco.id === id) ?? null;
  });

  protected readonly metrics = computed<CustomerMetric[]>(() => {
    const customers = this.customers();
    const active = customers.filter((customer) => customer.status === 'Ativo').length;
    const inactive = customers.length - active;

    return [
      { icon: 'groups', label: 'Total de clientes', value: this.formatNumber(customers.length), delta: 'base atual', tone: 'success' },
      { icon: 'donut_large', label: 'Clientes ativos', value: this.formatNumber(active), delta: this.percentual(active, customers.length), tone: 'success' },
      { icon: 'group_add', label: 'Novos clientes (mes)', value: '0', delta: 'base atual', tone: 'success' },
      { icon: 'cancel', label: 'Clientes inativos', value: String(inactive), delta: this.percentual(inactive, customers.length), tone: 'danger' },
    ];
  });

  protected readonly customerForm = this.fb.nonNullable.group({
    nome: ['', [Validators.required]],
    subtitulo: [''],
    tipo: ['PF' as 'PJ' | 'PF', [Validators.required]],
    documento: ['', [Validators.required]],
    nascimento: [''],
    rg: [''],
    orgaoEmissor: ['SSP'],
    estadoCivil: [''],
    profissao: [''],
    indicadorIe: ['Nao contribuinte'],
    telefone: [''],
    celular: [''],
    email: [''],
    cnh: [''],
    clienteDesde: [''],
    origem: [''],
    vendedorPadrao: [''],
    ativo: [true],
    cidade: [''],
    uf: ['RS'],
    razaoSocial: [''],
    nomeFantasia: [''],
    logradouro: [''],
    bairro: [''],
    cep: [''],
    observacoes: [''],
    relacionamento: ['Cliente' as 'Cliente' | 'Fornecedor' | 'Ambos'],
    enderecoId: [null as number | null],
    enderecoBusca: [''],
    enderecoCep: [''],
    enderecoLogradouro: [''],
    enderecoNumero: [''],
    enderecoComplemento: [''],
    enderecoBairro: [''],
    enderecoCidade: [''],
    enderecoUf: ['RS'],
    contatoNome: [''],
    contatoCargo: [''],
    contatoEmail: [''],
    contatoTelefone: [''],
    contatoCelular: [''],
    condicaoPagamento: [''],
    limiteCredito: [0],
    tabelaPreco: [''],
    bloquearVenda: [false],
    permiteFiado: [true],
  });

  constructor() {
    this.carregarEnderecos();
  }

  protected abrirNovoCliente(): void {
    this.customerForm.reset({
      nome: '',
      subtitulo: '',
      tipo: 'PF',
      documento: '',
      nascimento: '',
      rg: '',
      orgaoEmissor: 'SSP',
      estadoCivil: '',
      profissao: '',
      indicadorIe: 'Nao contribuinte',
      telefone: '',
      celular: '',
      email: '',
      cnh: '',
      clienteDesde: '',
      origem: '',
      vendedorPadrao: '',
      ativo: true,
      cidade: '',
      uf: 'RS',
      razaoSocial: '',
      nomeFantasia: '',
      logradouro: '',
      bairro: '',
      cep: '',
      observacoes: '',
      relacionamento: 'Cliente',
      enderecoId: null,
      enderecoBusca: '',
      enderecoCep: '',
      enderecoLogradouro: '',
      enderecoNumero: '',
      enderecoComplemento: '',
      enderecoBairro: '',
      enderecoCidade: '',
      enderecoUf: 'RS',
      contatoNome: '',
      contatoCargo: '',
      contatoEmail: '',
      contatoTelefone: '',
      contatoCelular: '',
      condicaoPagamento: '',
      limiteCredito: 0,
      tabelaPreco: '',
      bloquearVenda: false,
      permiteFiado: true,
    });
    this.customerMessage.set('');
    this.activeCustomerModalTab.set('Dados gerais');
    this.carregarEnderecos();
    this.showCustomerModal.set(true);
  }

  protected fecharNovoCliente(): void {
    this.showCustomerModal.set(false);
  }

  protected cadastrarCliente(): void {
    if (this.customerForm.invalid) {
      this.customerForm.markAllAsTouched();
      return;
    }

    const form = this.customerForm.getRawValue();
    const empresaId = this.auth.usuario()?.empresaId ?? 1;
    const id = String(Date.now());
    const customer: Customer = {
      id,
      nome: form.nome,
      subtitulo: form.subtitulo || (form.tipo === 'PJ' ? 'Cliente pessoa juridica' : 'Cliente pessoa fisica'),
      tipo: form.tipo,
      documento: form.documento,
      telefone: form.telefone,
      email: form.email,
      site: '',
      cidade: form.cidade,
      uf: form.uf,
      ultimaCompra: '-',
      totalComprado: 0,
      status: form.ativo ? 'Ativo' : 'Inativo',
      inscricaoEstadual: '',
      inscricaoMunicipal: '',
      razaoSocial: form.razaoSocial || form.nome,
      nomeFantasia: form.nomeFantasia || form.nome,
      logradouro: form.logradouro,
      bairro: form.bairro,
      cep: form.cep,
      observacoes: form.observacoes,
      relacionamento: form.relacionamento,
    };

    const enderecoSelecionado = this.enderecoSelecionado();
    if (enderecoSelecionado) {
      customer.logradouro = `${enderecoSelecionado.logradouro}, ${enderecoSelecionado.numero}`;
      customer.bairro = enderecoSelecionado.bairro;
      customer.cep = this.formatCep(enderecoSelecionado.cep);
      customer.cidade = enderecoSelecionado.municipio;
      customer.uf = enderecoSelecionado.uf;
    }

    this.savingCustomer.set(true);
    this.customerMessage.set('');

    this.api
      .criarCliente({
        empresaId,
        tipoPessoa: form.tipo === 'PJ' ? 2 : 1,
        relacionamento: form.relacionamento === 'Fornecedor' ? 2 : form.relacionamento === 'Ambos' ? 3 : 1,
        nomeRazaoSocial: form.razaoSocial || form.nome,
        nomeFantasia: form.nomeFantasia || null,
        cpfCnpj: form.documento,
        rg: form.rg || null,
        orgaoEmissor: form.orgaoEmissor || null,
        estadoCivil: form.estadoCivil || null,
        dataNascimento: form.nascimento || null,
        profissao: form.profissao || null,
        cnh: form.cnh || null,
        inscricaoEstadual: null,
        inscricaoMunicipal: null,
        indicadorIe: form.indicadorIe === 'Contribuinte ICMS' ? 1 : form.indicadorIe === 'Isento' ? 2 : 9,
        consumidorFinal: true,
        email: form.email || null,
        telefone: form.telefone || null,
        celular: form.celular || null,
        clienteDesde: form.clienteDesde || null,
        origem: form.origem || null,
        vendedorPadrao: form.vendedorPadrao || null,
        limiteCredito: form.limiteCredito || null,
        observacao: form.observacoes || null,
        ativo: form.ativo,
        enderecoId: form.enderecoId,
        endereco: null,
        contatos: form.contatoNome
          ? [
              {
                nome: form.contatoNome,
                cargo: form.contatoCargo || null,
                email: form.contatoEmail || null,
                telefone: form.contatoTelefone || null,
                celular: form.contatoCelular || null,
                principal: true,
              },
            ]
          : [],
        dadosComerciais: {
          condicaoPagamento: form.condicaoPagamento || null,
          limiteCredito: form.limiteCredito || null,
          tabelaPreco: form.tabelaPreco || null,
          vendedorPadrao: form.vendedorPadrao || null,
          origem: form.origem || null,
          bloquearVenda: form.bloquearVenda,
          permiteFiado: form.permiteFiado,
        },
      })
      .subscribe({
        next: (response) => {
          customer.id = String(response.id);
          this.customers.update((customers) => [customer, ...customers]);
          this.selectedCustomerId.set(customer.id);
          this.savingCustomer.set(false);
          this.showCustomerModal.set(false);
        },
        error: (error) => {
          this.customerMessage.set(error?.error || 'Nao foi possivel cadastrar o cliente.');
          this.savingCustomer.set(false);
        },
      });
  }

  protected selecionarCliente(id: string): void {
    this.selectedCustomerId.set(id);
  }

  protected selecionarEndereco(endereco: CadastroEndereco): void {
    this.customerForm.patchValue({ enderecoId: endereco.id, enderecoBusca: endereco.logradouro });
  }

  protected initials(name: string): string {
    return name
      .split(' ')
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  protected formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  protected formatNumber(value: number): string {
    return value.toLocaleString('pt-BR');
  }

  protected formatCep(cep: string): string {
    const clean = cep.replace(/\D/g, '');
    return clean.length === 8 ? `${clean.slice(0, 5)}-${clean.slice(5)}` : cep;
  }

  private percentual(valor: number, total: number): string {
    if (!total) {
      return '0% do total';
    }

    return `${Math.round((valor / total) * 100)}% do total`;
  }

  private carregarEnderecos(): void {
    const empresaId = this.auth.usuario()?.empresaId ?? 1;
    this.api.listarEnderecos(empresaId).subscribe({
      next: (enderecos) => {
        this.enderecos.set(enderecos);
      },
    });
  }
}
