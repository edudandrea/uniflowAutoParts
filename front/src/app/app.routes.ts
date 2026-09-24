import { Routes } from '@angular/router';

import { CompanyShellComponent } from './layouts/company-shell/company-shell.component';
import { AddressesComponent } from './pages/addresses/addresses.component';
import { BrandsComponent } from './pages/brands/brands.component';
import { CategoriesComponent } from './pages/categories/categories.component';
import { CustomersComponent } from './pages/customers/customers.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { InventoryComponent } from './pages/inventory/inventory.component';
import { ProductsComponent } from './pages/products/products.component';
import { PurchasesComponent } from './pages/purchases/purchases.component';
import { SuppliersComponent } from './pages/suppliers/suppliers.component';

export const routes: Routes = [
  {
    path: 'app',
    component: CompanyShellComponent,
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
  { path: '', pathMatch: 'full', redirectTo: 'app/dashboard' },
  { path: '**', redirectTo: 'app/dashboard' },
];
