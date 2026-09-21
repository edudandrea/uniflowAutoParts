import { Component, inject } from '@angular/core';

import { AuthSessionService } from '../../core/auth-session.service';

interface MetricCard {
  icon: string;
  label: string;
  value: string;
  delta: string;
}

interface CategoryRow {
  icon: string;
  label: string;
  percent: number;
}

interface SaleRow {
  id: string;
  cliente: string;
  itens: number;
  total: string;
  status: string;
}

interface ProductRow {
  posicao: number;
  produto: string;
  qtd: number;
  receita: string;
}

interface QuickAction {
  icon: string;
  label: string;
}

interface AlertItem {
  icon: string;
  tone: string;
  title: string;
  detail: string;
}

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  protected readonly auth = inject(AuthSessionService);
  protected readonly today = new Date();

  protected readonly metrics: MetricCard[] = [];

  protected readonly chartPoints: number[] = [];

  protected readonly categories: CategoryRow[] = [];

  protected readonly latestSales: SaleRow[] = [];

  protected readonly topProducts: ProductRow[] = [];

  protected readonly quickActions: QuickAction[] = [
    { icon: 'shopping_cart', label: 'Nova venda' },
    { icon: 'search', label: 'Buscar peca' },
    { icon: 'person_add', label: 'Cadastrar cliente' },
    { icon: 'description', label: 'Entrada de NF' },
    { icon: 'inventory_2', label: 'Ajustar estoque' },
    { icon: 'bar_chart', label: 'Relatorio de vendas' },
  ];

  protected readonly alerts: AlertItem[] = [];

  protected initials(name: string | undefined | null): string {
    return (name || 'U')
      .split(' ')
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }
}
