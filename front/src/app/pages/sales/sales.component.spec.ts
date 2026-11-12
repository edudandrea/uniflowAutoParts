import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/session.service';
import { sessionStub } from '../../../testing/session.fixture';
import { environment } from '../../../environments/environment';
import { appConfig } from '../../app.config';
import { SalesComponent } from './sales.component';

 describe('Sales workspace', () => {
  let fixture: ComponentFixture<SalesComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;
  const product = { id: 17, sku: 610, descricao: 'Filtro de óleo', marca: 'Marca cadastrada', preco: 42.9, custo: 12.5, ativo: true };
  const customer = { id: 7, nomeRazaoSocial: 'Cliente cadastrado', cpfCnpj: '11111111000111', ativo: true, relacionamento: 1 };
  const draftKey = sessionStub().storageKey('venda:rascunho');

  function catalog(products = [product]) {
    http.expectOne(req => req.url === `${environment.apiUrl}/clientes`).flush([customer]);
    http.expectOne(req => req.url === `${environment.apiUrl}/produtos`).flush({ produtos: products, total: products.length });
    fixture.detectChanges();
  }

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [SalesComponent],
      providers: [...appConfig.providers, provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: SessionService, useFactory: sessionStub }],
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