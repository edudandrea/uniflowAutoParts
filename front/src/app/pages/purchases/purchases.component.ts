import { Component, computed, signal } from '@angular/core';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

interface PurchaseMetric {
  icon: string;
  label: string;
  value: string;
  detail: string;
  tone: 'success' | 'danger' | 'neutral';
}

interface PurchaseOrder {
  numero: string;
  fornecedor: string;
  marca: string;
  emissao: string;
  previsao: string;
  itens: number;
  total: number;
  status: 'Enviado' | 'Parcial' | 'Recebido' | 'Em atraso' | 'Cancelado' | 'Concluido';
}

interface QuickAction {
  icon: string;
  title: string;
  detail: string;
}

interface Receipt {
  nf: string;
  fornecedor: string;
  data: string;
  valor: number;
}

@Component({
  selector: 'app-purchases',
  imports: [AppIconComponent],
  templateUrl: './purchases.component.html',
  styleUrl: './purchases.component.scss',
})
export class PurchasesComponent {
  protected readonly activeTab = signal('Pedidos de compra');

  protected readonly metrics = computed<PurchaseMetric[]>(() => {
    const comprasMes = this.orders.reduce((total, order) => total + order.total, 0);
    const abertos = this.orders.filter((order) => !['Recebido', 'Cancelado', 'Concluido'].includes(order.status));
    const atrasados = this.orders.filter((order) => order.status === 'Em atraso');
    const recebimentos = this.receipts.reduce((total, receipt) => total + receipt.valor, 0);

    return [
      { icon: 'shopping-bag', label: 'Compras no mes', value: this.formatCurrency(comprasMes), detail: 'base atual', tone: 'success' },
      { icon: 'file-text', label: 'Pedidos em aberto', value: String(abertos.length), detail: this.formatCurrency(abertos.reduce((total, order) => total + order.total, 0)), tone: 'neutral' },
      { icon: 'info', label: 'Pedidos em atraso', value: String(atrasados.length), detail: this.formatCurrency(atrasados.reduce((total, order) => total + order.total, 0)), tone: 'danger' },
      { icon: 'truck', label: 'Recebimentos (mes)', value: String(this.receipts.length), detail: this.formatCurrency(recebimentos), tone: 'success' },
    ];
  });

  protected readonly tabs = ['Pedidos de compra', 'Recebimentos', 'Sugestao de compra', 'Cotacoes'];

  protected readonly orders: PurchaseOrder[] = [];

  protected readonly quickActions: QuickAction[] = [
    { icon: 'plus', title: 'Nova compra', detail: 'Criar um novo pedido' },
    { icon: 'file-up', title: 'Importar NF-e', detail: 'Leia o XML do fornecedor' },
    { icon: 'shopping-cart', title: 'Sugestao de compra', detail: 'Gerar automaticamente' },
    { icon: 'file-text', title: 'Cotacoes', detail: 'Comparar fornecedores' },
  ];

  protected readonly receipts: Receipt[] = [];

  protected readonly totalPedidos = computed(() => this.orders.length);

  protected statusClass(status: PurchaseOrder['status']): string {
    const map: Record<PurchaseOrder['status'], string> = {
      Enviado: 'sent',
      Parcial: 'partial',
      Recebido: 'received',
      'Em atraso': 'late',
      Cancelado: 'canceled',
      Concluido: 'done',
    };

    return map[status];
  }

  protected countByStatus(status: PurchaseOrder['status']): number {
    return this.orders.filter((order) => order.status === status).length;
  }

  protected formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }
}
