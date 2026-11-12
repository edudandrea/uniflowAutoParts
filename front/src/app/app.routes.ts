import { companySessionGuard } from './core/session.service';
import { Routes } from '@angular/router';

import { CompanyShellComponent } from './layouts/company-shell/company-shell.component';
import { AddressesComponent } from './pages/addresses/addresses.component';
import { ApplicationsComponent } from './pages/applications/applications.component';
import { BrandsComponent } from './pages/brands/brands.component';
import { CategoriesComponent } from './pages/categories/categories.component';
import { CustomersComponent } from './pages/customers/customers.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { FinancialComponent } from './pages/financial/financial.component';
import { InventoryComponent } from './pages/inventory/inventory.component';
import { ProductsComponent } from './pages/products/products.component';
import { PurchasesComponent } from './pages/purchases/purchases.component';
import { SuppliersComponent } from './pages/suppliers/suppliers.component';
import { FiscalSettingsComponent } from './pages/fiscal-settings/fiscal-settings.component';

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
      { path: 'estoque', component: InventoryComponent },
      { path: 'clientes', component: CustomersComponent },
      { path: 'produtos', component: ProductsComponent },
      { path: 'aplicacoes', component: ApplicationsComponent },
      { path: 'compras', component: PurchasesComponent },
      { path: 'vendas', loadComponent: () => import('./pages/sales/sales.component').then(m => m.SalesComponent) },
      { path: 'notas', loadComponent: () => import('./pages/invoices/invoices.component').then(m => m.InvoicesComponent) },
      { path: 'fornecedores', component: SuppliersComponent },
      { path: 'financeiro', redirectTo: 'financeiro/visao-geral', pathMatch: 'full' },
      { path: 'financeiro/visao-geral', component: FinancialComponent },
      { path: 'financeiro/contas-a-receber', component: FinancialComponent },
      { path: 'financeiro/contas-a-pagar', component: FinancialComponent },
      { path: 'financeiro/caixa', component: FinancialComponent },
      { path: 'financeiro/movimentacoes', component: FinancialComponent },
      { path: 'financeiro/conciliacao', component: FinancialComponent },
      { path: 'configuracoes/enderecos', component: AddressesComponent },
      { path: 'configuracoes/categorias', component: CategoriesComponent },
      { path: 'configuracoes/marcas', component: BrandsComponent },
      { path: 'configuracoes/certificado-digital', component: FiscalSettingsComponent },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: '**', redirectTo: 'dashboard' },
    ],
  },
  { path: '', pathMatch: 'full', redirectTo: 'app/dashboard' },
  { path: '**', redirectTo: 'app/dashboard' },
];
