import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { FiscalCertificateService } from '../../core/fiscal-certificate.service';
import { AlertService } from '../../core/alert.service';
import { SessionService } from '../../core/session.service';
import { sessionStub } from '../../../testing/session.fixture';
import { environment } from '../../../environments/environment';
import { appConfig } from '../../app.config';
import { SalesComponent } from './sales.component';

 describe('Sales workspace', () => {
  let fixture: ComponentFixture<SalesComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;
  const product = { id: 17, sku: 610, descricao: 'Filtro de óleo', marca: 'Marca cadastrada', preco: 42.9, custo: 12.5, ativo: true, ncm: 84212300, origemMercadoria: '0', unidadeComercial: 'UN' };
  const customer = { id: 7, nomeRazaoSocial: 'Cliente cadastrado', cpfCnpj: '11111111000111', ativo: true, relacionamento: 1 };
  const draftKey = sessionStub().storageKey('venda:rascunho');

  function catalog(products = [product], fiscalEnvironment: 'homologacao' | 'producao' = 'homologacao') {
    http.expectOne(`${environment.apiUrl}/fiscal/ambiente`).flush({ environment: fiscalEnvironment, emissaoDisponivel: true });
    http.expectOne(req => req.url === `${environment.apiUrl}/clientes`).flush([customer]);
    http.expectOne(req => req.url === `${environment.apiUrl}/produtos`).flush({ produtos: products, total: products.length });
    fixture.detectChanges();
  }

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [SalesComponent],
      providers: [...appConfig.providers, provideHttpClient(), provideHttpClientTesting(), { provide: AlertService, useValue: { notify: vi.fn(), confirm: vi.fn().mockResolvedValue(true) } }, provideRouter([]), { provide: SessionService, useFactory: sessionStub }],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SalesComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
    catalog();
  });

  afterEach(() => {
    try { http.verify(); } finally { TestBed.resetTestingModule(); localStorage.clear(); vi.restoreAllMocks(); }
  });

  async function input(selector: string, value: string) {
    const field = element.querySelector<HTMLInputElement>(selector)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function addProduct() {
    await input('[aria-label="Buscar produto"]', 'Filtro');
    element.querySelector<HTMLButtonElement>('.product-results button')!.click();
    fixture.detectChanges();
    await fixture.whenStable();
  }

  const destination = { inscricaoEstadual: '2243658790', indicadorIe: 1, consumidorFinal: false, logradouro: 'Rua de teste', numero: '10', complemento: '', bairro: 'Centro', municipio: 'Porto Alegre', codigoMunicipio: '4314902', uf: 'RS', cep: '90000000' };
  const authorized = { id: 37, numero: '2', serie: '1', chaveAcesso: '43' + '1'.repeat(42), ambiente: 1, situacao: 'Autorizada', codigo: '100', motivo: 'Autorizado o uso da NF-e', protocolo: '143260000000001', podeReenviar: false };
  async function prepareEmission() {
    fixture.destroy(); fixture = TestBed.createComponent(SalesComponent); element = fixture.nativeElement; fixture.detectChanges(); catalog([product], 'producao');
    TestBed.inject(FiscalCertificateService).save({ fileName: 'fixture.pfx', base64: 'fixture-only', password: 'fixture-only', uf: 'RS', environment: 'producao' });
    await input('[aria-label="Buscar cliente"]', 'Cliente');
    element.querySelector<HTMLButtonElement>('.customer-panel .search-results button')!.click(); fixture.detectChanges();
    await addProduct(); element.querySelector<HTMLButtonElement>('.full-payment')!.click(); fixture.detectChanges();
    Object.defineProperty(element.querySelector('dialog')!, 'showModal', { value: vi.fn() });
    element.querySelector<HTMLButtonElement>('.finish-button')!.click(); fixture.detectChanges();
    http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`).flush(destination); fixture.detectChanges(); await fixture.whenStable();
  }

  it('automatically fills the recipient address from the customer registry and sends the reviewed values', async () => {
    await prepareEmission();
    expect(element.querySelector<HTMLInputElement>('[aria-label="Logradouro do destinatário"]')!.value).toBe(destination.logradouro);
    expect(element.querySelector<HTMLInputElement>('[aria-label="CEP do destinatário"]')!.value).toBe(destination.cep);
    expect(element.querySelector<HTMLInputElement>('[aria-label="Código IBGE do destinatário"]')!.value).toBe(destination.codigoMunicipio);
    expect(element.querySelector('.review-address-source')!.textContent).toContain('cadastro do cliente');
    await input('[aria-label="Número do destinatário"]', '27');
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const request = http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`);
    expect(request.request.body.destinatario).toEqual({ ...destination, numero: '27' });
    request.flush(authorized); fixture.detectChanges();
  });

  it('reloads the address from the customer registry and disables emission during loading', async () => {
    await prepareEmission();
    element.querySelector<HTMLButtonElement>('.review-reload')!.click(); fixture.detectChanges();
    expect(element.querySelector<HTMLButtonElement>('.invoice-button')!.disabled).toBe(true);
    http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`).flush({ ...destination, logradouro: 'Rua atualizada no cadastro', numero: '99' });
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLInputElement>('[aria-label="Logradouro do destinatário"]')!.value).toBe('Rua atualizada no cadastro');
    expect(element.querySelector<HTMLButtonElement>('.invoice-button')!.disabled).toBe(false);
  });

  it('keeps reviewed address edits when restoring a saved draft', async () => {
    await prepareEmission(); await input('[aria-label="Número do destinatário"]', '28');
    [...element.querySelectorAll<HTMLButtonElement>('.actions button')].find(b => b.textContent?.includes('Salvar rascunho'))!.click();
    fixture.destroy(); fixture = TestBed.createComponent(SalesComponent); element = fixture.nativeElement; fixture.detectChanges(); catalog([product], 'producao');
    await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLInputElement>('[aria-label="Número do destinatário"]')!.value).toBe('28');
  });

  it('navigates to each review section with an accessible current location', async () => {
    await prepareEmission();
    const section = element.querySelector<HTMLElement>('#review-destination')!;
    section.scrollIntoView = vi.fn();
    const button = element.querySelectorAll<HTMLButtonElement>('.review-navigation button')[2];
    button.click(); fixture.detectChanges();
    expect(section.scrollIntoView).toHaveBeenCalled(); expect(button.getAttribute('aria-current')).toBe('location');
    expect(element.querySelectorAll('.review-navigation [aria-current="location"]').length).toBe(1);
  });

  it('refreshes the registry IE and tax status every time review opens while preserving only manual address changes', async () => {
    await prepareEmission(); await input('[aria-label="Número do destinatário"]', '28');
    element.querySelector<HTMLButtonElement>('.finish-button')!.click(); fixture.detectChanges();
    http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`).flush({ ...destination, indicadorIe: 9, consumidorFinal: true });
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLSelectElement>('[aria-label="Indicador de IE do destinatário"]')!.selectedOptions[0].textContent).toContain('Não contribuinte');
    element.querySelector<HTMLButtonElement>('.finish-button')!.click(); fixture.detectChanges();
    const refresh = http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`);
    expect(refresh.request.headers.get('Cache-Control')).toBe('no-cache');
    refresh.flush({ ...destination, inscricaoEstadual: '290782465', consumidorFinal: true, logradouro: 'Rua atualizada no cadastro' });
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLSelectElement>('[aria-label="Indicador de IE do destinatário"]')!.selectedOptions[0].textContent).toContain('Contribuinte do ICMS');
    expect(element.querySelector<HTMLInputElement>('[aria-label="Número do destinatário"]')!.value).toBe('28');
    expect(element.querySelector<HTMLInputElement>('[aria-label="Logradouro do destinatário"]')!.value).toBe('Rua atualizada no cadastro');
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const req = http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`);
    expect(req.request.body.destinatario.indicadorIe).toBe(1); expect(req.request.body.destinatario.inscricaoEstadual).toBe('290782465'); expect(req.request.body.destinatario.consumidorFinal).toBe(true);
    req.flush(authorized); fixture.detectChanges();
  });

  it('replaces stale fiscal status from an old saved draft with the current customer registry', async () => {
    await prepareEmission();
    [...element.querySelectorAll<HTMLButtonElement>('.actions button')].find(b => b.textContent?.includes('Salvar rascunho'))!.click();
    const saved = JSON.parse(localStorage.getItem(draftKey)!); saved.destinatario.indicadorIe = 9; saved.destinatario.inscricaoEstadual = '290782465'; delete saved.destinatarioCadastro;
    localStorage.setItem(draftKey, JSON.stringify(saved));
    fixture.destroy(); fixture = TestBed.createComponent(SalesComponent); element = fixture.nativeElement; fixture.detectChanges(); catalog([product], 'producao');
    Object.defineProperty(element.querySelector('dialog')!, 'showModal', { value: vi.fn() });
    element.querySelector<HTMLButtonElement>('.finish-button')!.click(); fixture.detectChanges();
    http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`).flush({ ...destination, inscricaoEstadual: '290782465' });
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLSelectElement>('[aria-label="Indicador de IE do destinatário"]')!.selectedOptions[0].textContent).toContain('Contribuinte do ICMS');
  });

  it('blocks emission after a failed registry refresh instead of transmitting cached fiscal status', async () => {
    await prepareEmission(); element.querySelector<HTMLButtonElement>('.finish-button')!.click(); fixture.detectChanges();
    http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`).flush({}, { status: 500, statusText: 'Unavailable' }); fixture.detectChanges();
    expect(element.querySelector<HTMLButtonElement>('.invoice-button')!.disabled).toBe(true);
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    http.expectNone(`${environment.apiUrl}/notas-fiscais/emitir`);
  });

  it('transmits the reviewed sale and shows authorization only after receiving the protocol', async () => {
    await prepareEmission();
    expect(element.querySelector('.emission-result')).toBeNull();
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const req = http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`);
    expect(req.request.body.ambiente).toBe(1); expect(req.request.body.clienteId).toBe(7);
    expect(req.request.body.itens[0].precoUnitario).toBe(42.9); expect(req.request.body.itens[0].origem).toBe('0');
    expect(req.request.body.destinatario.codigoMunicipio).toBe('4314902');
    expect(JSON.stringify(JSON.parse(localStorage.getItem(draftKey)!))).not.toContain('fixture-only');
    req.flush(authorized); fixture.detectChanges();
    expect(element.querySelector('.emission-result')!.textContent).toContain('Autorizada');
    expect(element.querySelector('.emission-result')!.textContent).toContain(authorized.protocolo);
    expect(element.querySelector('.invoice-button:not(.new-sale-button)')).toBeNull();
    expect(element.querySelector<HTMLButtonElement>('.new-sale-button')!.disabled).toBe(false);
  });

  it('persists the sale CSOSN and reuses it when recovering an unknown response', async () => {
    await prepareEmission();
    const select = element.querySelector<HTMLSelectElement>('[aria-label="CSOSN desta venda"]')!;
    select.value = '102'; select.dispatchEvent(new Event('change')); fixture.detectChanges(); await fixture.whenStable();
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const first = http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`);
    expect(first.request.body.csosn).toBe('102');
    const requestId = first.request.body.solicitacaoId;
    first.flush({}, { status: 500, statusText: 'Disconnected' }); fixture.detectChanges();
    expect(element.querySelector<HTMLSelectElement>('[aria-label="CSOSN desta venda"]')!.closest('fieldset')!.disabled).toBe(true);
    fixture.destroy(); fixture = TestBed.createComponent(SalesComponent); element = fixture.nativeElement; fixture.detectChanges(); catalog([product], 'producao');
    await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLSelectElement>('[aria-label="CSOSN desta venda"]')!.value).toBe('102');
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const retry = http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`);
    expect(retry.request.body.csosn).toBe('102'); expect(retry.request.body.solicitacaoId).toBe(requestId);
    retry.flush(authorized); fixture.detectChanges();
  });

  it('recovers an unknown response with the same persisted request id after reloading the page', async () => {
    await prepareEmission(); element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const first = http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`); const requestId = first.request.body.solicitacaoId;
    first.flush({}, { status: 500, statusText: 'Disconnected' }); fixture.detectChanges();
    expect(element.querySelector<HTMLFieldSetElement>('.sale-fields')!.disabled).toBe(true);
    fixture.destroy(); fixture = TestBed.createComponent(SalesComponent); element = fixture.nativeElement; fixture.detectChanges(); catalog([product], 'producao');
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const retry = http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`);
    expect(retry.request.body.solicitacaoId).toBe(requestId); expect(retry.request.body.destinatario).toEqual(destination);
    retry.flush({ ...authorized, situacao: 'Processando', codigo: '', protocolo: '', motivo: 'Sem confirmacao' }); fixture.detectChanges();
    const queryButton = [...element.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.trim() === 'Consultar autorização')!;
    queryButton.click(); http.expectOne(`${environment.apiUrl}/notas-fiscais/37/autorizacao/consultar`).flush(authorized); fixture.detectChanges();
    expect(element.querySelector('.emission-result')!.textContent).toContain(authorized.protocolo);
  });

  it('shows a SEFAZ rejection and unlocks the inputs only when the user chooses to revise', async () => {
    await prepareEmission(); element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`).flush({ ...authorized, situacao: 'Rejeitada', codigo: '539', motivo: 'Rejeicao de teste', protocolo: '' }); fixture.detectChanges();
    expect(element.querySelector('.emission-result')!.textContent).toContain('Rejeitada');
    expect(element.querySelector<HTMLFieldSetElement>('.sale-fields')!.disabled).toBe(true);
    [...element.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.trim() === 'Revisar dados para nova tentativa')!.click(); fixture.detectChanges();
    http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`).flush(destination); fixture.detectChanges();
    expect(element.querySelector<HTMLFieldSetElement>('.sale-fields')!.disabled).toBe(false);
  });
  it('starts another sale while the registered invoice remains pending in the NF-e menu', async () => {
    await prepareEmission();
    Object.defineProperty(element.querySelector('dialog')!, 'close', { value: vi.fn() });
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`).flush({ ...authorized, situacao: 'Processando', codigo: '', protocolo: '', motivo: 'Aguardando SEFAZ' });
    fixture.detectChanges();
    expect(TestBed.inject(AlertService).notify).toHaveBeenCalledWith(expect.stringContaining('Aguardando SEFAZ'), 'info');
    element.querySelector<HTMLButtonElement>('.new-sale-button')!.click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(TestBed.inject(AlertService).confirm).toHaveBeenCalledWith(expect.stringContaining('menu NF-e'), 'Iniciar nova venda?');
    expect(element.querySelector<HTMLFieldSetElement>('.sale-fields')!.disabled).toBe(false);
    expect(element.querySelector('.emission-result')).toBeNull();
    expect(element.querySelectorAll('.items-panel tbody tr')).toHaveLength(0);
    expect(localStorage.getItem(draftKey)).toBeNull();
    http.expectNone(req => req.method === 'POST');
  });

  it('keeps the sale if the SweetAlert confirmation is dismissed', async () => {
    await addProduct();
    vi.mocked(TestBed.inject(AlertService).confirm).mockResolvedValueOnce(false);
    [...element.querySelectorAll<HTMLButtonElement>('.actions button')].find(b => b.textContent?.includes('Limpar venda'))!.click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector('.items-panel tbody')!.textContent).toContain(product.descricao);
  });

  it('does not discard an unknown transmission before recovering the same request', async () => {
    await prepareEmission(); element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`).flush({}, { status: 502, statusText: 'Unknown response' });
    [...element.querySelectorAll<HTMLButtonElement>('.actions button')].find(b => b.textContent?.includes('Limpar venda'))!.click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(TestBed.inject(AlertService).confirm).not.toHaveBeenCalled();
    expect(element.querySelector<HTMLFieldSetElement>('.sale-fields')!.disabled).toBe(true);
    expect(JSON.parse(localStorage.getItem(draftKey)!).emissionAttempted).toBe(true);
  });

  it('preserves the sale identity when correcting rejected fiscal data', async () => {
    await prepareEmission(); element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const original=http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`); const id=original.request.body.solicitacaoId;
    original.flush({ ...authorized, situacao: 'Rejeitada', codigo: '203', protocolo: '' }); fixture.detectChanges();
    [...element.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.trim() === 'Revisar dados para nova tentativa')!.click(); fixture.detectChanges();
    http.expectOne(`${environment.apiUrl}/fiscal/destinatario/7`).flush(destination); fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.invoice-button')!.click();
    const revision=http.expectOne(`${environment.apiUrl}/notas-fiscais/emitir`);
    expect(revision.request.body.vendaOrigemId).toBe(authorized.id);
    expect(revision.request.body.solicitacaoId).not.toBe(id);
    revision.flush(authorized);
  });

  it('shows the saved production environment instead of a fixed homologation label', async () => {
    fixture.destroy(); fixture = TestBed.createComponent(SalesComponent); element = fixture.nativeElement; fixture.detectChanges();
    http.expectOne(`${environment.apiUrl}/fiscal/ambiente`).flush({ environment: 'producao', emissaoDisponivel: false });
    http.expectOne(req => req.url === `${environment.apiUrl}/clientes`).flush([customer]);
    http.expectOne(req => req.url === `${environment.apiUrl}/produtos`).flush({ produtos: [product], total: 1 });
    fixture.detectChanges();
    expect(element.querySelector('.sale-environment')!.textContent).toContain('Produção');
    expect(element.querySelector('.review-environment')!.textContent).not.toContain('Homologação');
    expect(element.querySelector('.review-environment')!.textContent).toContain('Produção');
  });
  it('adds catalog products, adjusts quantity and reconciles the payment', async () => {
    await addProduct();
    element.querySelector<HTMLButtonElement>('[aria-label="Aumentar quantidade de Filtro de óleo"]')!.click();
    fixture.detectChanges();
    expect(element.querySelector('.item-total')!.textContent).toContain('85,80');
    await input('[aria-label="Desconto de Filtro de óleo"]', '10');
    expect(element.querySelector('.item-total')!.textContent).toContain('77,22');
    element.querySelector<HTMLButtonElement>('.full-payment')!.click();
    fixture.detectChanges();
    expect(element.querySelector('.paid-total')!.textContent).toContain('77,22');
    expect(element.querySelector('.payment-balance')).toBeNull();
  });

  it('never substitutes the cost for an undefined sale price', async () => {
    fixture.destroy();
    fixture = TestBed.createComponent(SalesComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
    catalog([{ ...product, preco: 0 }]);
    await input('[aria-label="Buscar produto"]', 'Filtro');
    expect(element.querySelector('.product-results')!.textContent).toContain('0,00');
    element.querySelector<HTMLButtonElement>('.product-results button')!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(element.querySelector<HTMLInputElement>('.money-input')!.value).toBe('0');
    expect(element.querySelector('.item-total')!.textContent).toContain('0,00');
    expect(element.querySelector('.items-summary .grand-total')!.textContent).toContain('0,00');
  });

  it('replaces legacy cost prices with sale prices while preserving manual edits', async () => {
    const saved = { itens: [
      { produto: product, quantidade: 2, preco: 0, desconto: 0 },
      { produto: { ...product, id: 18 }, quantidade: 1, preco: 7, desconto: 0 },
      { produto: { ...product, id: 19 }, quantidade: 1, preco: 0, desconto: 0, precoEditado: true },
      { produto: { ...product, id: 20, preco: 0 }, quantidade: 1, preco: 12.5, desconto: 0 },
    ], pagamentos: [], cliente: null, tipo: 'Venda', vendedor: '', emitir: true, modelo: '55', finalidade: 'Normal', natureza: 'Venda', cfop: '5102', serie: '1', desconto: 0, frete: 0, despesas: 0, observacoes: '' };
    localStorage.setItem(draftKey, JSON.stringify(saved));
    fixture.destroy();
    fixture = TestBed.createComponent(SalesComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
    catalog([{ ...product, preco: 0 }, { ...product, id: 18 }, { ...product, id: 19 }, { ...product, id: 20 }]);
    await fixture.whenStable();
    fixture.detectChanges();
    const prices = [...element.querySelectorAll<HTMLInputElement>('.money-input')].map(field => field.value);
    expect(prices).toEqual(['0', '7', '0', '42.9']);
    expect(element.querySelector('.items-summary .grand-total')!.textContent).toContain('49,90');
  });

  it('keeps a price intentionally set to zero after saving and reloading the draft', async () => {
    await addProduct();
    await input('.money-input', '0');
    Array.from(element.querySelectorAll<HTMLButtonElement>('.actions button')).find(button => button.textContent?.includes('Salvar rascunho'))!.click();
    expect(JSON.parse(localStorage.getItem(draftKey)!).itens[0].precoEditado).toBe(true);
    fixture.destroy();
    fixture = TestBed.createComponent(SalesComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
    catalog();
    await fixture.whenStable();
    expect(element.querySelector<HTMLInputElement>('.money-input')!.value).toBe('0');
  });
  it('keeps the optional vehicle in the company draft and restores it', async () => {
    element.querySelector<HTMLButtonElement>('.vehicle-empty')!.click();
    fixture.detectChanges();
    await input('.vehicle-fields label:first-child input', 'Volkswagen Gol');
    await input('.vehicle-fields label:last-child input', 'ABC1D23');
    element.querySelector<HTMLButtonElement>('.vehicle-done')!.click();
    fixture.detectChanges();
    const save = Array.from(element.querySelectorAll<HTMLButtonElement>('.actions button')).find(button => button.textContent?.includes('Salvar rascunho'))!;
    save.click();
    expect(JSON.parse(localStorage.getItem(draftKey)!).vehicleName).toBe('Volkswagen Gol');
    fixture.destroy();
    fixture = TestBed.createComponent(SalesComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
    catalog();
    expect(element.querySelector('.vehicle-panel')!.textContent).toContain('Volkswagen Gol');
    expect(element.querySelector('.vehicle-panel')!.textContent).toContain('ABC1D23');
  });

  it('previews the items and preserves validation before preparing a sale', async () => {
    await addProduct();
    const dialog = element.querySelector<HTMLDialogElement>('dialog')!;
    const showModal = vi.fn();
    Object.defineProperty(dialog, 'showModal', { value: showModal });
    element.querySelector<HTMLButtonElement>('.toolbar-actions button:first-child')!.click();
    expect(showModal).toHaveBeenCalledOnce();
    element.querySelector<HTMLButtonElement>('.finish-button')!.click();
    fixture.detectChanges();
    expect(element.querySelector('.notice')!.textContent).toContain('Selecione um cliente');
  });
});