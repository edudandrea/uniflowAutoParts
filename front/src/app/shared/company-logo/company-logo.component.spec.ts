import { TestBed } from '@angular/core/testing';
import { SessionService } from '../../core/session.service';
import { sessionStub, TEST_SESSION } from '../../../testing/session.fixture';
import { CompanyLogoComponent } from './company-logo.component';

describe('CompanyLogoComponent', () => {
  it('uses the authenticated company logo and updates after a company change', async () => {
    const session = sessionStub();
    session.session.set({ ...TEST_SESSION, empresa: { ...TEST_SESSION.empresa!, logoUrl: '/company-a.png' } });
    await TestBed.configureTestingModule({ imports: [CompanyLogoComponent], providers: [{ provide: SessionService, useValue: session }] }).compileComponents();
    const fixture = TestBed.createComponent(CompanyLogoComponent);
    fixture.detectChanges();
    let img = fixture.nativeElement.querySelector('img') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('/company-a.png');
    expect(img.alt).toBe('Empresa de teste');
    img.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Empresa de teste');
    session.session.set({ ...TEST_SESSION, empresa: { ...TEST_SESSION.empresa!, id: 2, nome: 'Empresa B', logoUrl: '/company-b.png' } });
    fixture.detectChanges();
    img = fixture.nativeElement.querySelector('img');
    expect(img.getAttribute('src')).toBe('/company-b.png');
    expect(img.alt).toBe('Empresa B');
  });

  it('shows the company name when no logo is registered', async () => {
    await TestBed.configureTestingModule({ imports: [CompanyLogoComponent], providers: [{ provide: SessionService, useValue: sessionStub() }] }).compileComponents();
    const fixture = TestBed.createComponent(CompanyLogoComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Empresa de teste');
  });
});
