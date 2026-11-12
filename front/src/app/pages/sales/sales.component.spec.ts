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
  const product = { id: 17, sku: 610, descricao: 'Filtro de óleo', marca: 'Marca cadastrada', preco: 42.9, ativo: true };
  const customer = { id: 7, nomeRazaoSocial: 'Cliente cadastrado', cpfCnpj: '11111111000111', ativo: true, relacionamento: 1 };
  const draftKey = sessionStub().storageKey('venda:rascunho');

  function catalog() {
    http.expectOne(req => req.url === `${environment.apiUrl}/clientes`).flush([customer]);
    http.expectOne(req => req.url === `${environment.apiUrl}/produtos`).flush({ produtos: [product], total: 1 });
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