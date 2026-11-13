import { AlertService } from '../../core/alert.service';
import { SessionService } from '../../core/session.service';
import { sessionStub } from '../../../testing/session.fixture';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { environment } from '../../../environments/environment';
import { EstoqueProduto, ImportarNfeResponse } from '../../core/app-models';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';
import { InventoryComponent } from './inventory.component';

describe('InventoryComponent API integration', () => {
  let fixture: ComponentFixture<InventoryComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  const product: EstoqueProduto = {
    id: 17, codigo: '1700', descricao: 'Peca cadastrada no banco', marca: 'Marca cadastrada',
    categoria: 'Categoria cadastrada', estoque: 3, minimo: 5, valor: 20, custo: 10, margemPreco: 100, fabricante: '901', ncm: '8708',
  };

  beforeEach(async () => {
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({
      imports: [InventoryComponent],
      providers: [{ provide: SessionService, useFactory: sessionStub }, provideHttpClient(), provideHttpClientTesting(), { provide: AlertService, useValue: { notify: vi.fn(), confirm: vi.fn().mockResolvedValue(true) } }, provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InventoryComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
    http.expectOne(req => req.url.endsWith('/estoque/margem')).flush({ margemPreco: null });
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
    }
  });

  function request() {
    return http.expectOne((req) => req.url === `${environment.apiUrl}/estoque` && req.params.get('empresaId') === '1');
  }

  function clickButton(text: string) {
    const button = [...element.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.trim() === text)!;
    button.click(); fixture.detectChanges();
  }

  const nota: ImportarNfeResponse = {
    entradaCompraId: 7, numeroNfe: '123', serie: '1', chaveNfe: '1'.repeat(44), modelo: '55',
    dataEmissao: '2026-11-06T12:00:00Z', dataEntrada: null, naturezaOperacao: 'Compra',
    fornecedorNome: 'Fornecedor da nota', fornecedorDocumento: '12345678000195',
    valorProdutos: 40, valorFrete: 0, valorSeguro: 0, valorDesconto: 0, valorOutrasDespesas: 0, valorTotal: 40,
    totalItens: 1, produtosIdentificados: 0, produtosPendentes: 1, status: 'Conferencia',
    itens: [{ numeroItem: 1, codigoFornecedor: 'ABC', descricaoXml: 'Peca da NF-e', ean: null, ncm: null, cest: null, cfop: null,
      unidadeComercial: 'UN', quantidadeComercial: 2, valorUnitarioComercial: 20, unidadeTributavel: 'UN', quantidadeTributavel: 2,
      valorUnitarioTributavel: 20, valorProduto: 40, valorFrete: 0, valorSeguro: 0, valorDesconto: 0, valorOutrasDespesas: 0,
      quantidadeRecebida: 2, custoUnitario: 20, produtoIdentificado: false, produtoId: null }],
  };
  const entriesUrl = `${environment.apiUrl}/estoque/entradas`;
  const resumo = { ...nota, importadoEm: '2026-11-10T12:00:00Z' };

  it('applies a single global margin regardless of visible filters and persists it after reload', async () => {
    request().flush([{ ...product, custo: 100, valor: 0, margemPreco: null }]);
    fixture.detectChanges(); await fixture.whenStable();
    expect(element.querySelector('.margin-editor')).toBeNull();
    expect(element.querySelectorAll('#global-margin')).toHaveLength(1);
    const search=element.querySelector<HTMLInputElement>('.search-control input')!;
    search.value='Filtro sem resultados'; search.dispatchEvent(new Event('input')); fixture.detectChanges();
    const margin=element.querySelector<HTMLInputElement>('#global-margin')!;
    margin.value='30'; margin.dispatchEvent(new Event('input')); fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.global-pricing .primary-action')!.click();
    await fixture.whenStable();
    const save=http.expectOne(req=>req.url.endsWith('/estoque/margem')&&req.method==='PUT');
    expect(save.request.params.get('empresaId')).toBe('1'); expect(save.request.body).toEqual({ margemPreco: 30 });
    save.flush({ margemPreco: 30, produtosAtualizados: 8 });
    request().flush([{ ...product, custo: 100, valor: 130, margemPreco: 30 }]); fixture.detectChanges();
    expect(element.querySelector('.pricing-feedback')!.textContent).toContain('8 produto(s)');
    expect(TestBed.inject(AlertService).notify).toHaveBeenCalledWith(expect.stringContaining('8 produto(s)'), 'success');
    fixture.destroy(); fixture=TestBed.createComponent(InventoryComponent); element=fixture.nativeElement; fixture.detectChanges();
    http.expectOne(req=>req.url.endsWith('/estoque/margem')).flush({ margemPreco: 30 });
    request().flush([{ ...product, custo: 100, valor: 130, margemPreco: 30 }]); fixture.detectChanges(); await fixture.whenStable();
    expect(element.querySelector<HTMLInputElement>('#global-margin')!.value).toBe('30');
    expect(element.querySelector('.sale-price')!.textContent).toContain('130,00');
  });
  it('preserves prices and the entered margin when applying the global margin fails', async () => {
    request().flush([product]); fixture.detectChanges(); await fixture.whenStable();
    const margin=element.querySelector<HTMLInputElement>('#global-margin')!; margin.value='40'; margin.dispatchEvent(new Event('input')); fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.global-pricing .primary-action')!.click(); await fixture.whenStable();
    http.expectOne(req=>req.url.endsWith('/estoque/margem')&&req.method==='PUT').flush({}, { status:500, statusText:'Error' }); fixture.detectChanges();
    expect(element.querySelector('.sale-price')!.textContent).toContain('20,00'); expect(margin.value).toBe('40');
    expect(TestBed.inject(AlertService).notify).toHaveBeenCalledWith(expect.stringContaining('Não foi possível confirmar'), 'error');
  });
  it('does not update any price when the global confirmation is dismissed', async () => {
    request().flush([product]); fixture.detectChanges();
    vi.mocked(TestBed.inject(AlertService).confirm).mockResolvedValueOnce(false);
    const margin=element.querySelector<HTMLInputElement>('#global-margin')!; margin.value='30'; margin.dispatchEvent(new Event('input')); fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.global-pricing .primary-action')!.click(); await fixture.whenStable();
    http.expectNone(req=>req.method==='PUT'); expect(element.querySelector('.sale-price')!.textContent).toContain('20,00');
  });
  it('reopens an imported invoice, saves received quantities and resumes after closing', async () => {
    request().flush([product]); fixture.detectChanges();
    clickButton('Conferir NF-e');
    http.expectOne(req => req.url === entriesUrl && req.params.get('tenantId') === '1').flush([resumo]);
    fixture.detectChanges();
    expect(element.querySelector('.inventory-detail-tab')).toBeNull();
    clickButton('Conferir itens');
    http.expectOne(req => req.url === `${entriesUrl}/7`).flush(nota);
    fixture.detectChanges(); await fixture.whenStable();
    const select = element.querySelector<HTMLSelectElement>('select[aria-label="Produto do item 1"]')!;
    select.value = select.options[1].value; select.dispatchEvent(new Event('change')); fixture.detectChanges();
    const quantity = element.querySelector<HTMLInputElement>('input[aria-label="Quantidade recebida do item 1"]')!;
    quantity.value = '1.5'; quantity.dispatchEvent(new Event('input')); fixture.detectChanges();
    clickButton('Salvar conferência');
    const save = http.expectOne(req => req.url === `${entriesUrl}/7/conferencia` && req.method === 'PUT');
    expect(save.request.body).toEqual({ itens: [{ numeroItem: 1, produtoId: 17, quantidadeRecebida: 1.5 }], concluir: false });
    const salva = { ...nota, produtosIdentificados: 1, produtosPendentes: 0,
      itens: [{ ...nota.itens[0], produtoId: 17, produtoIdentificado: true, quantidadeRecebida: 1.5 }] };
    save.flush(salva);
    request().flush([product]);
    http.expectOne(req => req.url === entriesUrl).flush([resumo]); fixture.detectChanges();
    (element.querySelector('.close-button') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(element.querySelector('[role="dialog"]')).toBeNull();
    clickButton('Conferir itens');
    http.expectOne(req => req.url === `${entriesUrl}/7`).flush(salva); fixture.detectChanges(); await fixture.whenStable();
    expect(element.querySelector<HTMLInputElement>('.received-quantity')?.value).toBe('1.5');
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Fornecedor da nota');
  });

  it('keeps edits on save failure and makes completed conferences read only', async () => {
    request().flush([product]); fixture.detectChanges();
    clickButton('Conferir NF-e');
    http.expectOne(req => req.url === entriesUrl).flush([resumo]); fixture.detectChanges();
    clickButton('Conferir itens');
    const vinculada = { ...nota, produtosIdentificados: 1, produtosPendentes: 0,
      itens: [{ ...nota.itens[0], produtoId: 17, produtoIdentificado: true }] };
    http.expectOne(req => req.url === `${entriesUrl}/7`).flush(vinculada); fixture.detectChanges(); await fixture.whenStable();
    clickButton('Salvar conferência');
    http.expectOne(req => req.url.endsWith('/conferencia')).flush('Falha ao salvar', { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Falha ao salvar');
    expect(element.querySelector('[role="dialog"]')).not.toBeNull();
    clickButton('Concluir conferência');
    const concluir = http.expectOne(req => req.url.endsWith('/conferencia'));
    expect(concluir.request.body.concluir).toBe(true);
    concluir.flush({ ...vinculada, status: 'Conferida' });
    request().flush([product]);
    http.expectOne(req => req.url === entriesUrl).flush([{ ...resumo, status: 'Conferida' }]); fixture.detectChanges();
    await fixture.whenStable();
    expect(element.querySelector<HTMLInputElement>('.received-quantity')?.disabled).toBe(true);
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Conferência concluída.');
  });

  it('opens the saved conference when the XML was already imported', async () => {
    request().flush([product]); fixture.detectChanges();
    clickButton('Importar XML NF-e');
    const file = new File(['<xml/>'], 'nota.xml', { type: 'text/xml' });
    Object.defineProperty(file, 'text', { value: async () => '<NFe><infNFe><ide><mod>55</mod></ide><det><prod/></det></infNFe></NFe>' });
    const input = element.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(input, 'files', { value: [file] });
    input.dispatchEvent(new Event('change')); await fixture.whenStable(); fixture.detectChanges();
    clickButton('Importar e conferir');
    http.expectOne(req => req.url.endsWith('/estoque/importar-nfe')).flush({ message: 'Já importada', entradaCompraId: 7 },
      { status: 409, statusText: 'Conflict' });
    http.expectOne(req => req.url === `${entriesUrl}/7`).flush(nota); fixture.detectChanges();
    request().flush([product]); fixture.detectChanges();
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Conferir itens da NF-e');
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Peca da NF-e');
  });

  it('creates missing products for an older note and fills the association list without losing received quantities', async () => {
    request().flush([]); fixture.detectChanges();
    clickButton('Conferir NF-e');
    http.expectOne(req => req.url === entriesUrl).flush([resumo]); fixture.detectChanges();
    clickButton('Conferir itens');
    http.expectOne(req => req.url === `${entriesUrl}/7`).flush(nota); fixture.detectChanges(); await fixture.whenStable();
    const quantity = element.querySelector<HTMLInputElement>('.received-quantity')!;
    quantity.value = '1.5'; quantity.dispatchEvent(new Event('input')); fixture.detectChanges();
    clickButton('Cadastrar produtos pendentes');
    const save = http.expectOne(req => req.url === `${entriesUrl}/7/conferencia`);
    expect(save.request.body.itens[0].quantidadeRecebida).toBe(1.5);
    http.expectNone(req => req.url === `${entriesUrl}/7/produtos`);
    const salva = { ...nota, produtosPendentes: 0, produtosIdentificados: 1,
      itens: [{ ...nota.itens[0], produtoId: 17, produtoIdentificado: true, quantidadeRecebida: 1.5 }] };
    save.flush({ ...nota, itens: [{ ...nota.itens[0], quantidadeRecebida: 1.5 }] });
    http.expectOne(req => req.url === `${entriesUrl}/7/produtos` && req.method === 'POST' && req.params.get('tenantId') === '1').flush(salva);
    request().flush([product]); fixture.detectChanges(); await fixture.whenStable();
    const select = element.querySelector<HTMLSelectElement>('select[aria-label="Produto do item 1"]')!;
    expect(select.selectedOptions[0].textContent).toContain(product.descricao);
    expect(element.querySelector<HTMLInputElement>('.received-quantity')?.value).toBe('1.5');
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Produtos cadastrados e associados');
  });

  it('refreshes products automatically after a successful XML import', async () => {
    request().flush([]); fixture.detectChanges();
    clickButton('Importar XML NF-e');
    const file = new File(['<xml/>'], 'nota.xml', { type: 'text/xml' });
    Object.defineProperty(file, 'text', { value: async () => '<NFe><infNFe><ide><mod>55</mod></ide><det><prod/></det></infNFe></NFe>' });
    const input = element.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(input, 'files', { value: [file] });
    input.dispatchEvent(new Event('change')); await fixture.whenStable(); fixture.detectChanges();
    clickButton('Importar e conferir');
    http.expectOne(req => req.url.endsWith('/estoque/importar-nfe')).flush({ ...nota, produtosIdentificados: 1, produtosPendentes: 0,
      itens: [{ ...nota.itens[0], produtoId: 17, produtoIdentificado: true }] });
    request().flush([product]); fixture.detectChanges(); await fixture.whenStable();
    const select = element.querySelector<HTMLSelectElement>('select[aria-label="Produto do item 1"]')!;
    expect(select.selectedOptions[0].textContent).toContain(product.descricao);
    expect(element.querySelector('[role="dialog"]')?.textContent).not.toContain('Cadastrar produtos pendentes');
  });

  it('reopens a repeated XML successfully and refreshes its stock without showing an object as an error', async () => {
    request().flush([product]); fixture.detectChanges();
    clickButton('Importar XML NF-e');
    const file = new File(['<xml/>'], 'nota.xml', { type: 'text/xml' });
    Object.defineProperty(file, 'text', { value: async () => '<NFe><infNFe><ide><mod>55</mod></ide><det><prod/></det></infNFe></NFe>' });
    const input = element.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(input, 'files', { value: [file] });
    input.dispatchEvent(new Event('change')); await fixture.whenStable(); fixture.detectChanges();
    clickButton('Importar e conferir');
    http.expectOne(req => req.url.endsWith('/estoque/importar-nfe')).flush({ ...nota, jaImportada: true });
    request().flush([{ ...product, estoque: 2 }]); fixture.detectChanges();
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('sem duplicar a entrada');
    expect(element.textContent).not.toContain('[object Object]');
    expect(element.querySelector('.metric-card strong')?.textContent).toBe('2');
  });

  it.each([{ message: 'Esta nota já foi importada.' }, { detail: 'Falha na validação da nota.' }])(
    'shows structured API error messages as readable text: %j', async (body) => {
      request().flush([]); fixture.detectChanges();
      clickButton('Importar XML NF-e');
      const file = new File(['<xml/>'], 'nota.xml', { type: 'text/xml' });
      Object.defineProperty(file, 'text', { value: async () => '<NFe><infNFe><ide><mod>55</mod></ide><det><prod/></det></infNFe></NFe>' });
      const input = element.querySelector<HTMLInputElement>('input[type="file"]')!;
      Object.defineProperty(input, 'files', { value: [file] });
      input.dispatchEvent(new Event('change')); await fixture.whenStable(); fixture.detectChanges();
      clickButton('Importar e conferir');
      http.expectOne(req => req.url.endsWith('/estoque/importar-nfe')).flush(body, { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();
      expect(element.querySelector('[role="alert"]')?.textContent).toContain('message' in body ? body.message : body.detail);
      expect(element.textContent).not.toContain('[object Object]');
    },
  );

  it('loads persisted items and computes stock indicators from the response', () => {
    expect(element.textContent).toContain('Carregando estoque');
    expect(element.textContent).not.toContain('W610/3');
    request().flush([product]);
    fixture.detectChanges();
    expect(element.querySelector('tbody')?.textContent).toContain(product.descricao);
    expect(element.querySelector('tbody')?.textContent).toContain('Estoque baixo');
    const metrics = [...element.querySelectorAll('.metric-card strong')].map((node) => node.textContent?.trim());
    expect(metrics[0]).toBe('3');
    expect(metrics[1]).toContain('30,00');
    expect(metrics.slice(2)).toEqual(['1', '0']);
  });

  it('shows an empty database without injecting sample products', () => {
    request().flush([]);
    fixture.detectChanges();
    expect(element.textContent).toContain('Nenhum produto cadastrado para esta empresa.');
    expect(element.querySelectorAll('tbody .selection-checkbox').length).toBe(0);
    expect(element.textContent).not.toContain('Bosch');
  });

  it('shows request failures and allows retrying instead of showing fake stock', () => {
    request().flush(null, { status: 500, statusText: 'Internal Server Error' });
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Nao foi possivel carregar o estoque');
    expect(element.querySelector('.metrics-grid')).toBeNull();
    (element.querySelector('[role="alert"] button') as HTMLButtonElement).click();
    request().flush([product]);
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')).toBeNull();
    expect(element.querySelector('tbody')?.textContent).toContain(product.descricao);
  });

  it('filters the loaded items by brand and stock status', async () => {
    request().flush([product, { ...product, id: 18, codigo: '1800', descricao: 'Outra peca', marca: 'Outra marca', estoque: 0 }]);
    fixture.detectChanges();
    await fixture.whenStable();
    const brand = element.querySelector('select[aria-label="Marca"]') as HTMLSelectElement;
    brand.value = product.marca;
    brand.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(element.querySelector('tbody')?.textContent).toContain(product.descricao);
    expect(element.querySelector('tbody')?.textContent).not.toContain('Outra peca');
    const status = element.querySelector('select[aria-label="Status"]') as HTMLSelectElement;
    status.value = 'Sem estoque';
    status.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(element.querySelector('tbody')?.textContent).toContain('Nenhum item encontrado');
  });
});
