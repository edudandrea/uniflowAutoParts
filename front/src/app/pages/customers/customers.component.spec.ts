import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SessionService } from '../../core/session.service';
import { sessionStub } from '../../../testing/session.fixture';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';
import { CustomersComponent } from './customers.component';

describe('Customer details and edition', () => {
  let fixture: ComponentFixture<CustomersComponent>; let http: HttpTestingController; let element: HTMLElement;
  const summary = { id: 7, empresaId: 1, tipoPessoa: 2, relacionamento: 1, nomeRazaoSocial: 'Cliente cadastrado', nomeFantasia: null, cpfCnpj: '88657788000154', email: 'cliente@example.test', telefone: '5432145566', celular: null, ativo: true, criadoEm: '2026-10-02T12:00:00Z' };
  const address = { id: 6, empresaId: 1, nome: 'Endereco principal', cep: '95098340', logradouro: 'Rua das Industrias', numero: '1450', complemento: 'Sala 2', bairro: 'Distrito Industrial', codigoIbgeMunicipio: '4305108', municipio: 'Caxias do Sul', uf: 'RS', pais: 'Brasil', ativo: true };
  const detail = { id: 7, criadoEm: summary.criadoEm, dados: { empresaId: 1, tipoPessoa: 2, relacionamento: 1, nomeRazaoSocial: summary.nomeRazaoSocial, nomeFantasia: null, cpfCnpj: summary.cpfCnpj, rg: null, orgaoEmissor: null, estadoCivil: null, dataNascimento: null, profissao: null, cnh: null, inscricaoEstadual: '0290123456', inscricaoMunicipal: '12345', indicadorIe: 1, consumidorFinal: false, email: summary.email, telefone: summary.telefone, celular: null, clienteDesde: '2026-10-02', origem: 'Indicacao', vendedorPadrao: null, limiteCredito: 1500, observacao: 'Observacao real do cadastro', ativo: true, enderecoId: 6, endereco: { ...address, tipo: 2, principal: true }, contatos: [{ nome: 'Contato principal', cargo: 'Gerente', email: 'contato@example.test', telefone: '5432145566', celular: null, principal: true }, { nome: 'Contato adicional', cargo: null, email: 'outro@example.test', telefone: null, celular: null, principal: false }], dadosComerciais: { condicaoPagamento: '30 dias', limiteCredito: 1500, tabelaPreco: 'Oficina', vendedorPadrao: null, origem: 'Indicacao', bloquearVenda: false, permiteFiado: true } } };
  const base = '/api/clientes';
  function click(selector: string) { element.querySelector<HTMLButtonElement>(selector)!.click(); fixture.detectChanges(); }
  async function input(name: string, value: string) { const field = element.querySelector<HTMLInputElement>('[formControlName="' + name + '"]')!; field.value = value; field.dispatchEvent(new Event('input')); fixture.detectChanges(); await fixture.whenStable(); }
  function openDetail() { click('.client-result-card'); http.expectOne(req => req.url === base + '/7' && req.params.get('empresaId') === '1').flush(detail); fixture.detectChanges(); }
  function submit() { element.querySelector('form.customer-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); fixture.detectChanges(); }
  beforeEach(async () => {
    TestBed.overrideComponent(AppIconComponent, { set: { template: '', imports: [] } });
    await TestBed.configureTestingModule({ imports: [CustomersComponent], providers: [provideHttpClient(), provideHttpClientTesting(), { provide: SessionService, useFactory: sessionStub }] }).compileComponents();
    HTMLDialogElement.prototype.showModal = function () { this.open = true; }; HTMLDialogElement.prototype.close = function () { this.open = false; };
    http = TestBed.inject(HttpTestingController); fixture = TestBed.createComponent(CustomersComponent); element = fixture.nativeElement; fixture.detectChanges();
    http.expectOne(req => req.url === '/api/enderecos').flush([address]); http.expectOne(req => req.url === base).flush([summary]); fixture.detectChanges();
  });
  afterEach(() => { try { http.verify(); } finally { TestBed.resetTestingModule(); } });

  it('navigates the data cards and opens address editing from the detail modal', () => {
    openDetail();
    const addressSection = element.querySelector<HTMLElement>('#customer-addresses')!; addressSection.scrollIntoView = vi.fn();
    element.querySelectorAll<HTMLButtonElement>('.detail-tabs button')[1].click(); fixture.detectChanges();
    expect(addressSection.scrollIntoView).toHaveBeenCalled(); expect(element.querySelectorAll('.detail-tabs [aria-current="location"]')).toHaveLength(1);
    click('.detail-add-address');
    expect(element.querySelector('.customer-modal-large[open]')).not.toBeNull(); expect(element.querySelector('.address-picker')).not.toBeNull();
  });

  it('opens real customer data in a modal instead of inline details', () => {
    openDetail(); expect(element.querySelector('.customer-detail-modal[open]')).not.toBeNull();
    expect(element.querySelector('.customer-detail-body')!.textContent).toContain('0290123456');
    expect(element.querySelector('.customer-detail-body')!.textContent).toContain('Rua das Industrias');
    expect(element.querySelector('.customer-detail-body')!.textContent).toContain('Contato adicional');
    expect(element.querySelector('.client-detail-layout')).toBeNull(); expect(element.querySelector('.client-main-tabs')).toBeNull();
  });

  it('loads edition values and persists IE without losing address, additional contacts or commercial data', async () => {
    openDetail(); click('.edit-customer'); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLInputElement>('[formControlName="inscricaoEstadual"]')!.value).toBe('0290123456');
    await input('nome', 'Cliente alterado'); await input('inscricaoEstadual', '0299999999'); submit();
    const req = http.expectOne(base + '/7'); expect(req.request.method).toBe('PUT');
    expect(req.request.body.nomeRazaoSocial).toBe('Cliente alterado'); expect(req.request.body.inscricaoEstadual).toBe('0299999999');
    expect(req.request.body.enderecoId).toBe(6); expect(req.request.body.contatos).toHaveLength(2); expect(req.request.body.contatos[1].nome).toBe('Contato adicional');
    expect(req.request.body.dadosComerciais.condicaoPagamento).toBe('30 dias'); expect(req.request.body.consumidorFinal).toBe(false);
    req.flush({ ...summary, nomeRazaoSocial: 'Cliente alterado' });
    http.expectOne(req => req.url === base + '/7').flush({ ...detail, dados: { ...detail.dados, nomeRazaoSocial: 'Cliente alterado', inscricaoEstadual: '0299999999' } }); fixture.detectChanges();
    expect(element.querySelector('.customer-detail-modal[open]')!.textContent).toContain('0299999999'); expect(element.querySelectorAll('.client-result-card')).toHaveLength(1);
  });

  it('cancels edition without sending updates', async () => {
    openDetail(); click('.edit-customer'); await input('nome', 'Nao salvar'); click('.customer-form .cancel-button');
    http.expectNone(req => req.method === 'PUT'); expect(element.querySelector('.customer-detail-modal[open]')!.textContent).toContain('Cliente cadastrado');
    expect(element.querySelector('.customer-detail-modal[open]')!.textContent).not.toContain('Nao salvar');
  });

  it('keeps edits and shows the server error when saving fails', async () => {
    openDetail(); click('.edit-customer'); await input('inscricaoEstadual', '0298888888'); submit();
    http.expectOne(base + '/7').flush('Documento ja cadastrado', { status: 409, statusText: 'Conflict' }); fixture.detectChanges();
    expect(element.querySelector('.customer-modal-large[open]')).not.toBeNull(); expect(element.querySelector('[role="alert"]')!.textContent).toContain('Documento ja cadastrado');
    expect(element.querySelector<HTMLInputElement>('[formControlName="inscricaoEstadual"]')!.value).toBe('0298888888');
  });

  it('creates a CPF customer without requiring a state registration', async () => {
    click('.hero-actions .primary-action'); expect(element.querySelector('[formControlName="inscricaoEstadual"]')).toBeNull();
    await input('nome', 'Cliente PF'); await input('documento', '12345678909'); submit();
    const req = http.expectOne(base); expect(req.request.method).toBe('POST'); expect(req.request.body.tipoPessoa).toBe(1); expect(req.request.body.inscricaoEstadual).toBeNull();
    req.flush({ ...summary, id: 8, tipoPessoa: 1, cpfCnpj: '12345678909', nomeRazaoSocial: 'Cliente PF' });
    http.expectOne(req => req.url === base + '/8').flush({ ...detail, id: 8, dados: { ...detail.dados, tipoPessoa: 1, cpfCnpj: '12345678909', nomeRazaoSocial: 'Cliente PF', inscricaoEstadual: null, indicadorIe: 9 } }); fixture.detectChanges();
    expect(element.querySelectorAll('.client-result-card')).toHaveLength(2);
  });
});
