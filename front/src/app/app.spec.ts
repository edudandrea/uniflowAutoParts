import { SessionService } from './core/session.service';
import { sessionStub } from '../testing/session.fixture';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { AppIconComponent } from './shared/app-icon/app-icon.component';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [{ provide: SessionService, useFactory: sessionStub }, provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('opens the fiscal menu at its own page instead of redirecting to the dashboard', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await TestBed.inject(Router).navigateByUrl('/app/notas');
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(req => req.url.endsWith('/notas-fiscais')).flush({
      pagina: 1, tamanhoPagina: 10, total: 0, notas: [],
      resumo: { emitidas: 0, autorizadas: 0, processando: 0, canceladas: 0, valorTotal: 0 },
    });
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Notas Fiscais');
    expect(TestBed.inject(Router).url).toBe('/app/notas');
    http.verify();
  });
});
