import { AlertService } from '../../core/alert.service';
import { SessionService } from '../../core/session.service';
import { Component, computed, ElementRef, HostListener, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ClienteResumoResponse, Produto, EmitirNfePayload, EmissaoNfeResponse, DestinatarioFiscalData } from '../../core/app-models';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';
import { FiscalCertificateService, FiscalEnvironment } from '../../core/fiscal-certificate.service';

interface SaleItem { produto: Produto; quantidade: number; preco: number; desconto: number; precoEditado?: boolean; origem?: string; ncm?: string; unidade?: string }
interface Payment { id: number; forma: string; valor: number }
interface SaleDraft {
  cliente: ClienteResumoResponse | null; itens: SaleItem[]; pagamentos: Payment[];
  tipo: string; vendedor: string; emitir: boolean; modelo: string; finalidade: string;
  natureza: string; cfop: string; serie: string; desconto: number; frete: number;
  despesas: number; observacoes: string; csosn?: string; destinatario?: DestinatarioFiscalData; destinatarioClienteId?: number; destinatarioCadastro?: DestinatarioFiscalData;
  vehicleName?: string; vehiclePlate?: string; modalidadeFrete?: number; indicadorPresenca?: number;
  vendaOrigemId?: number; emissionRequestId?: string; emissionPayload?: EmitirNfePayload | null; emittedNote?: EmissaoNfeResponse | null; emissionAttempted?: boolean;
}

@Component({
  selector: 'app-sales', imports: [FormsModule, RouterLink, AppIconComponent],
  templateUrl: './sales.component.html', styleUrls: ['./sales.component.scss', './sale-review.scss'],
})
export class SalesComponent {
  private readonly api = inject(ApiService);
  private readonly alerts = inject(AlertService);
  private clearing = false;
  private vendaOrigemId: number | undefined;
  private notify(text: string, icon: 'info' | 'success' | 'error' = 'info', fiscal = false): void {
    (fiscal ? this.fiscalMessage : this.message).set(text); this.alerts.notify(text, icon);
  }
  private readonly session = inject(SessionService);
  private readonly certificateStore = inject(FiscalCertificateService);
  private readonly reviewDialog = viewChild<ElementRef<HTMLDialogElement>>('reviewDialog');
  private readonly productSearchInput = viewChild<ElementRef<HTMLInputElement>>('productSearchInput');
  protected readonly fiscalMessage = signal('');
  protected readonly fiscalEnvironment = signal<FiscalEnvironment | null>(null);
  protected readonly environmentError = signal('');

  protected readonly emissionBusy = signal(false);
  protected readonly emissionAttempted = signal(false);
  protected readonly emittedNote = signal<EmissaoNfeResponse | null>(null);
  protected readonly reviewStep = signal(0);
  protected readonly reviewSteps = [
    { id: 'review-sale', label: 'Dados da venda', icon: 'file-text' },
    { id: 'review-tax', label: 'Dados fiscais', icon: 'tags' },
    { id: 'review-destination', label: 'Destinatário', icon: 'user' },
    { id: 'review-summary', label: 'Resumo', icon: 'receipt-text' },
  ];
  protected readonly destinationError = signal('');
  private destinationLoadingId: number | null = null;
  protected csosn = '';
  protected destination: DestinatarioFiscalData | null = null;
  protected readonly destinationBusy = signal(false);
  private destinationId: number | null = null;
  private destinationBaseline: DestinatarioFiscalData | null = null;
  private emissionRequestId: string = crypto.randomUUID();
  private emissionPayload: EmitirNfePayload | null = null;

