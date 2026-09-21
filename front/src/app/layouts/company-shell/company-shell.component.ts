import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthSessionService } from '../../core/auth-session.service';

interface CompanyMenuItem {
  label: string;
  icon: string;
  path: string;
  children?: CompanyMenuItem[];
}

@Component({
  selector: 'app-company-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './company-shell.component.html',
  styleUrl: './company-shell.component.scss',
})
export class CompanyShellComponent {
  protected readonly auth = inject(AuthSessionService);
  protected readonly logoUnavailable = signal(false);
  protected readonly expandedMenus = signal<Record<string, boolean>>({});
  private readonly router = inject(Router);

  protected readonly menuItems: CompanyMenuItem[] = [
    { label: 'Dashboard', icon: 'grid_view', path: '/app/dashboard' },
    { label: 'Vendas', icon: 'shopping_cart', path: '/app/vendas' },
    { label: 'Estoque', icon: 'inventory_2', path: '/app/estoque' },
    { label: 'Compras', icon: 'local_mall', path: '/app/compras' },
    { label: 'Clientes', icon: 'group', path: '/app/clientes' },
    { label: 'Fornecedores', icon: 'local_shipping', path: '/app/fornecedores' },
    { label: 'Produtos', icon: 'deployed_code', path: '/app/produtos' },
    { label: 'Aplicacoes', icon: 'apps', path: '/app/aplicacoes' },
    { label: 'NF-e / NFC-e', icon: 'description', path: '/app/notas' },
    { label: 'Financeiro', icon: 'paid', path: '/app/financeiro' },
    { label: 'Relatorios', icon: 'bar_chart', path: '/app/relatorios' },
    {
      label: 'Configuracoes',
      icon: 'settings',
      path: '/app/configuracoes/enderecos',
      children: [
        { label: 'Cadastro de enderecos', icon: 'map', path: '/app/configuracoes/enderecos' },
        { label: 'Categorias', icon: 'category', path: '/app/configuracoes/categorias' },
        { label: 'Marcas', icon: 'sell', path: '/app/configuracoes/marcas' },
      ],
    },
  ];

  protected toggleMenu(label: string): void {
    this.expandedMenus.update((menus) => ({ ...menus, [label]: !menus[label] }));
  }

  protected isExpanded(label: string): boolean {
    return this.expandedMenus()[label] ?? false;
  }

  protected sair() {
    this.auth.sair();
    this.router.navigateByUrl('/login');
  }
}
