import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from '../../core/session.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';
import { CompanyLogoComponent } from '../../shared/company-logo/company-logo.component';

interface CompanyMenuItem {
  label: string;
  icon: string;
  path: string;
  children?: CompanyMenuItem[];
}

@Component({
  selector: 'app-company-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, AppIconComponent, CompanyLogoComponent],
  templateUrl: './company-shell.component.html',
  styleUrl: './company-shell.component.scss',
  host: { '(window:keydown)': 'handleShortcut($event)' },
})
export class CompanyShellComponent {
  private readonly router = inject(Router);
  protected readonly session = inject(SessionService);
  private readonly searchInput = viewChild.required<ElementRef<HTMLInputElement>>('searchInput');
  protected readonly signingOut = signal(false);
  protected readonly sessionError = signal('');
  protected readonly searchOpen = signal(false);
  protected readonly searchTerm = signal('');
  protected readonly expandedMenus = signal<Record<string, boolean>>({});
  protected readonly mobileMenuOpen = signal(false);
  protected readonly userInitials = computed(() => {
    const names = this.session.session()?.usuario?.nome.trim().split(/\s+/) ?? [];
    const first = names[0]?.charAt(0) || 'U';
    const last = names.length > 1 ? names.at(-1)?.charAt(0) ?? '' : '';
    return (first + last).toUpperCase();
  });

  protected readonly menuItems: CompanyMenuItem[] = [
    { label: 'Dashboard', icon: 'layout-dashboard', path: '/app/dashboard' },
    { label: 'Vendas', icon: 'shopping-cart', path: '/app/vendas' },
    { label: 'Estoque', icon: 'boxes', path: '/app/estoque' },
    { label: 'Produtos', icon: 'package', path: '/app/produtos' },
    { label: 'Compras', icon: 'shopping-bag', path: '/app/compras' },
    { label: 'Clientes', icon: 'users', path: '/app/clientes' },
    { label: 'Fornecedores', icon: 'truck', path: '/app/fornecedores' },
    { label: 'NF-e / NFC-e', icon: 'receipt-text', path: '/app/notas' },
    {
      label: 'Financeiro',
      icon: 'circle-dollar-sign',
      path: '/app/financeiro/visao-geral',
      children: [
        { label: 'Visão geral', icon: 'layout-dashboard', path: '/app/financeiro/visao-geral' },
        { label: 'Contas a receber', icon: 'hand-coins', path: '/app/financeiro/contas-a-receber' },
        { label: 'Contas a pagar', icon: 'receipt', path: '/app/financeiro/contas-a-pagar' },
        { label: 'Caixa', icon: 'wallet-cards', path: '/app/financeiro/caixa' },
        { label: 'Movimentações', icon: 'receipt-text', path: '/app/financeiro/movimentacoes' },
        { label: 'Conciliação', icon: 'check', path: '/app/financeiro/conciliacao' },
      ],
    },
    { label: 'Aplicações', icon: 'car-front', path: '/app/aplicacoes' },
    {
      label: 'Configurações',
      icon: 'settings',
      path: '/app/configuracoes/enderecos',
      children: [
        { label: 'Cadastro de endereços', icon: 'map-pin', path: '/app/configuracoes/enderecos' },
        { label: 'Cadastro de municípios', icon: 'map-pin', path: '/app/configuracoes/municipios' },
        { label: 'Categorias', icon: 'tags', path: '/app/configuracoes/categorias' },
        { label: 'Marcas', icon: 'badge', path: '/app/configuracoes/marcas' },
        { label: 'Certificado e tributações', icon: 'lock', path: '/app/configuracoes/certificado-digital' },
      ],
    },
  ];

  protected readonly searchResults = computed(() => {
    const term = this.normalize(this.searchTerm().trim());
    if (!term) return this.menuItems.slice(0, 6);
    return this.menuItems.flatMap(item =>
      this.normalize(item.label).includes(term) ? [item] :
        (item.children ?? []).filter(child => this.normalize(child.label).includes(term)),
    ).slice(0, 8);
  });

  protected logout(): void {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    this.sessionError.set('');
    this.session.logout().subscribe({
      next: result => window.location.assign(result.portalUrl || '/entrar'),
      error: () => {
        this.signingOut.set(false);
        this.sessionError.set('Não foi possível encerrar a sessão. Tente novamente.');
      },
    });
  }

  protected toggleMenu(label: string, active: boolean): void {
    this.expandedMenus.update(menus => ({ ...menus, [label]: !this.isExpanded(label, active) }));
  }

  protected isExpanded(label: string, active: boolean): boolean {
    return this.expandedMenus()[label] ?? active;
  }

  protected toggleMobileMenu(): void {
    this.mobileMenuOpen.update(open => !open);
  }

  protected closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  protected handleShortcut(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k' && !document.querySelector('dialog[open]')) {
      event.preventDefault();
      this.closeMobileMenu();
      this.searchInput().nativeElement.focus();
      this.searchOpen.set(true);
    }
  }

  protected closeSearchOnBlur(event: FocusEvent): void {
    if (!(event.currentTarget as HTMLElement).contains(event.relatedTarget as Node | null)) {
      this.searchOpen.set(false);
    }
  }

  protected closeAccountOnBlur(account: HTMLDetailsElement, event: FocusEvent): void {
    if (!account.contains(event.relatedTarget as Node | null)) account.open = false;
  }

  protected closeSearch(): void {
    this.searchTerm.set('');
    this.searchOpen.set(false);
  }

  protected openFirstResult(): void {
    const match = this.searchResults()[0];
    if (!match || !this.searchOpen()) return;
    this.closeSearch();
    void this.router.navigateByUrl(match.path);
  }

  private normalize(value: string): string {
    return value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
  }
}
