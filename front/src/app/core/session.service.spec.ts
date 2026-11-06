import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom, Observable } from 'rxjs';
import { TEST_SESSION } from '../../testing/session.fixture';
import { FiscalCertificateService } from './fiscal-certificate.service';
import { companySessionGuard, SessionService, sessionInterceptor } from './session.service';

describe('Hub session and company isolation', () => {
  let service: SessionService;
  let http: HttpTestingController;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionInterceptor])), provideHttpClientTesting()] });
    service = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { try { http.verify(); } finally { TestBed.resetTestingModule(); localStorage.clear(); } });

  it('loads company and user only from the authenticated backend session', () => {
    expect(() => service.empresaId).toThrow();
    service.load().subscribe();
    const req = http.expectOne('/api/auth/session');
    expect(req.request.method).toBe('GET');
    req.flush(TEST_SESSION);
    expect(service.empresaId).toBe(1);
    expect(service.session()?.usuario?.nome).toBe('Usuario de teste');
    expect(localStorage.getItem('uniflow:session:identity')).toBe('1:central-1');
    expect(localStorage.getItem('token')).toBeNull();
  });

  it('does not grant access from a stored identifier without a server session', () => {
    localStorage.setItem('uniflow:session:identity', '1:central-1');
    service.load().subscribe();
    http.expectOne('/api/auth/session').flush({ authenticated: false, available: true, portalUrl: null });
    expect(() => service.empresaId).toThrow();
    expect(localStorage.getItem('uniflow:session:identity')).toBeNull();
  });

  it('adds CSRF protection to writes and never sends it to an external host', () => {
    service.session.set(TEST_SESSION);
    const client = TestBed.inject(HttpClient);
    client.post('/api/produtos', { descricao: 'Produto' }).subscribe();
    const write = http.expectOne('/api/produtos'); expect(write.request.headers.get('X-Parts-CSRF')).toBe('csrf-test'); write.flush({});
    client.get('/api/produtos').subscribe();
    const read = http.expectOne('/api/produtos'); expect(read.request.headers.has('X-Parts-CSRF')).toBe(false); read.flush({});
    client.post('https://external.example.test/api/documentos', {}).subscribe();
    const external = http.expectOne('https://external.example.test/api/documentos'); expect(external.request.headers.has('X-Parts-CSRF')).toBe(false); external.flush({});
  });

  it('isolates certificates, tax settings and drafts for company A, company B and another user', () => {
    const certificate = TestBed.inject(FiscalCertificateService);
    const a1 = { fileName: 'a.pfx', base64: 'test-only', password: 'test-only', uf: 'RS' as const, environment: 'homologacao' as const };
    service.session.set(TEST_SESSION);
    certificate.save(a1);
    const draftA = service.storageKey('venda:rascunho');
    localStorage.setItem(draftA, 'rascunho-A');
    service.session.set({ ...TEST_SESSION, empresa: { ...TEST_SESSION.empresa!, id: 2, nome: 'Empresa B', hubTenantId: 'central-2' } });
    expect(certificate.load()).toBeNull(); expect(localStorage.getItem(service.storageKey('venda:rascunho'))).toBeNull();
    service.session.set({ ...TEST_SESSION, usuario: { ...TEST_SESSION.usuario!, sujeito: 'central-99' } });
    expect(certificate.load()).toBeNull();
    service.session.set(TEST_SESSION);
    expect(certificate.load()?.fileName).toBe('a.pfx'); expect(localStorage.getItem(service.storageKey('venda:rascunho'))).toBe('rascunho-A');
  });

  it('does not reuse the unscoped legacy company-1 certificate', () => {
    localStorage.setItem('uniflow:certificado:empresa-1', JSON.stringify({ fileName: 'legacy.pfx' }));
    service.session.set(TEST_SESSION);
    expect(TestBed.inject(FiscalCertificateService).load()).toBeNull();
  });

  it('ends the local session through the protected API and clears the active identity', () => {
    service.session.set(TEST_SESSION);
    localStorage.setItem('uniflow:session:identity', '1:central-1');
    service.logout().subscribe();
    const req = http.expectOne('/api/auth/logout'); expect(req.request.headers.get('X-Parts-CSRF')).toBe('csrf-test'); req.flush({ portalUrl: TEST_SESSION.portalUrl });
    expect(service.session()).toBeNull(); expect(() => service.empresaId).toThrow(); expect(localStorage.getItem('uniflow:session:identity')).toBeNull();
  });

  it('redirects an unauthenticated application route to central login', async () => {
    const result = TestBed.runInInjectionContext(() => companySessionGuard({} as never, { url: '/app/estoque' } as never));
    const pending = firstValueFrom(result as Observable<unknown>);
    http.expectOne('/api/auth/session').flush({ authenticated: false, available: true, portalUrl: TEST_SESSION.portalUrl });
    const tree = await pending;
    expect(TestBed.inject(Router).serializeUrl(tree as never)).toContain('/entrar?returnTo=%2Fapp%2Festoque');
  });
});
