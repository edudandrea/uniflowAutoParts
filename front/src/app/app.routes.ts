import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';

import { AuthSessionService } from './core/auth-session.service';
import { CompanyShellComponent } from './layouts/company-shell/company-shell.component';
import { SaasShellComponent } from './layouts/saas-shell/saas-shell.component';
import { AddressesComponent } from './pages/addresses/addresses.component';
import { BrandsComponent } from './pages/brands/brands.component';
import { CategoriesComponent } from './pages/categories/categories.component';
import { CompaniesComponent } from './pages/companies/companies.component';
import { CustomersComponent } from './pages/customers/customers.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { InventoryComponent } from './pages/inventory/inventory.component';
import { LoginComponent } from './pages/login/login.component';
import { ProductsComponent } from './pages/products/products.component';
import { PurchasesComponent } from './pages/purchases/purchases.component';
import { SuppliersComponent } from './pages/suppliers/suppliers.component';
import { UsersComponent } from './pages/users/users.component';

const adminSaasGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);

  return auth.isAdministradorSaas() || router.parseUrl('/login');
};

const empresaGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);
  const usuario = auth.usuario();

  return (usuario && usuario.perfil !== 'AdministradorSaas') || router.parseUrl('/login');
};

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: 'saas',
    component: SaasShellComponent,
    canActivate: [adminSaasGuard],
    children: [
      { path: 'empresas', component: CompaniesComponent },
      { path: 'usuarios', component: UsersComponent },
      { path: '', pathMatch: 'full', redirectTo: 'empresas' },
    ],
  },
  {
    path: 'app',
    component: CompanyShellComponent,
    canActivate: [empresaGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'estoque', component: InventoryComponent },
      { path: 'clientes', component: CustomersComponent },
      { path: 'produtos', component: ProductsComponent },
      { path: 'compras', component: PurchasesComponent },
      { path: 'fornecedores', component: SuppliersComponent },
      { path: 'configuracoes/enderecos', component: AddressesComponent },
      { path: 'configuracoes/categorias', component: CategoriesComponent },
      { path: 'configuracoes/marcas', component: BrandsComponent },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: '**', redirectTo: 'dashboard' },
    ],
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' },
];
