import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { DashboardResumo } from '../../core/app-models';
import { SessionService } from '../../core/session.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

type PeriodKey = 'month' | 'week' | 'lastMonth';
type ChartMode = 'valor' | 'quantidade';
type DashboardState = { status: 'loading' | 'error' } | { status: 'ready'; data: DashboardResumo };

const DAY = 86_400_000;
const ZONE = 'America/Sao_Paulo';
const dayKey = new Intl.DateTimeFormat('sv-SE', { timeZone: ZONE });
const shortDate = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', timeZone: 'UTC' });
const dateTime = new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', timeZone: ZONE,
});
const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 });
const percent = new Intl.NumberFormat('pt-BR', { style: 'percent', maximumFractionDigits: 1 });
const chartCurrency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 });
const iso = (date: Date) => date.toISOString().slice(0, 10);
const shortDay = (date: Date) => shortDate.format(date).replace(' de ', ' ').replace('.', '');

@Component({
  selector: 'app-dashboard',
  imports: [AppIconComponent, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  protected readonly period = signal<PeriodKey>('month');
  protected readonly mode = signal<ChartMode>('valor');
  private readonly reload = signal(0);
  protected readonly periods = [
    { value: 'month', label: 'Este mês' },
    { value: 'week', label: 'Últimos 7 dias' },
    { value: 'lastMonth', label: 'Mês anterior' },
  ];

  protected readonly range = computed(() => {
    const today = new Date(dayKey.format(new Date()) + 'T00:00:00Z');
    const year = today.getUTCFullYear();
    const month = today.getUTCMonth();
    switch (this.period()) {
      case 'week': return { start: new Date(+today - 6 * DAY), end: today };
      case 'lastMonth': return {
        start: new Date(Date.UTC(year, month - 1, 1)), end: new Date(Date.UTC(year, month, 0)),
      };
      default: return { start: new Date(Date.UTC(year, month, 1)), end: today };
    }
  });
  protected readonly periodLabel = computed(() => {
    const { start, end } = this.range();
    const label = this.periods.find(period => period.value === this.period())?.label;
    return `${label} (${shortDay(start)} – ${shortDay(end)})`;
  });
  private readonly request = computed(() => ({ ...this.range(), refresh: this.reload() }));
  protected readonly state = toSignal(toObservable(this.request).pipe(
    switchMap(({ start, end }) => this.api.carregarDashboard(this.session.empresaId, iso(start), iso(end)).pipe(
      map((data): DashboardState => ({ status: 'ready', data })),
      startWith({ status: 'loading' } as const),
      catchError(() => of({ status: 'error' } as const)),
    )),
  ), { initialValue: { status: 'loading' } as const });

  protected readonly data = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly greeting = computed(() => {
    const hour = Number(new Intl.DateTimeFormat('pt-BR', {
      hour: '2-digit', hourCycle: 'h23', timeZone: ZONE,
    }).format(new Date()));
    const greeting = hour < 12 ? 'Bom dia' : hour < 18 ? 'Boa tarde' : 'Boa noite';
    const name = this.session.session()?.usuario?.nome.trim().split(/\s+/)[0];
    return name ? `${greeting}, ${name}!` : `${greeting}!`;
  });

  protected readonly metrics = computed(() => {
    const data = this.data();
    const previous = data?.comparacao;
    const ticket = data?.total ? data.faturamento / data.total : null;
    const previousTicket = previous?.quantidade ? previous.valor / previous.quantidade : null;
    return [
      { label: 'Faturamento', icon: 'shopping-cart', tone: 'green',
        value: data ? currency.format(data.faturamento) : '—', detail: 'Em notas autorizadas',
        delta: this.change(data?.faturamento, previous?.valor) },
      { label: 'Vendas', icon: 'boxes', tone: 'mint',
        value: data ? quantity.format(data.total) : '—', detail: 'Notas autorizadas',
        delta: this.change(data?.total, previous?.quantidade) },
      { label: 'Ticket médio', icon: 'tags', tone: 'rose',
        value: ticket === null ? '—' : currency.format(ticket), detail: 'Por nota autorizada',
        delta: this.change(ticket, previousTicket) },
      { label: 'Margem de venda', icon: 'bar-chart-3', tone: 'green',
        value: data?.margem != null ? percent.format(data.margem / 100) : '—',
        detail: data?.margem == null ? 'Sem dados de custo suficientes' : 'Sobre o faturamento autorizado',
        delta: null },
    ];
  });

  protected readonly alerts = computed(() => {
    const data = this.data();
    if (!data) return [];
    return [
      { count: data.estoqueBaixo, title: data.estoqueBaixo === 1 ? 'produto com estoque baixo' : 'produtos com estoque baixo',
        detail: 'Confira os itens abaixo do mínimo.', tone: 'danger', icon: 'package', path: '/app/estoque' },
      { count: data.pendentes, title: data.pendentes === 1 ? 'nota em processamento' : 'notas em processamento',
        detail: 'Acompanhe o retorno da SEFAZ.', tone: 'warning', icon: 'receipt-text', path: '/app/notas' },
      { count: data.rejeitadas, title: data.rejeitadas === 1 ? 'nota rejeitada no período' : 'notas rejeitadas no período',
        detail: 'Consulte o motivo da rejeição.', tone: 'info', icon: 'calendar-x', path: '/app/notas' },
    ].filter(alert => alert.count > 0);
  });

  protected readonly activity = computed(() => {
    const data = this.data();
    if (!data) return [];
    return [
      ...data.notas.map(nota => ({
        title: `${nota.tipo === 'NFCe' ? 'NFC-e' : 'NF-e'} #${nota.numero} · ${nota.situacao.toLowerCase()}`,
        detail: [nota.cliente, currency.format(nota.valor)].filter(Boolean).join(' · '),
        time: nota.emissao, icon: 'receipt-text', path: '/app/notas',
      })),
      ...data.entradas.map(entrada => ({
        title: `Entrada de compra #${entrada.numeroNfe}`,
        detail: entrada.status === 'Conferencia' ? 'Em conferência' : entrada.status,
        time: entrada.importadoEm, icon: 'package', path: '/app/compras',
      })),
    ].sort((a, b) => Date.parse(b.time) - Date.parse(a.time)).slice(0, 5)
      .map(item => ({ ...item, label: dateTime.format(new Date(item.time)) }));
  });

  protected readonly products = computed(() => (this.data()?.produtos ?? []).map(product => ({
    ...product, units: quantity.format(product.quantidade),
  })));

  protected readonly chart = computed(() => {
    const data = this.data();
    const mode = this.mode();
    const { start, end } = this.range();
    const count = (+end - +start) / DAY + 1;
    const byDate = new Map(data?.serie.map(point => [point.dia.slice(0, 10), point[mode]]));
    const amounts = Array.from({ length: count }, (_, index) => byDate.get(iso(new Date(+start + index * DAY))) ?? 0);
    const peak = Math.max(1, ...amounts);
    const power = 10 ** Math.floor(Math.log10(peak / 4));
    const step = Math.max(1, Math.ceil(peak / 4 / power) * power);
    const format = mode === 'valor' ? chartCurrency : quantity;
    const points = amounts.map((value, index) => ({
      x: count === 1 ? 310 : 5 + index * 610 / (count - 1),
      y: 146 - value / (step * 4) * 140,
      title: `${shortDate.format(new Date(+start + index * DAY))} · ${format.format(value)}`,
    }));
    const first = points[0];
    const last = points[points.length - 1];
    const line = points.slice(1).reduce((path, point, index) => {
      const previous = points[index];
      const middle = (previous.x + point.x) / 2;
      return `${path} C ${middle} ${previous.y}, ${middle} ${point.y}, ${point.x} ${point.y}`;
    }, `M ${first.x} ${first.y}`);
    const labelCount = Math.min(count, 6);
    const labels = Array.from({ length: labelCount }, (_, index) => {
      const offset = labelCount === 1 ? 0 : Math.round(index * (count - 1) / (labelCount - 1));
      return shortDay(new Date(+start + offset * DAY));
    });
    return {
      description: points.map(point => point.title).join('. '),
      points, line, area: `${line} L ${last.x} 146 H ${first.x} Z`, labels,
      ticks: [4, 3, 2, 1, 0].map(tick => format.format(tick * step)),
    };
  });

  protected setPeriod(value: string): void {
    if (value === 'month' || value === 'week' || value === 'lastMonth') this.period.set(value);
  }

  protected retry(): void {
    this.reload.update(value => value + 1);
  }

  private change(value?: number | null, previous?: number | null) {
    if (value == null || !previous || value === previous) return null;
    const delta = (value - previous) / previous;
    return { down: delta < 0, label: percent.format(Math.abs(delta)) };
  }
}
