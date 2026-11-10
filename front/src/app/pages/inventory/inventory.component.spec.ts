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
    categoria: 'Categoria cadastrada', estoque: 3, minimo: 5, valor: 20, fabricante: '901', ncm: '8708',
  };

  beforeEach(async () => {
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({
      imports: [InventoryComponent],
      providers: [{ provide: SessionService, useFactory: sessionStub }, provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InventoryComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
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
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Conferir itens da NF-e');
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Peca da NF-e');
  });

  it('loads persisted items and computes stock indicators from the response', () => {
    expect(element.textContent).toContain('Carregando estoque');
    expect(element.textContent).not.toContain('W610/3');
    request().flush([product]);
    fixture.detectChanges();
    expect(element.querySelector('tbody')?.textContent).toContain(product.descricao);
    expect(element.querySelector('tbody')?.textContent).toContain('Estoque baixo');
    const metrics = [...element.querySelectorAll('.metric-card strong')].map((node) => node.textContent?.trim());
    expect(metrics[0]).toBe('3');
    expect(metrics[1]).toContain('60,00');
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
