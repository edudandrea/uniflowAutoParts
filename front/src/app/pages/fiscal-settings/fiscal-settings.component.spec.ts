import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FiscalCertificateService } from '../../core/fiscal-certificate.service';
import { SessionService } from '../../core/session.service';
import { sessionStub } from '../../../testing/session.fixture';
import { environment } from '../../../environments/environment';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';
import { FiscalSettingsComponent } from './fiscal-settings.component';

describe('Fiscal settings database persistence', () => {
  let fixture: ComponentFixture<FiscalSettingsComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;
  const url = `${environment.apiUrl}/fiscal/tributacao`;
  const issuer = { cnpj: '12345678000195', dados: { razaoSocial: 'Empresa de teste', inscricaoEstadual: '2243658790', logradouro: 'Rua de teste', numero: '100', complemento: '', bairro: 'Centro', municipio: 'Porto Alegre', codigoMunicipio: '4314902', uf: 'RS', cep: '90000000', serie: 1, proximoNumero: 1 } };
  const settings = { regime: '1', icmsCode: '101', pisCode: '49', cofinsCode: '49', icmsRate: null, pisRate: 1.2345, cofinsRate: 0 };

  beforeEach(async () => {
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({
      imports: [FiscalSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: SessionService, useFactory: sessionStub }],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(FiscalCertificateService).save({ fileName: 'test.pfx', base64: 'test-only', password: 'test-only', uf: 'RS', environment: 'homologacao' });
    fixture = TestBed.createComponent(FiscalSettingsComponent);
    element = fixture.nativeElement;
    http.expectOne(`${environment.apiUrl}/fiscal/emitente`).flush(issuer);
    http.expectOne(`${environment.apiUrl}/fiscal/ambiente`).flush({ environment: 'homologacao', emissaoDisponivel: false });
    fixture.detectChanges();
  });

  afterEach(() => {
    try { http.match(req => req.url === `${environment.apiUrl}/fiscal/emitente` && req.method === 'GET').forEach(req => req.flush(issuer)); http.verify(); } finally { TestBed.resetTestingModule(); localStorage.clear(); vi.restoreAllMocks(); }
  });

  async function selectProduction() {
    const select = element.querySelector<HTMLSelectElement>('[aria-label="Ambiente fiscal"]')!;
    select.value = 'producao'; select.dispatchEvent(new Event('change'));
    fixture.detectChanges(); await fixture.whenStable();
    element.querySelector<HTMLButtonElement>('.environment-controls .primary')!.click();
  }

  it('persists production through the API without changing the existing certificate', async () => {
    http.expectOne(url).flush(settings); fixture.detectChanges(); await fixture.whenStable();
    const before = TestBed.inject(FiscalCertificateService).load();
    await selectProduction();
    const save = http.expectOne(`${environment.apiUrl}/fiscal/ambiente`);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ environment: 'producao' });
    save.flush({ environment: 'producao', emissaoDisponivel: false }); fixture.detectChanges();
    expect(element.querySelector('.certificate-details')!.textContent).toContain('Produção');
    expect(element.querySelector('.certificate-details')!.textContent).not.toContain('Homologação');
    expect(TestBed.inject(FiscalCertificateService).load()).toEqual(before);
    expect(element.textContent).toContain('Ambiente fiscal salvo no banco');
  });

  it('keeps the previous environment when saving fails', async () => {
    http.expectOne(url).flush(settings); fixture.detectChanges(); await fixture.whenStable();
    await selectProduction();
    http.expectOne(`${environment.apiUrl}/fiscal/ambiente`).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(element.querySelector('.certificate-details')!.textContent).toContain('Homologação');
    expect(element.textContent).toContain('A configuração anterior continua em uso');
  });

  it('reports the actual environment returned by the SEFAZ connection test', async () => {
    http.expectOne(url).flush(settings); fixture.detectChanges(); await fixture.whenStable();
    await selectProduction();
    http.expectOne(`${environment.apiUrl}/fiscal/ambiente`).flush({ environment: 'producao', emissaoDisponivel: false });
    fixture.detectChanges();
    const test = [...element.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.includes('Testar conexão'))!;
    test.click();
    http.expectOne(`${environment.apiUrl}/fiscal/sefaz/status`).flush({ codigo: '107', motivo: 'Servico em operacao', ambiente: 'producao' });
    fixture.detectChanges();
    expect(element.textContent).toContain('SEFAZ/RS · Produção: 107');
    expect(element.textContent).toContain('Esta consulta não emite nota fiscal');
  });
  it('allows retrying a failed environment load without pretending an environment is selected', async () => {
    http.expectOne(url).flush(settings);
    fixture.destroy(); fixture = TestBed.createComponent(FiscalSettingsComponent); element = fixture.nativeElement; fixture.detectChanges();
    http.expectOne(url).flush(settings);
    http.expectOne(`${environment.apiUrl}/fiscal/ambiente`).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLSelectElement>('[aria-label="Ambiente fiscal"]')!.disabled).toBe(true);
    expect(element.querySelector('.certificate-details')!.textContent).toContain('Ambiente não carregado');
    element.querySelector<HTMLButtonElement>('.environment-controls button:last-child')!.click();
    http.expectOne(`${environment.apiUrl}/fiscal/ambiente`).flush({ environment: 'producao', emissaoDisponivel: false });
    fixture.detectChanges();
    expect(element.querySelector('.certificate-details')!.textContent).toContain('Produção');
  });
  it('saves issuer identity, address and the next invoice number', async () => {
    http.expectOne(url).flush(settings); fixture.detectChanges(); await fixture.whenStable();
    const cnpj = element.querySelector<HTMLInputElement>('[name="issuerCnpj"]')!;
    expect(cnpj.value).toBe(issuer.cnpj);
    cnpj.value = '12.345.678/0001-95'; cnpj.dispatchEvent(new Event('input')); fixture.detectChanges();
    const series = element.querySelector<HTMLInputElement>('[name="issuerSeries"]')!;
    series.value = '2'; series.dispatchEvent(new Event('input')); fixture.detectChanges();
    const next = element.querySelector<HTMLInputElement>('[name="issuerNext"]')!;
    next.value = '50'; next.dispatchEvent(new Event('input')); fixture.detectChanges();
    const form = element.querySelector('.issuer-fields')!.closest('form')!;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const save = http.expectOne(req => req.url === `${environment.apiUrl}/fiscal/emitente` && req.method === 'PUT');
    expect(save.request.body.serie).toBe(2); expect(save.request.body.proximoNumero).toBe(50);
    expect(save.request.body.cnpj).toBe('12.345.678/0001-95');
    save.flush({ ...issuer, dados: { ...issuer.dados, serie: 2, proximoNumero: 50 } }); fixture.detectChanges();
    expect(element.textContent).toContain('Emitente e numeração fiscal salvos');
  });
  it('loads company settings and persists them through the API', async () => {
    expect(element.querySelector<HTMLFieldSetElement>('.tax-card fieldset')!.disabled).toBe(true);
    http.expectOne(url).flush(settings);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(element.querySelector<HTMLInputElement>('[name="pisRate"]')!.value).toBe('1.2345');
    const storage = vi.spyOn(Storage.prototype, 'setItem');
    element.querySelector<HTMLButtonElement>('.tax-card .tax-actions .primary')!.click();
    const save = http.expectOne(url);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual(settings);
    save.flush(settings);
    fixture.detectChanges();
    expect(element.textContent).toContain('salva no banco de dados da empresa');
    expect(storage).not.toHaveBeenCalled();
    storage.mockRestore();
  });

  it('restores the last settings from the server', () => {
    http.expectOne(url).flush(settings);
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.tax-card .tax-actions button:last-child')!.click();
    const restore = http.expectOne(url);
    expect(restore.request.method).toBe('GET');
    restore.flush({ ...settings, pisRate: 2 });
    fixture.detectChanges();
    expect(element.textContent).toContain('Última configuração salva da empresa restaurada.');
  });

  it('prevents saving after a failed load and allows retry', () => {
    http.expectOne(url).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(element.querySelector<HTMLButtonElement>('.tax-card .tax-actions .primary')!.disabled).toBe(true);
    element.querySelector<HTMLButtonElement>('.tax-card .tax-actions button:last-child')!.click();
    http.expectOne(url).flush(null);
    fixture.detectChanges();
    expect(element.querySelector<HTMLButtonElement>('.tax-card .tax-actions .primary')!.disabled).toBe(false);
  });

  it('does not report success when saving fails', () => {
    http.expectOne(url).flush(settings);
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.tax-card .tax-actions .primary')!.click();
    http.expectOne(url).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(element.textContent).toContain('Não foi possível salvar a configuração tributária');
    expect(element.textContent).not.toContain('Configuração tributária salva no banco');
  });
});
