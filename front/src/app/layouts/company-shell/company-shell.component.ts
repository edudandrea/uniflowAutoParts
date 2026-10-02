import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

interface CompanyMenuItem {
  label: string;
  icon: string;
  path: string;
  children?: CompanyMenuItem[];
}

@Component({
  selector: 'app-company-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, AppIconComponent],
  templateUrl: './company-shell.component.html',
  styleUrl: './company-shell.component.scss',
})
export class CompanyShellComponent {
  protected readonly logoUnavailable = signal(false);
  protected readonly expandedMenus = signal<Record<string, boolean>>({});
  protected readonly mobileMenuOpen = signal(false);

  protected readonly menuItems: CompanyMenuItem[] = [
    { label: 'Dashboard', icon: 'layout-dashboard', path: '/app/dashboard' },
    { label: 'Vendas', icon: 'shopping-cart', path: '/app/vendas' },
    { label: 'Estoque', icon: 'boxes', path: '/app/estoque' },
    { label: 'Compras', icon: 'shopping-bag', path: '/app/compras' },
    { label: 'Clientes', icon: 'users', path: '/app/clientes' },
    { label: 'Fornecedores', icon: 'truck', path: '/app/fornecedores' },
    { label: 'Produtos', icon: 'package', path: '/app/produtos' },
    { label: 'Aplicacoes', icon: 'car-front', path: '/app/aplicacoes' },
    { label: 'NF-e / NFC-e', icon: 'receipt-text', path: '/app/notas' },
    {
      label: 'Financeiro',
      icon: 'circle-dollar-sign',
      path: '/app/financeiro/visao-geral',
      children: [
        { label: 'Visao geral', icon: 'layout-dashboard', path: '/app/financeiro/visao-geral' },
        { label: 'Contas a receber', icon: 'hand-coins', path: '/app/financeiro/contas-a-receber' },
        { label: 'Contas a pagar', icon: 'receipt', path: '/app/financeiro/contas-a-pagar' },
        { label: 'Caixa', icon: 'wallet-cards', path: '/app/financeiro/caixa' },
        { label: 'Movimentacoes', icon: 'receipt-text', path: '/app/financeiro/movimentacoes' },
        { label: 'Conciliacao', icon: 'check', path: '/app/financeiro/conciliacao' },
      ],
    },
    { label: 'Relatorios', icon: 'chart-no-axes-combined', path: '/app/relatorios' },
    {
      label: 'Configuracoes',
      icon: 'settings',
      path: '/app/configuracoes/enderecos',
      children: [
        { label: 'Cadastro de enderecos', icon: 'map-pin', path: '/app/configuracoes/enderecos' },
        { label: 'Categorias', icon: 'tags', path: '/app/configuracoes/categorias' },
        { label: 'Marcas', icon: 'badge', path: '/app/configuracoes/marcas' },
      ],
    },
  ];

  protected toggleMenu(label: string): void {
    this.expandedMenus.update((menus) => ({ ...menus, [label]: !menus[label] }));
  }

  protected toggleMobileMenu(): void {
    this.mobileMenuOpen.update((open) => !open);
  }

  protected closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  protected isExpanded(label: string): boolean {
    return this.expandedMenus()[label] ?? false;
  }
}
