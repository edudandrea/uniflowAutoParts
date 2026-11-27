import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { DocumentoReferenciaCaixa, ReferenciaCaixa } from '../../core/app-models';
import { downloadCsv } from '../../core/csv-download';
import { SessionService } from '../../core/session.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

type LoadState = { status: 'loading' | 'error' } | { status: 'ready'; data: ReferenciaCaixa };
type Origem = DocumentoReferenciaCaixa['origem'];

const ZONE = 'America/Sao_Paulo';
const DAY = 86_400_000;
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const axisMoney = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 });
const dayFormat = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeZone: 'UTC' });
const dayShort = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', timeZone: 'UTC' });
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: ZONE });
const asIsoDay = (timestamp: number) => new Date(timestamp).toISOString().slice(0, 10);
const normalize = (text: string) => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('pt-BR');

@Component({
  selector: 'app-cash',
  imports: [AppIconComponent],
  templateUrl: './cash.component.html',
  styleUrl: './cash.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CashComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  private readonly refresh = signal(0);
  private readonly today = new Intl.DateTimeFormat('sv-SE', { timeZone: ZONE }).format(new Date());
  protected readonly period = signal({ de: asIsoDay(Date.parse(this.today) - 29 * DAY), ate: this.today });
  protected readonly periodError = signal('');
  protected readonly query = signal('');
  protected readonly origin = signal<Origem | ''>('');
  protected readonly newestFirst = signal(true);
  protected readonly page = signal(1);
  protected readonly pageSize = 10;
  protected readonly metrics = [
    { label: 'Saldo atual', icon: 'wallet-cards', tone: 'green', detail: 'Sem saldo de caixa registrado' },
    { label: 'Entradas hoje', icon: 'arrow-down', tone: 'green', detail: 'Sem registros de recebimentos' },
    { label: 'Saídas hoje', icon: 'arrow-up', tone: 'rose', detail: 'Sem registros de pagamentos' },
    { label: 'Saldo projetado', icon: 'circle-dollar-sign', tone: 'purple', detail: 'Sem saldo de partida' },
  ];
  protected readonly originLabels = { saida: 'Nota de saída', compra: 'Compra conferida' };

  private readonly request = computed(() => ({
    ...this.period(), empresaId: this.session.empresaId, refresh: this.refresh(),
  }));
  protected readonly state = toSignal(toObservable(this.request).pipe(
    switchMap(({ de, ate, empresaId }) => this.api.carregarReferenciaCaixa(empresaId, de, ate).pipe(
      map((data): LoadState => ({ status: 'ready', data })),
      startWith({ status: 'loading' } as const),
      catchError(() => of({ status: 'error' } as const)),
    )),
  ), { initialValue: { status: 'loading' } as const });

  protected readonly data = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly periodLabel = computed(() => {
    const { de, ate } = this.period();
    return `${dayFormat.format(new Date(de))} – ${dayFormat.format(new Date(ate))}`;
  });

  protected readonly summary = computed(() => {
    const data = this.data();
    if (!data) return null;
    let saida = 0;
    let compra = 0;
    for (const note of data.documentos) {
      if (note.origem === 'saida') saida += note.valor;
      else compra += note.valor;
    }
    return { saida, compra, count: data.documentos.length };
  });

  protected readonly chart = computed(() => {
    const data = this.data();
    if (!data?.documentos.length) return null;
    const { de, ate } = this.period();
    const start = Date.parse(de);
    const end = Date.parse(ate);
    const days = (end - start) / DAY + 1;
    const span = Math.ceil(days / 6);
    const buckets = Array.from({ length: Math.ceil(days / span) }, (_, index) => {
      const first = start + index * span * DAY;
      const last = Math.min(first + (span - 1) * DAY, end);
      const label = first === last ? dayShort.format(first) : `${dayShort.format(first)}–${dayShort.format(last)}`;
      return { key: asIsoDay(first), label, saida: 0, compra: 0 };
    });
    for (const note of data.documentos) {
      const index = Math.floor((Date.parse(note.dia) - start) / DAY / span);
      const bucket = buckets[index];
      if (bucket) bucket[note.origem] += note.valor;
    }

    const peak = Math.max(1, ...buckets.flatMap(bucket => [bucket.saida, bucket.compra]));
    const magnitude = 10 ** Math.floor(Math.log10(peak / 4));
    const step = Math.max(1, Math.ceil(peak / 4 / magnitude) * magnitude);
    const groups = buckets.map(bucket => ({
      ...bucket, saidaHeight: bucket.saida / (4 * step) * 100, compraHeight: bucket.compra / (4 * step) * 100,
      description: `${bucket.label}. Notas de saída ${money.format(bucket.saida)}. Compras ${money.format(bucket.compra)}.`,
    }));
    return {
      groups, description: groups.map(group => group.description).join(' '),
      ticks: [4, 3, 2, 1, 0].map(tick => axisMoney.format(tick * step)),
    };
  });

  protected readonly filtered = computed(() => {
    const origin = this.origin();
    const query = normalize(this.query().trim());
    const rows = (this.data()?.documentos ?? []).filter(note => (!origin || origin === note.origem)
      && (!query || normalize(`${this.documentLabel(note)} ${note.nome ?? ''}`).includes(query)));
    return this.newestFirst() ? rows : rows.reverse();
  });
  protected readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  protected readonly currentPage = computed(() => Math.min(this.page(), this.pageCount()));
  protected readonly rows = computed(() => this.filtered().slice((this.currentPage() - 1) * this.pageSize, this.currentPage() * this.pageSize));

  protected applyPeriod(event: Event, de: string, ate: string, disclosure: HTMLDetailsElement): void {
    event.preventDefault();
    if (ate < de || Date.parse(ate) - Date.parse(de) >= 366 * DAY) {
      this.periodError.set('Selecione um período válido de até 366 dias.');
      return;
    }
    this.periodError.set('');
    this.page.set(1);
    this.period.set({ de, ate });
    disclosure.open = false;
    disclosure.querySelector('summary')?.focus();
  }

  protected filterOrigin(value: string): void {
    if (value === 'saida' || value === 'compra' || value === '') {
      this.origin.set(value);
      this.page.set(1);
    }
  }

  protected retry(): void { this.refresh.update(value => value + 1); }
  protected formatCurrency(value: number): string { return money.format(value); }
  protected formatDateTime(value: string): string { return dateTime.format(new Date(value)); }
  protected documentLabel(note: DocumentoReferenciaCaixa): string {
    return `${note.tipo === 'NFCe' ? 'NFC-e' : 'NF-e'} ${note.numero}`;
  }

  protected exportCsv(): void {
    const rows = this.filtered();
    if (!rows.length) return;
    downloadCsv([
      ['Emissão', 'Documento', 'Série', 'Cliente ou fornecedor', 'Origem', 'Dinheiro declarado (BRL, antes do troco)'],
      ...rows.map(note => [this.formatDateTime(note.emissao), this.documentLabel(note), note.serie,
        note.nome ?? '', this.originLabels[note.origem], note.valor.toFixed(2).replace('.', ',')]),
    ], 'caixa-referencia-fiscal.csv');
  }
}
