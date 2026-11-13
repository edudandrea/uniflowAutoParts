import { SessionService } from '../../core/session.service';
import { Component, computed, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CadastroEndereco, ClienteResumoResponse, ClienteDetalheResponse, CriarClientePayload } from '../../core/app-models';
import { ApiService } from '../../core/api.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

@Component({ selector: 'app-customers', imports: [ReactiveFormsModule, AppIconComponent], templateUrl: './customers.component.html', styleUrls: ['./customers.component.scss', './customer-display.scss'] })
export class CustomersComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private get empresaId(): number { return this.session.empresaId; }
  private readonly customerDialog = viewChild<ElementRef<HTMLDialogElement>>('customerDialog');
  private readonly detailDialog = viewChild<ElementRef<HTMLDialogElement>>('detailDialog');
  private returnFocus: HTMLElement | null = null;
  private editSource: ClienteDetalheResponse | null = null;
  protected readonly selectedCustomerId = signal('');
  protected readonly editingCustomerId = signal<number | null>(null);
  protected readonly activeDetailSection = signal('customer-record');
  protected readonly customerDetailSections = [
    { id: 'customer-record', label: 'Dados cadastrais', icon: 'file-text' },
    { id: 'customer-addresses', label: 'Endereço(s)', icon: 'map-pin' },
    { id: 'customer-contacts', label: 'Contatos', icon: 'phone' },
    { id: 'customer-commercial', label: 'Dados comerciais', icon: 'boxes' },
    { id: 'customer-history', label: 'Histórico', icon: 'calendar' },
  ];
  protected readonly customerDetails = signal<ClienteDetalheResponse | null>(null);
  protected readonly detailBusy = signal(false);
  protected readonly detailError = signal('');
  protected readonly loadingCustomers = signal(true);
  protected readonly listError = signal('');
  protected readonly pageMessage = signal('');
  protected readonly customerSearch = signal('');
  protected readonly activeCustomerModalTab = signal('Dados gerais');
  protected readonly savingCustomer = signal(false);
  protected readonly customerMessage = signal('');
  protected readonly enderecos = signal<CadastroEndereco[]>([]);
  protected readonly customerRecords = signal<ClienteResumoResponse[]>([]);
  protected readonly customers = computed(() => this.customerRecords().map(c => ({
    id: String(c.id), nome: c.nomeFantasia || c.nomeRazaoSocial, subtitulo: c.tipoPessoa === 2 ? 'Pessoa jurídica' : 'Pessoa física',
    documento: c.cpfCnpj, telefone: c.celular || c.telefone || 'Telefone não informado', email: c.email || 'E-mail não informado', status: c.ativo ? 'Ativo' : 'Inativo',
  })));
  protected readonly filteredCustomers = computed(() => {
    const term = this.customerSearch().trim().toLocaleLowerCase('pt-BR');
    return this.customers().filter(c => !term || [c.nome, c.subtitulo, c.documento, c.telefone, c.email].some(v => v.toLocaleLowerCase('pt-BR').includes(term)));
  });
  protected readonly metrics = computed(() => {
    const all = this.customerRecords(); const active = all.filter(c => c.ativo).length; const now = new Date();
    const recent = all.filter(c => { const date = new Date(c.criadoEm); return date.getMonth() === now.getMonth() && date.getFullYear() === now.getFullYear(); }).length;
    return [
      { icon: 'users', label: 'Total de clientes', value: String(all.length), delta: 'Cadastros na empresa', tone: 'success' },
      { icon: 'badge-check', label: 'Clientes ativos', value: String(active), delta: 'Disponíveis para vendas', tone: 'success' },
      { icon: 'plus', label: 'Novos clientes (mês)', value: String(recent), delta: 'Cadastrados neste mês', tone: 'success' },
      { icon: 'x', label: 'Clientes inativos', value: String(all.length - active), delta: 'Cadastros inativos', tone: 'danger' },
    ];
  });
  protected readonly customerForm = this.fb.nonNullable.group({
    nome: ['', Validators.required], tipo: ['PF' as 'PF' | 'PJ', Validators.required], documento: ['', Validators.required],
    nascimento: [''], rg: [''], orgaoEmissor: [''], estadoCivil: [''], profissao: [''], cnh: [''],
    nomeFantasia: [''], inscricaoEstadual: [''], inscricaoMunicipal: [''], indicadorIe: ['Nao contribuinte'], consumidorFinal: [true],
    telefone: [''], celular: [''], email: ['', Validators.email], clienteDesde: [''], origem: [''], vendedorPadrao: [''],
    ativo: [true], observacoes: [''], relacionamento: ['Cliente' as 'Cliente' | 'Fornecedor' | 'Ambos'],
    enderecoId: [null as number | null], enderecoBusca: [''],
    contatoNome: [''], contatoCargo: [''], contatoEmail: ['', Validators.email], contatoTelefone: [''], contatoCelular: [''],
    condicaoPagamento: [''], limiteCredito: [0, Validators.min(0)], tabelaPreco: [''], bloquearVenda: [false], permiteFiado: [true],
  });
  private readonly formState = toSignal(this.customerForm.valueChanges, { initialValue: this.customerForm.getRawValue() });
  protected readonly enderecosFiltrados = computed(() => {
    const term = (this.formState().enderecoBusca ?? '').trim().toLocaleLowerCase('pt-BR');
    return this.enderecos().filter(e => (e.ativo || e.id === this.formState().enderecoId) && (!term || [e.nome, e.logradouro, e.bairro, e.municipio, e.cep].some(v => v.toLocaleLowerCase('pt-BR').includes(term))));
  });
  protected readonly enderecoSelecionado = computed(() => this.enderecos().find(e => e.id === this.formState().enderecoId) ?? null);
  protected readonly legacyAddress = computed(() => !this.formState().enderecoId ? this.editSource?.dados.endereco ?? null : null);

  constructor() {
    this.api.listarEnderecos(this.empresaId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: data => this.enderecos.set(data), error: () => {} });
    this.carregarClientes();
  }
  protected carregarClientes(): void {
    this.loadingCustomers.set(true); this.listError.set('');
    this.api.listarClientes(this.empresaId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => { this.customerRecords.set(data); this.loadingCustomers.set(false); },
      error: () => { this.loadingCustomers.set(false); this.listError.set('Não foi possível carregar os clientes. Tente novamente.'); },
    });
  }
  protected selecionarCliente(id: string): void {
    this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.activeDetailSection.set('customer-record');
    this.selectedCustomerId.set(id); this.customerDetails.set(null); this.detailError.set(''); this.detailBusy.set(true);
    this.detailDialog()?.nativeElement.showModal();
    this.api.obterCliente(this.empresaId, Number(id)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => { if (this.selectedCustomerId() === id) { this.customerDetails.set(data); this.detailBusy.set(false); } },
      error: error => { if (this.selectedCustomerId() === id) { this.detailError.set(this.errorMessage(error, 'Não foi possível carregar o cadastro do cliente.')); this.detailBusy.set(false); } },
    });
  }
  protected goToCustomerSection(id: string): void {
    const section = this.detailDialog()?.nativeElement.querySelector<HTMLElement>('#' + id);
    if (!section) return;
    this.activeDetailSection.set(id); section.scrollIntoView({ block: 'start', behavior: 'instant' }); section.focus({ preventScroll: true });
  }
  protected editCustomerAddress(): void { this.editarCliente(); this.activeCustomerModalTab.set('Enderecos'); }
  protected formatDocument(value: string): string {
    const digits = value.replace(/\D/g, '');
    return digits.length === 14 ? digits.replace(/^(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})$/, '$1.$2.$3/$4-$5') : digits.length === 11 ? digits.replace(/^(\d{3})(\d{3})(\d{3})(\d{2})$/, '$1.$2.$3-$4') : value;
  }
  protected printCustomer(): void {
    const dialog = this.detailDialog()?.nativeElement; if (!dialog || !this.customerDetails()) return;
    const content = dialog.cloneNode(true) as HTMLElement;
    content.querySelectorAll('button, nav, .detail-footer').forEach(e => e.remove());
    const frame = document.createElement('iframe'); frame.title = 'Impressão do cadastro do cliente'; frame.setAttribute('aria-hidden', 'true');
    frame.setAttribute('sandbox', 'allow-same-origin allow-modals');
    Object.assign(frame.style, { position: 'fixed', width: '1px', height: '1px', border: '0', opacity: '0' });
    frame.onload = () => {
      const preview = frame.contentWindow; if (!preview) { frame.remove(); return; }
      preview.addEventListener('afterprint', () => frame.remove(), { once: true });
      try { preview.focus(); preview.print(); } catch { frame.remove(); }
    };
    frame.srcdoc = "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\"><title>Cadastro do cliente</title><style>body{font:12px Arial,sans-serif;color:#19344e;padding:20px}h2{font-size:20px}h3{font-size:14px}section{margin:14px 0;padding:12px;border:1px solid #dde5eb;break-inside:avoid}dl{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}dt{color:#687d90;font-size:11px}dd{margin:4px 0;overflow-wrap:anywhere}svg{width:14px;height:14px;vertical-align:middle}.detail-profile{display:flex;gap:12px;align-items:center}.detail-profile-badges{margin-left:auto}.avatar{font-weight:bold}p{line-height:1.5}.detail-notes{white-space:pre-wrap}</style></head><body>" + content.innerHTML + "</body></html>";
    document.body.append(frame);
  }
  protected fecharDetalhes(): void { this.detailDialog()?.nativeElement.close(); this.returnFocus?.focus(); }
  protected abrirNovoCliente(): void {
    this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.editSource = null; this.editingCustomerId.set(null); this.customerForm.reset(); this.customerMessage.set('');
    this.activeCustomerModalTab.set('Dados gerais'); this.customerDialog()?.nativeElement.showModal();
  }
  protected editarCliente(): void {
    const detail = this.customerDetails(); if (!detail || this.detailBusy()) return;
    this.editSource = structuredClone(detail); this.editingCustomerId.set(detail.id);
    const d = detail.dados; const contact = d.contatos?.[0]; const commercial = d.dadosComerciais;
    this.customerForm.reset();
    this.customerForm.patchValue({
      nome: d.nomeRazaoSocial, tipo: d.tipoPessoa === 2 ? 'PJ' : 'PF', documento: d.cpfCnpj,
      nascimento: d.dataNascimento || '', rg: d.rg || '', orgaoEmissor: d.orgaoEmissor || '', estadoCivil: d.estadoCivil || '', profissao: d.profissao || '', cnh: d.cnh || '',
      nomeFantasia: d.nomeFantasia || '', inscricaoEstadual: d.inscricaoEstadual || '', inscricaoMunicipal: d.inscricaoMunicipal || '',
      indicadorIe: d.indicadorIe === 1 ? 'Contribuinte ICMS' : d.indicadorIe === 2 ? 'Isento' : 'Nao contribuinte', consumidorFinal: d.consumidorFinal,
      telefone: d.telefone || '', celular: d.celular || '', email: d.email || '', clienteDesde: d.clienteDesde || '', origem: d.origem || '', vendedorPadrao: d.vendedorPadrao || '',
      ativo: d.ativo, observacoes: d.observacao || '', relacionamento: d.relacionamento === 2 ? 'Fornecedor' : d.relacionamento === 3 ? 'Ambos' : 'Cliente',
      enderecoId: d.enderecoId, enderecoBusca: '', contatoNome: contact?.nome || '', contatoCargo: contact?.cargo || '', contatoEmail: contact?.email || '', contatoTelefone: contact?.telefone || '', contatoCelular: contact?.celular || '',
      condicaoPagamento: commercial?.condicaoPagamento || '', limiteCredito: commercial?.limiteCredito ?? d.limiteCredito ?? 0, tabelaPreco: commercial?.tabelaPreco || '', bloquearVenda: commercial?.bloquearVenda ?? false, permiteFiado: commercial?.permiteFiado ?? true,
    });
    this.customerMessage.set(''); this.activeCustomerModalTab.set('Dados gerais'); this.detailDialog()?.nativeElement.close(); this.customerDialog()?.nativeElement.showModal();
  }
  protected fecharNovoCliente(): void {
    if (this.savingCustomer()) return;
    this.customerDialog()?.nativeElement.close();
    if (this.editingCustomerId() !== null && this.customerDetails()) this.detailDialog()?.nativeElement.showModal();
    else this.returnFocus?.focus();
  }
  protected cadastrarCliente(): void {
    if (this.savingCustomer()) return;
    if (this.customerForm.invalid) { this.customerForm.markAllAsTouched(); this.customerMessage.set('Revise os campos obrigatórios e os e-mails antes de salvar.'); return; }
    const f = this.customerForm.getRawValue(); const digits = f.documento.replace(/\D/g, '');
    if (digits.length !== (f.tipo === 'PJ' ? 14 : 11)) { this.customerMessage.set('Informe CPF com 11 dígitos ou CNPJ com 14 dígitos conforme o tipo de pessoa.'); this.activeCustomerModalTab.set('Dados gerais'); return; }
    if (f.indicadorIe === 'Contribuinte ICMS' && !f.inscricaoEstadual.trim()) { this.customerMessage.set('Informe a Inscrição Estadual do cliente contribuinte.'); this.activeCustomerModalTab.set('Dados gerais'); return; }
    const contacts = [...(this.editSource?.dados.contatos ?? [])];
    if (f.contatoNome.trim()) {
      const primary = { nome: f.contatoNome, cargo: f.contatoCargo || null, email: f.contatoEmail || null, telefone: f.contatoTelefone || null, celular: f.contatoCelular || null, principal: contacts[0]?.principal ?? true };
      if (contacts.length) contacts[0] = primary; else contacts.push(primary);
    } else if (contacts.length) contacts.shift();
    const payload: CriarClientePayload = {
      empresaId: this.empresaId, tipoPessoa: f.tipo === 'PJ' ? 2 : 1, relacionamento: f.relacionamento === 'Fornecedor' ? 2 : f.relacionamento === 'Ambos' ? 3 : 1,
      nomeRazaoSocial: f.nome, nomeFantasia: f.nomeFantasia || null, cpfCnpj: digits, rg: f.rg || null, orgaoEmissor: f.orgaoEmissor || null, estadoCivil: f.estadoCivil || null,
      dataNascimento: f.nascimento || null, profissao: f.profissao || null, cnh: f.cnh || null, inscricaoEstadual: f.inscricaoEstadual || null, inscricaoMunicipal: f.inscricaoMunicipal || null,
      indicadorIe: f.indicadorIe === 'Contribuinte ICMS' ? 1 : f.indicadorIe === 'Isento' ? 2 : 9, consumidorFinal: f.consumidorFinal,
      email: f.email || null, telefone: f.telefone || null, celular: f.celular || null, clienteDesde: f.clienteDesde || null, origem: f.origem || null, vendedorPadrao: f.vendedorPadrao || null,
      limiteCredito: f.limiteCredito, observacao: f.observacoes || null, ativo: f.ativo, enderecoId: f.enderecoId, endereco: this.editSource?.dados.endereco ?? null, contatos: contacts,
      dadosComerciais: { condicaoPagamento: f.condicaoPagamento || null, limiteCredito: f.limiteCredito, tabelaPreco: f.tabelaPreco || null, vendedorPadrao: f.vendedorPadrao || null, origem: f.origem || null, bloquearVenda: f.bloquearVenda, permiteFiado: f.permiteFiado },
    };
    this.savingCustomer.set(true); this.customerMessage.set('');
    const editing = this.editingCustomerId();
    const request = editing === null ? this.api.criarCliente(payload) : this.api.atualizarCliente(editing, payload);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.customerRecords.update(all => all.some(c => c.id === saved.id) ? all.map(c => c.id === saved.id ? saved : c) : [saved, ...all]);
        this.savingCustomer.set(false); this.customerDialog()?.nativeElement.close(); this.pageMessage.set(editing === null ? 'Cliente cadastrado.' : 'Cadastro do cliente atualizado.');
        this.selecionarCliente(String(saved.id));
      },
      error: error => { this.savingCustomer.set(false); this.customerMessage.set(this.errorMessage(error, 'Não foi possível salvar o cadastro.')); },
    });
  }
  protected selecionarEndereco(endereco: CadastroEndereco): void { this.customerForm.patchValue({ enderecoId: endereco.id, enderecoBusca: '' }); }
  protected initials(name: string): string { return name.split(' ').filter(Boolean).slice(0, 2).map(s => s[0]).join('').toUpperCase(); }
  protected formatCurrency(value: number): string { return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }); }
  protected formatCep(cep: string): string { return cep.replace(/^(\d{5})(\d{3})$/, '$1-$2'); }
  protected formatDate(date: string | null | undefined): string { if (!date) return 'Não informado'; const value = new Date(date.length === 10 ? date + 'T12:00:00' : date); return Number.isNaN(value.getTime()) ? date : value.toLocaleDateString('pt-BR'); }
  private errorMessage(error: any, fallback: string): string { return typeof error?.error === 'string' ? error.error : error?.error?.detail || fallback; }
}
