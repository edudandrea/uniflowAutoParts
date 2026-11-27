import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { FinanceiroResumo } from '../../core/app-models';
import { SessionService } from '../../core/session.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

type FinanceiroState = { status: 'loading' | 'error' } | { status: 'ready'; data: FinanceiroResumo };

const ZONE = 'America/Sao_Paulo';
const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const axisCurrency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 });
const percent = new Intl.NumberFormat('pt-BR', { style: 'percent', maximumFractionDigits: 1, signDisplay: 'exceptZero' });
const monthShort = new Intl.DateTimeFormat('pt-BR', { month: 'short', year: '2-digit', timeZone: 'UTC' });
const monthLong = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric', timeZone: 'UTC' });
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: ZONE });

@Component({
  selector: 'app-financial',
  imports: [AppIconComponent, RouterLink],
  templateUrl: './financial.component.html',
  styleUrl: './financial.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FinancialComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  private readonly refresh = signal(0);
  protected readonly currentMonth = new Intl.DateTimeFormat('sv-SE', { timeZone: ZONE }).format(new Date()).slice(0, 7);
  protected readonly month = signal(this.currentMonth);
  protected readonly monthLabel = computed(() => monthLong.format(new Date(this.month() + '-01')));
  private readonly request = computed(() => ({
    month: this.month(),
    empresaId: this.session.empresaId,
    refresh: this.refresh(),
  }));
  protected readonly state = toSignal(toObservable(this.request).pipe(
    switchMap(({ empresaId, month }) => this.api.carregarFinanceiro(empresaId, month + '-01').pipe(
      map((data): FinanceiroState => ({ status: 'ready', data })),
      startWith({ status: 'loading' } as const),
      catchError(() => of({ status: 'error' } as const)),
    )),
  ), { initialValue: { status: 'loading' } as const });

  protected readonly data = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly summary = computed(() => this.data()?.serie.at(-1));
  private readonly previous = computed(() => this.data()?.serie.at(-2));

  protected readonly duePanels = [
    { label: 'Contas a receber', icon: 'calendar-check', tone: 'green', detail: 'Parcelas informadas nas notas fiscais de saída.', route: '/app/financeiro/contas-a-receber' },
    { label: 'Contas a pagar', icon: 'calendar-x', tone: 'rose', detail: 'Parcelas informadas nas notas fiscais de compra.', route: '/app/financeiro/contas-a-pagar' },
  ];
  protected readonly metrics = computed(() => {
    const month = this.summary();
    const previous = this.previous();
    const difference = month && month.saidas - month.compras;
    return [
      {
        label: 'Notas de saída', icon: 'receipt-text', tone: 'green',
        value: month && currency.format(month.saidas), detail: 'Notas autorizadas em produção',
        delta: this.change(month?.saidas, previous?.saidas),
      },
      {
        label: 'Compras conferidas', icon: 'shopping-cart', tone: 'rose',
        value: month && currency.format(month.compras), detail: 'NF-e de compra conferidas',
        delta: this.change(month?.compras, previous?.compras),
      },
      {
        label: 'Diferença fiscal', icon: 'chart-no-axes-combined', tone: 'blue',
        value: difference === undefined ? undefined : currency.format(difference),
        detail: 'Total de saídas menos compras', delta: null,
      },
      {
        label: 'Saldo projetado', icon: 'circle-dollar-sign', tone: 'purple',
        value: 'Indisponível', detail: 'Sem dados de contas e vencimentos', delta: null, unavailable: true,
      },
    ];
  });

  protected readonly chart = computed(() => {
    const months = this.data()?.serie;
    if (!months?.some(month => month.totalSaidas || month.totalCompras)) return null;
    const values = months.flatMap(month => [month.saidas, month.compras, month.saidas - month.compras]);
    const peak = Math.max(1, ...values);
    const low = Math.min(0, ...values);
    const rough = (peak - low) / 4;
    const power = 10 ** Math.floor(Math.log10(rough));
    const step = Math.max(1, Math.ceil(rough / power) * power);
    const top = Math.ceil(peak / step);
    const bottom = Math.floor(low / step);
    const position = (value: number) => (value / step - bottom) / (top - bottom) * 100;
    const baseline = position(0);
    const bar = (value: number) => ({
      bottom: Math.min(position(value), baseline),
      height: Math.abs(position(value) - baseline),
    });
    const points = months.map((month, index) => ({
      month: month.mes,
      label: monthShort.format(new Date(month.mes)).replace('.', '').replace(' de ', '/'),
      x: (index + .5) / months.length * 100,
      y: 100 - position(month.saidas - month.compras),
      notes: bar(month.saidas),
      purchases: bar(month.compras),
      description: `${monthLong.format(new Date(month.mes))}. Notas de saída ${currency.format(month.saidas)}. Compras conferidas ${currency.format(month.compras)}. Diferença ${currency.format(month.saidas - month.compras)}.`,
    }));
    return {
      baseline, points,
      line: points.map(point => `${point.x},${point.y}`).join(' '),
      description: points.map(point => point.description).join(' '),
      ticks: Array.from({ length: top - bottom + 1 }, (_, i) => ({
        label: axisCurrency.format((top - i) * step),
        position: position((top - i) * step),
      })),
    };
  });

  protected readonly formatCurrency = (value: number) => currency.format(value);
  protected readonly formatDateTime = (value: string) => dateTime.format(new Date(value));

  protected setMonth(input: HTMLInputElement): void {
    if (input.validity.valid && /^\d{4}-(0[1-9]|1[0-2])$/.test(input.value)) {
      this.month.set(input.value);
    } else {
      input.value = this.month();
    }
  }

  protected retry(): void {
    this.refresh.update(value => value + 1);
  }

  private change(current?: number, previous?: number): string | null {
    if (current === undefined || !previous || current === previous) return null;
    return percent.format((current - previous) / previous);
  }
}
