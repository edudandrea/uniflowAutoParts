import { companySessionGuard } from './core/session.service';
import { Routes } from '@angular/router';

import { CompanyShellComponent } from './layouts/company-shell/company-shell.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';

export const routes: Routes = [
  { path: 'sso', loadComponent: () => import('./pages/hub-login/hub-login.component').then(m => m.HubLoginComponent) },
  { path: 'entrar', loadComponent: () => import('./pages/hub-login/hub-login.component').then(m => m.HubLoginComponent) },
  {
    path: 'app',
    canActivate: [companySessionGuard],
    canActivateChild: [companySessionGuard],
    component: CompanyShellComponent,
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'estoque', loadComponent: () => import('./pages/inventory/inventory.component').then(m => m.InventoryComponent) },
      { path: 'clientes', loadComponent: () => import('./pages/customers/customers.component').then(m => m.CustomersComponent) },
      { path: 'produtos', loadComponent: () => import('./pages/products/products.component').then(m => m.ProductsComponent) },
      { path: 'aplicacoes', loadComponent: () => import('./pages/applications/applications.component').then(m => m.ApplicationsComponent) },
      { path: 'compras', loadComponent: () => import('./pages/purchases/purchases.component').then(m => m.PurchasesComponent) },
      { path: 'vendas', loadComponent: () => import('./pages/sales/sales.component').then(m => m.SalesComponent) },
      { path: 'notas', loadComponent: () => import('./pages/invoices/invoices.component').then(m => m.InvoicesComponent) },
      { path: 'fornecedores', loadComponent: () => import('./pages/suppliers/suppliers.component').then(m => m.SuppliersComponent) },
      { path: 'financeiro', redirectTo: 'financeiro/visao-geral', pathMatch: 'full' },
      { path: 'financeiro/visao-geral', loadComponent: () => import('./pages/financial/financial.component').then(m => m.FinancialComponent) },
      { path: 'financeiro/contas-a-receber', data: { receber: true }, loadComponent: () => import('./pages/financial-titles/financial-titles.component').then(m => m.FinancialTitlesComponent) },
      { path: 'financeiro/contas-a-pagar', loadComponent: () => import('./pages/financial-titles/financial-titles.component').then(m => m.FinancialTitlesComponent) },
      { path: 'financeiro/caixa', loadComponent: () => import('./pages/cash/cash.component').then(m => m.CashComponent) },
      { path: 'financeiro/movimentacoes', loadComponent: () => import('./pages/financial-movements/financial-movements.component').then(m => m.FinancialMovementsComponent) },
      { path: 'financeiro/conciliacao', loadComponent: () => import('./pages/financial/financial.component').then(m => m.FinancialComponent) },
      { path: 'configuracoes/enderecos', loadComponent: () => import('./pages/addresses/addresses.component').then(m => m.AddressesComponent) },
      { path: 'configuracoes/municipios', loadComponent: () => import('./pages/municipalities/municipalities.component').then(m => m.MunicipalitiesComponent) },
      { path: 'configuracoes/categorias', loadComponent: () => import('./pages/categories/categories.component').then(m => m.CategoriesComponent) },
      { path: 'configuracoes/marcas', loadComponent: () => import('./pages/brands/brands.component').then(m => m.BrandsComponent) },
      { path: 'configuracoes/certificado-digital', loadComponent: () => import('./pages/fiscal-settings/fiscal-settings.component').then(m => m.FiscalSettingsComponent) },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: '**', redirectTo: 'dashboard' },
    ],
  },
  { path: '', pathMatch: 'full', redirectTo: 'app/dashboard' },
  { path: '**', redirectTo: 'app/dashboard' },
];
