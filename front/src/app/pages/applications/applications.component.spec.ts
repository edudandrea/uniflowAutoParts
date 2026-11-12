import { SessionService } from '../../core/session.service';
import { sessionStub } from '../../../testing/session.fixture';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { EstoqueProduto } from '../../core/app-models';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';
import { ApplicationsComponent } from './applications.component';

describe('ApplicationsComponent API integration', () => {
  let fixture: ComponentFixture<ApplicationsComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;
  const product: EstoqueProduto = {
    id: 17, codigo: '1700', descricao: 'Peca cadastrada no banco', marca: 'Marca cadastrada',
    categoria: 'Categoria cadastrada', estoque: 3, minimo: 5, valor: 20, custo: 10, margemPreco: 100, fabricante: '901', ncm: '8708',
  };

  beforeEach(async () => {
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({
      imports: [ApplicationsComponent],
      providers: [{ provide: SessionService, useFactory: sessionStub }, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ApplicationsComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => {
    try { http.verify(); } finally { TestBed.resetTestingModule(); }
  });

  function request() {
    return http.expectOne((req) => req.url === `${environment.apiUrl}/estoque` && req.params.get('empresaId') === '1');
  }

  it('displays persisted products and stock without fabricated vehicle links', () => {
    expect(element.textContent).toContain('Carregando produtos');
    request().flush([product]);
    fixture.detectChanges();
    expect(element.querySelectorAll('.part-result').length).toBe(1);
    expect(element.querySelector('.part-summary')?.textContent).toContain(product.descricao);
    expect(element.querySelector('.part-summary')?.textContent).toContain('R$');
    (element.querySelector('.part-result') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(element.querySelector('.product-card')?.textContent).toContain('901');
    expect(element.textContent).toContain('Aplicacoes por veiculo indisponiveis');
    expect(element.querySelector('.application-form')).toBeNull();
    expect(element.textContent).not.toContain('Volkswagen');
    expect(element.textContent).not.toContain('W610/3');
  });

  it('handles an empty database without selecting a nonexistent product', () => {
    request().flush([]);
    fixture.detectChanges();
    expect(element.textContent).toContain('Nenhum produto cadastrado para esta empresa.');
    expect(element.querySelector('.part-summary')).toBeNull();
    expect((element.querySelector('.tabs button:last-child') as HTMLButtonElement).disabled).toBe(true);
  });

  it('shows failures and retries the real API instead of falling back to examples', () => {
    request().flush(null, { status: 500, statusText: 'Internal Server Error' });
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Nao foi possivel carregar');
    expect(element.querySelector('.part-result')).toBeNull();
    (element.querySelector('[role="alert"] button') as HTMLButtonElement).click();
    request().flush([product]);
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')).toBeNull();
    expect(element.querySelector('.part-result')?.textContent).toContain(product.descricao);
  });

  it('searches real products by manufacturer code and handles no results', async () => {
    request().flush([product, { ...product, id: 18, codigo: '1800', fabricante: '902', descricao: 'Outra peca' }]);
    fixture.detectChanges();
    await fixture.whenStable();
    const search = element.querySelector('input[type="search"]') as HTMLInputElement;
    search.value = '901';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(element.querySelectorAll('.part-result').length).toBe(1);
    expect(element.querySelector('.part-result')?.textContent).toContain(product.descricao);
    search.value = 'inexistente';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(element.textContent).toContain('Nenhuma peca encontrada');
    expect(element.querySelector('.part-result')).toBeNull();
  });
});