  protected requestInvoice(): void {
    if (this.emissionBusy() || this.emittedNote() || this.destinationBusy()) return;
    if (!this.emitir || this.tipo !== 'Venda' || this.modelo !== '55') { this.notify('Selecione Venda e NF-e modelo 55 para emitir.', 'info', true); return; }
    if (!this.cliente() || !this.itens().length || this.balance() !== 0) { this.notify('Revise cliente, itens e pagamentos antes de emitir.', 'info', true); return; }
    if (!this.emissionPayload && this.destinationError()) { this.notify('Atualize os dados do cadastro antes de emitir: ' + this.destinationError(), 'info', true); return; }
    if (!this.destination && !this.emissionPayload) { this.notify('Carregue e revise o endereço fiscal do destinatário antes de emitir.', 'info', true); return; }
    if (!this.fiscalEnvironment()) { this.notify('Carregue o ambiente fiscal antes de emitir.', 'info', true); return; }
    try {
      const cert = this.certificateStore.load();
      if (!cert) { this.notify('Cadastre o certificado A1 nas configurações antes de emitir.', 'info', true); return; }
      this.emissionPayload ??= {
        solicitacaoId: this.emissionRequestId, vendaOrigemId: this.vendaOrigemId, clienteId: this.cliente()!.id, ambiente: this.fiscalEnvironment() === 'producao' ? 1 : 2,
        modalidadeFrete: this.modalidadeFrete, indicadorPresenca: this.indicadorPresenca,
        serie: Number(this.serie), natureza: this.natureza, cfop: this.cfop, modelo: this.modelo,
        desconto: this.desconto || 0, frete: this.frete || 0, despesas: this.despesas || 0, observacoes: this.observacoes,
        itens: this.itens().map(i => ({ produtoId: i.produto.id, quantidade: i.quantidade, precoUnitario: i.preco, descontoPercentual: i.desconto,
          origem: i.origem ?? i.produto.origemMercadoria ?? '', ncm: i.ncm ?? (i.produto.ncm ? String(i.produto.ncm).padStart(8, '0') : ''), unidade: i.unidade ?? i.produto.unidadeComercial ?? 'UN' })),
        destinatario: this.destination ?? undefined, csosn: this.csosn || undefined,
        pagamentos: this.pagamentos().map(p => ({ forma: p.forma, valor: p.valor })),
      };
      this.emissionAttempted.set(true);
      if (!this.saveDraft()) { this.emissionAttempted.set(false); this.emissionPayload = null; this.notify('Não foi possível guardar a identificação da tentativa. Salve o rascunho antes de transmitir.', 'error', true); return; }
      this.emissionBusy.set(true); this.fiscalMessage.set('Enviando a NF-e. Aguarde a resposta da SEFAZ.');
      this.api.emitirNfe(this.emissionPayload, cert).subscribe({
        next: result => this.applyEmission(result),
        error: error => {
          this.emissionBusy.set(false);
          if (error.status === 400) { this.emissionAttempted.set(false); this.emissionPayload = null; }
          this.notify(typeof error.error === 'string' ? error.error : error.error?.detail ?? 'A resposta não foi recebida. Use Recuperar tentativa para consultar a mesma solicitação, sem gerar outra nota.', 'error', true);
          this.saveDraft();
        },
      });
    } catch { this.emissionBusy.set(false); this.notify('Não foi possível carregar o certificado ou preparar a solicitação fiscal.', 'error', true); }
  }

