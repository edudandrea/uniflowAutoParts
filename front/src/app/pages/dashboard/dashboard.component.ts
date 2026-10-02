import { Component } from '@angular/core';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

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
  imports: [AppIconComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  protected readonly today = new Date();

  protected readonly metrics: MetricCard[] = [];

  protected readonly chartPoints: number[] = [];

  protected readonly categories: CategoryRow[] = [];

  protected readonly latestSales: SaleRow[] = [];

  protected readonly topProducts: ProductRow[] = [];

  protected readonly quickActions: QuickAction[] = [
    { icon: 'shopping-cart', label: 'Nova venda' },
    { icon: 'search', label: 'Buscar peca' },
    { icon: 'users', label: 'Cadastrar cliente' },
    { icon: 'receipt-text', label: 'Entrada de NF' },
    { icon: 'boxes', label: 'Ajustar estoque' },
    { icon: 'chart-no-axes-combined', label: 'Relatorio de vendas' },
  ];

  protected readonly alerts: AlertItem[] = [];

  protected initials(name: string): string {
    return name
      .split(' ')
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }
}
