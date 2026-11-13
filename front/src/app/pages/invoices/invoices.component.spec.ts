import { AlertService } from '../../core/alert.service';
import { SessionService } from '../../core/session.service';
import { sessionStub } from '../../../testing/session.fixture';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { NotaFiscal, NotaFiscalListaResponse } from '../../core/app-models';
import { FiscalCertificateService } from '../../core/fiscal-certificate.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';
import { InvoicesComponent } from './invoices.component';

describe('InvoicesComponent', () => {
  let fixture: ComponentFixture<InvoicesComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;
  const certificate = { base64: 'certificado-de-teste', password: 'senha-de-teste', fileName: 'teste.pfx', uf: 'RS', environment: 'homologacao' };
  const certificates = { load: vi.fn(() => certificate) };
  const nota: NotaFiscal = {
    id: 7, numero: '1700', serie: '1', emissao: '2026-11-06T12:00:00Z', cliente: 'Cliente cadastrado', documentoCliente: '12345678909',
    tipo: 'NFe', situacao: 'Autorizada', valor: 250.5, chaveAcesso: '43261112345678000195550010000000011123456780',
    ambiente: 2, temPdf: true, podeCancelar: true, podeCorrigir: true, bloqueioEventos: null,
  };
  function response(notas: NotaFiscal[] = [nota], total = notas.length, pagina = 1): NotaFiscalListaResponse {
    return { notas, total, pagina, tamanhoPagina: 10, resumo: { emitidas: total, autorizadas: total, canceladas: 0, processando: 0, valorTotal: 250.5 * total } };
  }
  const api = `${environment.apiUrl}/notas-fiscais`;
  function listRequest() { return http.expectOne(req => req.url === api && req.params.get('empresaId') === '1'); }
  function click(selector: string) { (element.querySelector(selector) as HTMLButtonElement).click(); fixture.detectChanges(); }
  function action(text: string) { return [...element.querySelectorAll<HTMLButtonElement>('.row-actions button')].find(b => b.textContent?.includes(text))!; }

  beforeEach(async () => {
    certificates.load.mockReturnValue(certificate);
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({
      imports: [InvoicesComponent], providers: [{ provide: SessionService, useFactory: sessionStub }, provideHttpClient(), provideHttpClientTesting(), { provide: AlertService, useValue: { notify: vi.fn(), confirm: vi.fn().mockResolvedValue(true) } }, { provide: FiscalCertificateService, useValue: certificates }],
    }).compileComponents();
    HTMLDialogElement.prototype.showModal = function () { this.open = true; };
    HTMLDialogElement.prototype.close = function () { this.open = false; };
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InvoicesComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
  });
  afterEach(() => { try { http.verify(); } finally { TestBed.resetTestingModule(); vi.restoreAllMocks(); } });

  const rejected = { ...nota, situacao: 'Rejeitada' as const, podeCancelar: false, podeCorrigir: false, podeReenviar: true, codigoAutorizacao: '203', motivoAutorizacao: 'Emissor não habilitado' };
  function openResend() { listRequest().flush(response([rejected])); fixture.detectChanges(); click('[aria-label="Reenviar XML da nota 1700"]'); }
  const authorization = { id: 7, numero: nota.numero, serie: nota.serie, chaveAcesso: nota.chaveAcesso, ambiente: 2, situacao: 'Autorizada', codigo: '100', motivo: 'Autorizado o uso da NF-e', protocolo: '143260000000001', podeReenviar: false };

  it('resends the stored XML only after the user requests it and shows the SEFAZ protocol', () => {
    openResend();
    expect(element.querySelector('.resend-dialog[open]')).not.toBeNull();
    expect(element.querySelector('.previous-return')!.textContent).toContain('203');
    http.expectNone(req => req.url.endsWith('/reenviar'));
    click('.resend-submit');
    const req = http.expectOne(api + '/7/autorizacao/reenviar');
    expect(req.request.method).toBe('POST'); expect(req.request.body).toEqual({ base64: certificate.base64, password: certificate.password });
    expect(element.querySelector<HTMLButtonElement>('.resend-submit')!.disabled).toBe(true);
    req.flush(authorization); listRequest().flush(response([{ ...nota, podeReenviar: false, codigoAutorizacao: '100' }])); fixture.detectChanges();
    expect(element.querySelector('.resend-result')!.textContent).toContain('143260000000001');
    expect(element.querySelector('.resend-result')!.textContent).toContain('Autorizado o uso');
    expect(element.querySelector('.resend-result')!.textContent).toContain(nota.chaveAcesso);
    expect(element.querySelector('.resend-submit')).toBeNull();
  });

  it('shows the full rejection returned by SEFAZ after a resend', () => {
    openResend(); click('.resend-submit');
    http.expectOne(api + '/7/autorizacao/reenviar').flush({ ...authorization, situacao: 'Rejeitada', codigo: '225', motivo: 'Falha no Schema XML do lote de NFe', protocolo: '', podeReenviar: true });
    listRequest().flush(response([{ ...rejected, codigoAutorizacao: '225' }])); fixture.detectChanges();
    expect(element.querySelector('.resend-result.is-rejected')!.textContent).toContain('225');
    expect(element.querySelector('.resend-result')!.textContent).toContain('Falha no Schema XML');
    expect(element.querySelector('.resend-result')!.textContent).not.toContain('Protocolo:');
  });

  it('keeps an unconfirmed resend as an error without inventing a SEFAZ return', () => {
    openResend(); click('.resend-submit');
    http.expectOne(api + '/7/autorizacao/reenviar').flush({ detail: 'Consulta sem confirmação' }, { status: 502, statusText: 'Bad Gateway' });
    listRequest().flush(response([rejected])); fixture.detectChanges();
    expect(element.querySelector('.resend-dialog [role="alert"]')!.textContent).toContain('Consulta sem confirmação');
    expect(element.querySelector('.resend-result')).toBeNull();
  });

  it('does not offer resend for an authorized invoice or send without an A1 certificate', () => {
    listRequest().flush(response()); fixture.detectChanges();
    expect(element.querySelector<HTMLButtonElement>('[aria-label="Reenviar XML da nota 1700"]')!.disabled).toBe(true);
    click('.filters-panel .primary'); listRequest().flush(response([rejected])); fixture.detectChanges();
    click('[aria-label="Reenviar XML da nota 1700"]'); certificates.load.mockReturnValue(null as never); click('.resend-submit');
    expect(element.querySelector('.resend-dialog [role="alert"]')!.textContent).toContain('certificado A1');
    http.expectNone(req => req.url.endsWith('/reenviar'));
  });

  it('loads real API records and renders the requested fiscal actions', () => {
    expect(element.textContent).toContain('Carregando notas fiscais');
    listRequest().flush(response()); fixture.detectChanges();
    expect(element.querySelector('tbody')?.textContent).toContain(nota.cliente);
    expect(element.querySelector('.date-cell')?.textContent).toContain('06/11/2026 09:00');
    expect(action('PDF')).toBeTruthy(); expect(action('XML')).toBeTruthy();
    expect(action('Carta de correção').disabled).toBe(false); expect(action('Cancelar').disabled).toBe(false);
    expect(element.textContent).not.toContain('Oficina Mecânica Rossi');
  });

  it('requests the next page in groups of ten and handles the final partial page', () => {
    listRequest().flush(response(Array.from({ length: 10 }, (_, i) => ({ ...nota, id: i + 1 })), 12)); fixture.detectChanges();
    expect(element.querySelectorAll('tbody tr')).toHaveLength(10);
    click('[aria-label="Próxima página"]');
    const req = listRequest(); expect(req.request.params.get('pagina')).toBe('2');
    req.flush(response([{ ...nota, id: 11 }, { ...nota, id: 12 }], 12, 2)); fixture.detectChanges();
    expect(element.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(element.querySelector('.pagination')?.textContent).toContain('Mostrando 11 a 12 de 12 registros');
    expect((element.querySelector('[aria-label="Próxima página"]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('sends search, type, situation, dates and advanced filters to the API', async () => {
    listRequest().flush(response()); fixture.detectChanges(); await fixture.whenStable();
    click('.advanced-toggle'); await fixture.whenStable();
    const setInput = (name: string, value: string, event = 'input') => {
      const input = element.querySelector(`[name="${name}"]`) as HTMLInputElement;
      input.value = value; input.dispatchEvent(new Event(event));
    };
    setInput('busca', '1700'); setInput('tipo', 'NFe', 'change'); setInput('situacao', 'Autorizada', 'change');
    setInput('de', '2026-11-01'); setInput('ate', '2026-11-06'); setInput('serie', '1'); setInput('ambiente', '2', 'change');
    setInput('valorMinimo', '10'); setInput('valorMaximo', '300');
    element.querySelector('.filters-panel')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const req = listRequest();
    expect(req.request.params.get('busca')).toBe('1700'); expect(req.request.params.get('tipo')).toBe('NFe');
    expect(req.request.params.get('situacao')).toBe('Autorizada'); expect(req.request.params.get('de')).toBe('2026-11-01');
    expect(req.request.params.get('ate')).toBe('2026-11-06'); expect(req.request.params.get('ambiente')).toBe('2');
    expect(req.request.params.get('valorMinimo')).toBe('10'); expect(req.request.params.get('valorMaximo')).toBe('300');
    expect(req.request.params.get('serie')).toBe('1'); expect(req.request.params.get('pagina')).toBe('1');
    req.flush(response());
  });

  it('shows an empty database without inventing invoices', () => {
    listRequest().flush(response([])); fixture.detectChanges();
    expect(element.textContent).toContain('Nenhuma nota fiscal encontrada');
    expect(element.querySelector('.row-actions')).toBeNull();
    expect(element.querySelector('.pagination')?.textContent).toContain('Mostrando 0 a 0 de 0 registros');
  });

  it('handles API failures and allows retrying', () => {
    listRequest().flush({ detail: 'Falha de conexao' }, { status: 500, statusText: 'Error' }); fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Falha de conexao');
    click('[role="alert"] button'); listRequest().flush(response()); fixture.detectChanges();
    expect(element.querySelector('.invoice-number')?.textContent).toContain('1700');
  });

  it('loads confirmed event history and exposes its protocol without fabricating confirmation', async () => {
    listRequest().flush(response([{ ...nota, ambiente: 1 }])); fixture.detectChanges();
    action('Eventos').click();
    http.expectOne(req => req.url === `${api}/7/eventos`).flush([{ id: 12, tipo: '110110', sequencia: 1, situacao: 'Confirmado', codigo: '135', motivo: 'Evento registrado', protocolo: '143260000000002', texto: 'Corrigir observacao de entrega', criadoEm: '2026-11-12T12:00:00Z' }]);
    fixture.detectChanges();
    expect(element.querySelector('#history-title')!.textContent).toContain('1700');
    expect(element.querySelector('.event-history-item')!.textContent).toContain('143260000000002');
    expect(element.querySelector<HTMLButtonElement>('.event-history-item button')!.disabled).toBe(false);
  });
  it('requires confirmation and only updates cancellation after the API returns a protocol', async () => {
    listRequest().flush(response()); fixture.detectChanges(); action('Cancelar').click(); fixture.detectChanges(); await fixture.whenStable();
    expect(element.querySelector('dialog[open]')).not.toBeNull();
    http.expectNone(req => req.url.endsWith('/cancelar'));
    const noCirculation = element.querySelector<HTMLInputElement>('input[name="noCirculation"]');
    if (noCirculation) { noCirculation.checked = true; noCirculation.dispatchEvent(new Event('change')); fixture.detectChanges(); }
    const textarea = element.querySelector('textarea') as HTMLTextAreaElement;
    textarea.value = 'Cancelamento solicitado pelo cliente'; textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    element.querySelector('dialog[open] form')!.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
    const req = http.expectOne(`${api}/7/cancelar?empresaId=1`);
    expect(req.request.method).toBe('POST'); expect(req.request.body.texto).toBe(textarea.value);
    expect(req.request.body.base64).toBe(certificate.base64); expect(req.request.body.confirmarNaoCirculacao).toBe(true);
    expect(element.querySelector('tbody')?.textContent).not.toContain('Cancelada');
    req.flush({ codigo: '135', motivo: 'Evento registrado', protocolo: '143260000000002', sequencia: 1 });
    listRequest().flush(response([{ ...nota, situacao: 'Cancelada', podeCancelar: false, podeCorrigir: false }])); fixture.detectChanges();
    expect(element.querySelector('dialog[open]')).toBeNull();
    expect(element.textContent).toContain('143260000000002');
    expect(element.querySelector('tbody')?.textContent).toContain('Cancelada');
  });

  it('keeps a rejected correction visible and does not fabricate an approved event', async () => {
    listRequest().flush(response()); fixture.detectChanges(); action('Carta de correção').click(); fixture.detectChanges(); await fixture.whenStable();
    const noCirculation = element.querySelector<HTMLInputElement>('input[name="noCirculation"]');
    if (noCirculation) { noCirculation.checked = true; noCirculation.dispatchEvent(new Event('change')); fixture.detectChanges(); }
    const textarea = element.querySelector('textarea') as HTMLTextAreaElement;
    textarea.value = 'Corrigir observacao da nota fiscal'; textarea.dispatchEvent(new Event('input')); fixture.detectChanges();
    element.querySelector('dialog[open] form')!.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
    http.expectOne(`${api}/7/carta-correcao?empresaId=1`).flush({ detail: 'SEFAZ 580: Evento exige nota autorizada' }, { status: 422, statusText: 'Rejected' });
    listRequest().flush(response()); fixture.detectChanges();
    expect(element.querySelector('dialog[open] [role="alert"]')?.textContent).toContain('SEFAZ 580');
    expect(element.querySelector('.notice')).toBeNull();
  });

  it('disables unavailable fiscal operations and missing PDFs', () => {
    listRequest().flush(response([{ ...nota, tipo: 'NFCe', temPdf: false, podeCorrigir: false }])); fixture.detectChanges();
    expect(action('Carta de correção').disabled).toBe(true); expect(action('PDF').disabled).toBe(true);
    expect(action('XML').disabled).toBe(false);
  });

  it('downloads the actual XML returned by the API', () => {
    listRequest().flush(response()); fixture.detectChanges();
    const create = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    action('XML').click();
    const blob = new Blob(['<nfeProc/>'], { type: 'application/xml' });
    http.expectOne(`${api}/7/xml?empresaId=1`).flush(blob);
    expect(create).toHaveBeenCalledWith(blob);
  });

  it('consults the SEFAZ and refreshes the persisted situation', () => {
    listRequest().flush(response()); fixture.detectChanges(); click('[aria-label="Consultar nota 1700 na SEFAZ"]');
    http.expectOne(`${api}/7/consultar?empresaId=1`).flush({ codigo: '101', motivo: 'Cancelamento homologado', situacao: 'Cancelada', pendente: false });
    listRequest().flush(response([{ ...nota, situacao: 'Cancelada', podeCancelar: false, podeCorrigir: false }])); fixture.detectChanges();
    expect(element.querySelector('tbody')?.textContent).toContain('Cancelada');
    expect(element.querySelector('.notice')?.textContent).toContain('Cancelamento homologado');
    expect(TestBed.inject(AlertService).notify).toHaveBeenCalledWith(expect.stringContaining('Cancelamento homologado'), 'success');
  });
});