  protected consultEmission(reenviar = false): void {
    const nota = this.emittedNote(); if (!nota || this.emissionBusy()) return;
    try {
      const cert = this.certificateStore.load(); if (!cert) { this.notify('Cadastre o certificado A1 para consultar.', 'info', true); return; }
      this.emissionBusy.set(true);
      this.api.consultarAutorizacao(nota.id, cert, reenviar).subscribe({ next: result => this.applyEmission(result), error: error => {
        this.emissionBusy.set(false); this.notify(typeof error.error === 'string' ? error.error : error.error?.detail ?? 'Não foi possível confirmar o processamento. Consulte novamente a mesma nota.', 'error', true);
      } });
    } catch { this.emissionBusy.set(false); this.notify('Certificado salvo inválido.', 'error', true); }
  }
  private applyEmission(result: EmissaoNfeResponse): void {
    this.emittedNote.set(result); this.emissionBusy.set(false); this.saveDraft();
    this.notify(result.situacao === 'Autorizada' ? `NF-e ${result.numero} autorizada. Protocolo ${result.protocolo}.` : `SEFAZ ${result.codigo || 'sem confirmação'}: ${result.motivo}`, result.situacao === 'Autorizada' ? 'success' : result.situacao === 'Rejeitada' ? 'error' : 'info', true);
  }
  protected reviseRejected(): void {
    if (this.emittedNote()?.situacao !== 'Rejeitada') return;
    this.vendaOrigemId ??= this.emittedNote()!.id;
    this.emissionAttempted.set(false); this.emissionPayload = null; this.emittedNote.set(null); this.emissionRequestId = crypto.randomUUID(); this.notify('Corrija os dados fiscais antes de iniciar uma nova tentativa.', 'info', true); this.saveDraft(); this.loadDestination();
  }
  protected goToReviewSection(index: number): void {
    const section = this.reviewDialog()?.nativeElement.querySelector<HTMLElement>('#' + this.reviewSteps[index]?.id);
    if (!section) return;
    this.reviewStep.set(index); section.scrollIntoView({ block: 'start', behavior: 'instant' }); section.focus({ preventScroll: true });
  }
  protected editReviewCustomer(): void {
    this.closeReview(); this.editingCustomer = true;
    setTimeout(() => document.querySelector<HTMLInputElement>('[aria-label="Buscar cliente"]')?.focus());
  }
  protected editReviewOperation(): void {
    this.closeReview(); this.showOptions = true;
    setTimeout(() => document.querySelector<HTMLElement>('.fiscal-fields input')?.focus());
  }
  protected loadDestination(force = false): void {
    const customer = this.cliente();
    if (!customer || this.emissionAttempted() || this.destinationLoadingId === customer.id) return;
    this.destinationLoadingId = customer.id; this.destinationBusy.set(true); this.destinationError.set('');
    this.api.carregarDestinatarioFiscal(customer.id).subscribe({
      next: data => {
        if (this.cliente()?.id !== customer.id || this.destinationLoadingId !== customer.id) return;
        const previous = this.destinationId === customer.id ? this.destination : null;
        const merged = { ...data };
        // Mantem apenas ajustes manuais de endereco; a condicao fiscal vem do cadastro atual.
        const addressFields = ['logradouro', 'numero', 'complemento', 'bairro', 'municipio', 'codigoMunicipio', 'uf', 'cep'] as const;
        if (!force && previous) for (const field of addressFields) {
          if (!this.destinationBaseline || previous[field] !== this.destinationBaseline[field]) Object.assign(merged, { [field]: previous[field] });
        }
        this.destination = merged; this.destinationBaseline = { ...data }; this.destinationId = customer.id;
        this.destinationLoadingId = null; this.destinationBusy.set(false);
      },
      error: () => {
        if (this.cliente()?.id !== customer.id || this.destinationLoadingId !== customer.id) return;
        this.destinationLoadingId = null; this.destinationBusy.set(false);
        this.destinationError.set('Não foi possível carregar o endereço do cadastro. Tente novamente.');
      },
    });
  }
  protected updateFiscalItem(id: number, field: 'origem' | 'ncm' | 'unidade', value: string): void {
    if (this.emissionAttempted()) return;
    this.itens.update(items => items.map(i => i.produto.id === id ? { ...i, [field]: value } : i));
  }
  protected readonly downloading = signal<'xml' | 'pdf' | null>(null);
  protected downloadAuthorized(formato: 'xml' | 'pdf'): void {
    const nota = this.emittedNote(); if (!nota || this.downloading() || !['Autorizada', 'Cancelada'].includes(nota.situacao)) return;
    this.downloading.set(formato);
    this.api.baixarNotaFiscal(this.session.empresaId, nota.id, formato).subscribe({ next: blob => {
      this.downloading.set(null);
      const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = `nfe-${nota.chaveAcesso}.${formato}`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    }, error: () => { this.downloading.set(null); this.notify(`Não foi possível baixar o ${formato === 'pdf' ? 'DANFE em PDF' : 'XML autorizado'}. Tente novamente.`, 'error', true); } });
  }
  protected closeReview(): void {
    if (!this.emissionBusy()) this.reviewDialog()?.nativeElement.close();
  }
  private get draftKey(): string { return this.session.storageKey('venda:rascunho'); }
  protected readonly clientes = signal<ClienteResumoResponse[]>([]);
  protected readonly produtos = signal<Produto[]>([]);
  protected readonly cliente = signal<ClienteResumoResponse | null>(null);
  protected readonly itens = signal<SaleItem[]>([]);
  protected readonly pagamentos = signal<Payment[]>([{ id: 1, forma: 'Pix', valor: 0 }]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal('');
  protected readonly message = signal('');
  protected readonly customerSearch = signal('');
  protected readonly productSearch = signal('');
  protected readonly customerResults = computed(() => {
    const term = this.customerSearch().trim().toLocaleLowerCase('pt-BR');
    return term ? this.clientes().filter(c => `${c.nomeRazaoSocial} ${c.cpfCnpj} ${c.id}`.toLocaleLowerCase('pt-BR').includes(term)).slice(0, 8) : [];
  });
  protected readonly productResults = computed(() => {
    const term = this.productSearch().trim().toLocaleLowerCase('pt-BR');
    return term ? this.produtos().filter(p => `${p.descricao} ${p.sku} ${p.codintern} ${p.codfabricante}`.toLocaleLowerCase('pt-BR').includes(term)).slice(0, 8) : [];
  });
  protected tipo = 'Venda';
  protected vendedor = '';
  protected emitir = true;
  protected modelo = '55';
  protected finalidade = 'Normal';
  protected natureza = 'Venda de mercadoria adquirida ou recebida de terceiros';
  protected cfop = '5102';
  protected modalidadeFrete = 9;
  protected indicadorPresenca = 1;
  protected serie = '1';
  protected desconto = 0;
  protected frete = 0;
  protected despesas = 0;
  protected observacoes = '';
  protected showExtras = false;
  protected showOptions = false;
  protected editingCustomer = false;
  protected editingVehicle = false;
  protected vehicleName = '';
  protected vehiclePlate = '';
  protected readonly subtotal = computed(() => this.round(this.itens().reduce((sum, i) => sum + i.quantidade * i.preco, 0)));
  protected readonly itemDiscount = computed(() => this.round(this.itens().reduce((sum, i) => sum + i.quantidade * i.preco * i.desconto / 100, 0)));

  constructor() { this.restore(); this.loadCatalog(); this.loadEnvironment(); }

  private loadEnvironment(): void {
    this.api.carregarAmbienteFiscal().subscribe({
      next: settings => { if (!this.itens().length && this.serie === '1') this.serie = String(settings.seriePadrao ?? 1); this.fiscalEnvironment.set(settings.environment); this.environmentError.set(''); },
      error: () => { this.fiscalEnvironment.set(null); this.environmentError.set('Não foi possível carregar o ambiente fiscal da empresa.'); },
    });
  }

  protected environmentLabel(): string {
    const saved = this.emittedNote();
    const environment = saved ? saved.ambiente === 1 ? 'producao' : 'homologacao' : this.fiscalEnvironment();
    return environment === 'producao' ? 'Produção' : environment === 'homologacao' ? 'Homologação · sem valor fiscal' : 'Ambiente não carregado';
  }
  protected loadCatalog(): void {
    this.loading.set(true); this.loadError.set('');
    forkJoin({ clientes: this.api.listarClientes(this.session.empresaId), produtos: this.api.listarProdutos(this.session.empresaId, 1, 100) }).subscribe({
      next: ({ clientes, produtos }) => {
        this.clientes.set(clientes.filter(c => c.ativo && c.relacionamento !== 2));
        this.produtos.set(produtos.produtos.filter(p => p.ativo));
        this.recoverDraftPrices();
        // Load the remaining catalog pages so product search is not limited to the first page.
        const pages = Math.ceil(produtos.total / 100);
        if (pages > 1) forkJoin(Array.from({ length: pages - 1 }, (_, i) => this.api.listarProdutos(this.session.empresaId, i + 2, 100))).subscribe({
          next: results => { this.produtos.update(p => [...p, ...results.flatMap(r => r.produtos.filter(x => x.ativo))]); this.recoverDraftPrices(); this.loading.set(false); },
          error: () => { this.loading.set(false); this.loadError.set('Não foi possível carregar todo o catálogo. Tente novamente.'); },
        });
        else this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.loadError.set('Não foi possível carregar clientes e produtos. Verifique se o backend está disponível e tente novamente.'); },
    });
  }
  protected selectCustomer(c: ClienteResumoResponse): void { this.csosn = ''; this.cliente.set(c); this.customerSearch.set(''); this.editingCustomer = false; this.destination = null; this.destinationId = null; this.destinationBaseline = null; this.destinationLoadingId = null; this.destinationBusy.set(false); this.destinationError.set(''); }
  protected focusProductSearch(): void { this.productSearchInput()?.nativeElement.focus(); }

  @HostListener('document:keydown', ['$event'])
  protected onSearchShortcut(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    if (event.key !== '/' || event.ctrlKey || event.altKey || event.metaKey ||
      target?.closest('input, textarea, select, [contenteditable="true"], dialog') || this.reviewDialog()?.nativeElement.open) return;
    event.preventDefault();
    this.focusProductSearch();
  }

  protected preview(): void {
    if (!this.itens().length) { this.message.set('Adicione pelo menos um produto para pré-visualizar a venda.'); return; }
    this.fiscalMessage.set('');
    this.reviewStep.set(0); this.loadDestination(); this.reviewDialog()?.nativeElement.showModal();
  }
  protected addProduct(produto: Produto): void {
    this.itens.update(items => {
      const existing = items.find(i => i.produto.id === produto.id);
      return existing ? items.map(i => i === existing ? { ...i, quantidade: i.quantidade + 1 } : i) : [...items, { produto, quantidade: 1, preco: this.productPrice(produto), desconto: 0 }];
    });
    this.productSearch.set(''); this.message.set('');
  }
  protected productPrice(produto: Produto): number {
    return Number.isFinite(produto.preco) && produto.preco > 0 ? produto.preco : 0;
  }

  private recoverDraftPrices(): void {
    const catalog = new Map(this.produtos().map(produto => [produto.id, produto]));
    this.itens.update(items => items.map(item => {
      const produto = catalog.get(item.produto.id);
      // Recupera o zero causado pela importacao sem sobrescrever precos editados.
      const precoDeCustoAntigo = item.produto.preco === 0 && item.preco === item.produto.custo;
      return (item.preco === 0 || precoDeCustoAntigo) && !item.precoEditado && produto
        ? { ...item, produto, preco: this.productPrice(produto) } : item;
    }));
  }

  protected updateItem(id: number, field: 'quantidade' | 'preco' | 'desconto', value: number | null): void {
    const number = Number.isFinite(value) ? Number(value) : 0;
    this.itens.update(items => items.map(i => i.produto.id === id ? { ...i, [field]: Math.max(field === 'quantidade' ? 0.01 : 0, field === 'desconto' ? Math.min(100, number) : number), precoEditado: field === 'preco' || i.precoEditado } : i));
  }
  protected removeItem(id: number): void { this.itens.update(items => items.filter(i => i.produto.id !== id)); }
  protected itemTotal(i: SaleItem): number { return this.round(i.quantidade * i.preco * (1 - i.desconto / 100)); }
  protected total(): number { return this.round(Math.max(0, this.subtotal() - this.itemDiscount() - (this.desconto || 0) + (this.frete || 0) + (this.despesas || 0))); }
  protected paid(): number { return this.round(this.pagamentos().reduce((sum, p) => sum + p.valor, 0)); }
  protected balance(): number { return this.round(this.total() - this.paid()); }
  protected addPayment(): void { this.pagamentos.update(p => [...p, { id: Date.now(), forma: 'Pix', valor: Math.max(0, this.balance()) }]); }
  protected updatePayment(id: number, field: 'forma' | 'valor', value: string | number | null): void {
    this.pagamentos.update(payments => payments.map(p => p.id === id ? { ...p, [field]: field === 'valor' ? Math.max(0, Number(value) || 0) : value } : p));
  }
  protected removePayment(id: number): void { this.pagamentos.update(p => p.filter(x => x.id !== id)); }
  protected fillPayment(): void { this.pagamentos.set([{ id: Date.now(), forma: this.pagamentos()[0]?.forma ?? 'Pix', valor: this.total() }]); }
  protected currency(value: number): string { return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }); }
  private round(value: number): number { return Math.round((value + Number.EPSILON) * 100) / 100; }
  private draft(): SaleDraft {
    return { cliente: this.cliente(), itens: this.itens(), pagamentos: this.pagamentos(), tipo: this.tipo, vendedor: this.vendedor, emitir: this.emitir, modelo: this.modelo, finalidade: this.finalidade, natureza: this.natureza, cfop: this.cfop, serie: this.serie, desconto: this.desconto, frete: this.frete, despesas: this.despesas, observacoes: this.observacoes, csosn: this.csosn, destinatario: this.destination ?? undefined, destinatarioClienteId: this.destinationId ?? undefined, destinatarioCadastro: this.destinationBaseline ?? undefined, vehicleName: this.vehicleName, vehiclePlate: this.vehiclePlate, modalidadeFrete: this.modalidadeFrete, indicadorPresenca: this.indicadorPresenca, vendaOrigemId: this.vendaOrigemId, emissionRequestId: this.emissionRequestId, emissionPayload: this.emissionPayload, emittedNote: this.emittedNote(), emissionAttempted: this.emissionAttempted() };
  }
  protected saveDraft(showAlert = false): boolean {
    try { localStorage.setItem(this.draftKey, JSON.stringify(this.draft())); this.message.set('Rascunho salvo neste navegador. Você pode continuar depois.'); if (showAlert) this.alerts.notify(this.message(), 'success'); return true; }
    catch { this.notify('Não foi possível salvar o rascunho. Verifique o armazenamento do navegador.', 'error'); return false; }
  }
  private restore(): void {
    try {
      const raw = localStorage.getItem(this.draftKey); if (!raw) return;
      const draft: SaleDraft = JSON.parse(raw);
      if (!Array.isArray(draft.itens) || !Array.isArray(draft.pagamentos)) return;
      this.cliente.set(draft.cliente); this.itens.set(draft.itens); this.pagamentos.set(draft.pagamentos);
      this.tipo = draft.tipo; this.vendedor = draft.vendedor; this.emitir = draft.emitir;
      this.modelo = draft.modelo; this.finalidade = draft.finalidade; this.natureza = draft.natureza;
      this.csosn = draft.csosn ?? '';
      this.cfop = draft.cfop; this.serie = draft.serie; this.desconto = draft.desconto;
      this.frete = draft.frete; this.despesas = draft.despesas; this.observacoes = draft.observacoes;
      this.modalidadeFrete = draft.modalidadeFrete ?? 9; this.indicadorPresenca = draft.indicadorPresenca ?? 1;
      this.vendaOrigemId = draft.vendaOrigemId;
      this.emissionRequestId = draft.emissionRequestId ?? crypto.randomUUID(); this.emissionPayload = draft.emissionPayload ?? null; this.emittedNote.set(draft.emittedNote ?? null); this.emissionAttempted.set(draft.emissionAttempted ?? false);
      this.destination = this.emissionPayload?.destinatario ?? (draft.destinatarioClienteId === draft.cliente?.id ? draft.destinatario ?? null : null);
      this.destinationId = this.destination ? draft.cliente?.id ?? null : null;
      this.destinationBaseline = this.destination ? draft.destinatarioCadastro ?? null : null;
      this.vehicleName = draft.vehicleName ?? ''; this.vehiclePlate = draft.vehiclePlate ?? '';
      this.message.set('Seu último rascunho foi recuperado.');
    } catch { this.message.set('Não foi possível recuperar o rascunho salvo.'); }
  }
  protected async clear(): Promise<void> {
    if (this.clearing) return;
    if (this.emissionBusy() || this.emissionAttempted() && !this.emittedNote()) { this.notify('Recupere a tentativa para confirmar seu registro antes de iniciar outra venda.'); return; }
    this.clearing = true;
    const registered = this.emittedNote();
    try {
      if ((this.itens().length || this.cliente()) && !await this.alerts.confirm(registered
        ? `A NF-e ${registered.numero} continuará no menu NF-e para consulta e validação. Iniciar uma nova venda mantém o registro e a baixa do estoque.`
        : 'Limpar a venda e excluir o rascunho salvo?', registered ? 'Iniciar nova venda?' : 'Limpar venda?')) return;
    } finally { this.clearing = false; }
    if (this.emissionBusy()) return;
    try { localStorage.removeItem(this.draftKey); } catch { this.notify('Não foi possível excluir o rascunho. A venda foi preservada; verifique o armazenamento do navegador.', 'error'); return; }
    this.closeReview(); this.vendaOrigemId = undefined; this.showOptions = false; this.reviewStep.set(0);
    this.emissionAttempted.set(false); this.emissionPayload = null; this.emittedNote.set(null); this.emissionRequestId = crypto.randomUUID(); this.fiscalMessage.set('');
    this.cliente.set(null); this.itens.set([]); this.pagamentos.set([{ id: 1, forma: 'Pix', valor: 0 }]);
    this.csosn = ''; this.destination = null; this.destinationId = null; this.destinationBaseline = null; this.destinationLoadingId = null; this.destinationBusy.set(false); this.destinationError.set('');
    this.desconto = 0; this.frete = 0; this.despesas = 0; this.observacoes = ''; this.vendedor = '';
    this.vehicleName = ''; this.vehiclePlate = ''; this.editingVehicle = false; this.editingCustomer = false;
    this.customerSearch.set(''); this.productSearch.set(''); this.message.set('');
    this.notify(registered ? `Nova venda liberada. A NF-e ${registered.numero} está disponível no menu NF-e.` : 'Venda limpa. Você já pode iniciar outra venda.', 'success');
  }
  protected finish(): void {
    if (!this.cliente()) { this.notify('Selecione um cliente para continuar.', 'info', false); return; }
    if (!this.itens().length) { this.notify('Adicione pelo menos um produto à venda.', 'info', false); return; }
    if (this.desconto < 0 || this.frete < 0 || this.despesas < 0 || this.desconto > this.subtotal() - this.itemDiscount()) { this.notify('Revise os descontos, frete e despesas da venda.', 'info', false); return; }
    if (this.balance() !== 0) { this.notify('O valor dos pagamentos deve ser igual ao total da venda.', 'info', false); return; }
    if (this.emitir && (!/^\d{4}$/.test(this.cfop) || !/^\d{1,3}$/.test(this.serie) || !this.natureza.trim())) { this.notify('Preencha a natureza da operação, um CFOP de 4 dígitos e uma série válida.', 'info', false); return; }
    if (this.saveDraft()) {
      this.message.set('');
      this.fiscalMessage.set('');
      this.reviewStep.set(0); this.loadDestination(); this.reviewDialog()?.nativeElement.showModal();
    }
  }
}
