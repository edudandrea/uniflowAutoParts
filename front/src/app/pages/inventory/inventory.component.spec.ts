import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { environment } from '../../../environments/environment';
import { EstoqueProduto } from '../../core/app-models';
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
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
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
