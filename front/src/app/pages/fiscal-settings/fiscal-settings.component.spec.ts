import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
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
  const settings = { regime: '1', icmsCode: '101', pisCode: '49', cofinsCode: '49', icmsRate: null, pisRate: 1.2345, cofinsRate: 0 };

  beforeEach(async () => {
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({
      imports: [FiscalSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: SessionService, useFactory: sessionStub }],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(FiscalSettingsComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => {
    try { http.verify(); } finally { TestBed.resetTestingModule(); }
  });

  it('loads company settings and persists them through the API', async () => {
    expect(element.querySelector<HTMLFieldSetElement>('fieldset')!.disabled).toBe(true);
    http.expectOne(url).flush(settings);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(element.querySelector<HTMLInputElement>('[name="pisRate"]')!.value).toBe('1.2345');
    const storage = vi.spyOn(Storage.prototype, 'setItem');
    element.querySelector<HTMLButtonElement>('.tax-actions .primary')!.click();
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
    element.querySelector<HTMLButtonElement>('.tax-actions button:last-child')!.click();
    const restore = http.expectOne(url);
    expect(restore.request.method).toBe('GET');
    restore.flush({ ...settings, pisRate: 2 });
    fixture.detectChanges();
    expect(element.textContent).toContain('Última configuração salva da empresa restaurada.');
  });

  it('prevents saving after a failed load and allows retry', () => {
    http.expectOne(url).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(element.querySelector<HTMLButtonElement>('.tax-actions .primary')!.disabled).toBe(true);
    element.querySelector<HTMLButtonElement>('.tax-actions button:last-child')!.click();
    http.expectOne(url).flush(null);
    fixture.detectChanges();
    expect(element.querySelector<HTMLButtonElement>('.tax-actions .primary')!.disabled).toBe(false);
  });

  it('does not report success when saving fails', () => {
    http.expectOne(url).flush(settings);
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.tax-actions .primary')!.click();
    http.expectOne(url).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(element.textContent).toContain('Não foi possível salvar a configuração tributária');
    expect(element.textContent).not.toContain('Configuração tributária salva no banco');
  });
});
