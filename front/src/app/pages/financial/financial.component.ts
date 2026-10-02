import { Component } from '@angular/core';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

interface FinanceMetric {
  icon: string;
  label: string;
  value: string;
  detail: string;
  delta: string;
  tone: 'success' | 'danger' | 'info' | 'purple';
}

interface FlowMonth {
  label: string;
  entradas: number;
  saidas: number;
  resultado: number;
}

interface DueTitle {
  vencimento: string;
  nome: string;
  origem: string;
  valor: string;
  status: string;
}

interface Movement {
  data: string;
  descricao: string;
  origem: string;
  forma: string;
  valor: string;
  saldo: string;
  type: 'in' | 'out';
}

interface FinanceAccount {
  icon: string;
  bank: string;
  type: string;
  balance: string;
  tone: string;
}

@Component({
  selector: 'app-financial',
  imports: [AppIconComponent],
  templateUrl: './financial.component.html',
  styleUrl: './financial.component.scss',
})
export class FinancialComponent {
  protected readonly metrics: FinanceMetric[] = [
    { icon: 'hand-coins', label: 'A receber', value: 'R$ 48.920,00', detail: '32 titulos em aberto', delta: '+12%', tone: 'success' },
    { icon: 'receipt', label: 'A pagar', value: 'R$ 31.450,00', detail: '18 titulos em aberto', delta: '+8%', tone: 'danger' },
    { icon: 'receipt-text', label: 'Recebido no mes', value: 'R$ 126.840,00', detail: '214 recebimentos', delta: '+8,4%', tone: 'info' },
    { icon: 'circle-dollar-sign', label: 'Saldo projetado (30 dias)', value: 'R$ 17.470,00', detail: 'Considerando entradas e saidas', delta: 'ver', tone: 'purple' },
  ];

  protected readonly flow: FlowMonth[] = [
    { label: 'Mai/26', entradas: 138000, saidas: 103000, resultado: 35000 },
    { label: 'Jun/26', entradas: 122000, saidas: 96000, resultado: 26000 },
    { label: 'Jul/26', entradas: 149000, saidas: 116000, resultado: 33000 },
    { label: 'Ago/26', entradas: 133000, saidas: 109000, resultado: 24000 },
    { label: 'Set/26', entradas: 121000, saidas: 94000, resultado: 27000 },
    { label: 'Out/26', entradas: 126840, saidas: 91420, resultado: 35420 },
  ];

  protected readonly receivables: DueTitle[] = [
    { vencimento: '05/10/2026', nome: 'Auto Center Silva', origem: 'Venda #1854 (1/3)', valor: 'R$ 500,00', status: 'Em aberto' },
    { vencimento: '05/10/2026', nome: 'Oficina Avenida', origem: 'Venda #1852', valor: 'R$ 1.280,00', status: 'Em aberto' },
    { vencimento: '07/10/2026', nome: 'Mecanica Sul', origem: 'Venda #1828 (2/3)', valor: 'R$ 720,00', status: 'Vencido' },
    { vencimento: '08/10/2026', nome: 'CarTech Servicos', origem: 'Venda #1870 (1/2)', valor: 'R$ 950,00', status: 'Em aberto' },
    { vencimento: '10/10/2026', nome: 'Posto Rodovia', origem: 'Venda #1865', valor: 'R$ 1.450,00', status: 'Em aberto' },
  ];

  protected readonly payables: DueTitle[] = [
    { vencimento: '05/10/2026', nome: 'Distribuidora Bosch', origem: 'NF-e 45879 (1/3)', valor: 'R$ 4.280,00', status: 'Em aberto' },
    { vencimento: '07/10/2026', nome: 'Cofap Autopecas', origem: 'NF-e 45210', valor: 'R$ 2.890,00', status: 'Em aberto' },
    { vencimento: '10/10/2026', nome: 'Mann Filter', origem: 'NF-e 46123 (2/2)', valor: 'R$ 3.450,00', status: 'Vencido' },
    { vencimento: '12/10/2026', nome: 'NGK do Brasil', origem: 'NF-e 46190', valor: 'R$ 1.980,00', status: 'Em aberto' },
    { vencimento: '15/10/2026', nome: 'Mobil Lubrificantes', origem: 'NF-e 46233 (1/2)', valor: 'R$ 2.650,00', status: 'Em aberto' },
  ];

  protected readonly movements: Movement[] = [
    { data: '02/10/2026 10:42', descricao: 'Recebimento de venda #1858', origem: 'Cliente: Auto Center Silva', forma: 'PIX', valor: 'R$ 489,90', saldo: 'R$ 18.520,00', type: 'in' },
    { data: '02/10/2026 09:28', descricao: 'Pagamento fornecedor', origem: 'Distribuidora Bosch', forma: 'Transferencia', valor: '- R$ 2.850,00', saldo: 'R$ 18.030,10', type: 'out' },
    { data: '01/10/2026 16:15', descricao: 'Recebimento de venda #1857', origem: 'Cliente: Oficina Avenida', forma: 'Cartao credito', valor: 'R$ 1.249,00', saldo: 'R$ 20.880,10', type: 'in' },
    { data: '01/10/2026 14:32', descricao: 'Sangria de caixa', origem: 'Caixa Balcao 01', forma: 'Dinheiro', valor: '- R$ 500,00', saldo: 'R$ 19.631,10', type: 'out' },
    { data: '01/10/2026 11:20', descricao: 'Pagamento fornecedor', origem: 'Cofap Autopecas', forma: 'PIX', valor: '- R$ 1.890,00', saldo: 'R$ 20.131,10', type: 'out' },
  ];

  protected readonly accounts: FinanceAccount[] = [
    { icon: 'wallet-cards', bank: 'Caixa Balcao 01', type: 'Dinheiro', balance: 'R$ 5.420,00', tone: 'cash' },
    { icon: 'circle-dollar-sign', bank: 'Banco Sicredi', type: 'Conta corrente', balance: 'R$ 18.030,10', tone: 'bank' },
    { icon: 'wallet-cards', bank: 'Nubank PJ', type: 'Conta corrente', balance: 'R$ 12.845,90', tone: 'purple' },
    { icon: 'circle-dollar-sign', bank: 'Banco do Brasil', type: 'Conta corrente', balance: 'R$ 4.250,00', tone: 'yellow' },
  ];

  protected readonly maxFlowValue = 160000;

  protected barHeight(value: number): string {
    return `${Math.max((value / this.maxFlowValue) * 100, 6)}%`;
  }

  protected resultPosition(value: number): string {
    return `${Math.min(Math.max((value / 65000) * 100, 16), 86)}%`;
  }

  protected statusClass(status: string): string {
    return status === 'Vencido' ? 'late' : 'open';
  }
}
