import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { DocumentoFinanceiro } from '../../core/app-models';
import { downloadCsv } from '../../core/csv-download';
import { SessionService } from '../../core/session.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

type SortField = 'emissao' | 'valor';
type State = { status: 'loading' | 'error' } | { status: 'ready'; documentos: DocumentoFinanceiro[] };

const ZONE = 'America/Sao_Paulo';
const DAY = 86_400_000;
const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const date = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeZone: 'UTC' });
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: ZONE });
const normalize = (value: string) => value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('pt-BR');

@Component({
  selector: 'app-financial-movements',
  imports: [AppIconComponent],
  templateUrl: './financial-movements.component.html',
  styleUrl: './financial-movements.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FinancialMovementsComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  private readonly revision = signal(0);
  private readonly today = new Intl.DateTimeFormat('sv-SE', { timeZone: ZONE }).format(new Date());
  protected readonly period = signal({ de: this.today.slice(0, 7) + '-01', ate: this.today });
  protected readonly periodError = signal('');
  protected readonly origin = signal('');
  protected readonly model = signal('');
  protected readonly text = signal('');
  protected readonly sorting = signal<{ field: SortField; descending: boolean }>({ field: 'emissao', descending: true });
  protected readonly page = signal(1);
  protected readonly selected = signal<DocumentoFinanceiro | null>(null);
  private readonly pageSize = 12;
  protected readonly descriptions = {
    saida: { origin: 'Nota de saída', status: 'Autorizada', icon: 'receipt-text' },
    compra: { origin: 'Compra', status: 'Conferida', icon: 'shopping-cart' },
  };

  private readonly request = computed(() => ({
    ...this.period(), empresaId: this.session.empresaId, revision: this.revision(),
  }));
  protected readonly state = toSignal(toObservable(this.request).pipe(
    switchMap(({ de, ate, empresaId }) => this.api.carregarReferenciaMovimentacoes(empresaId, de, ate).pipe(
      map(({ documentos }): State => ({ status: 'ready', documentos })),
      startWith({ status: 'loading' } as const),
      catchError(() => of({ status: 'error' } as const)),
    )),
  ), { initialValue: { status: 'loading' } as const });

  protected readonly periodLabel = computed(() => {
    const { de, ate } = this.period();
    return `${date.format(new Date(de))} – ${date.format(new Date(ate))}`;
  });

  protected readonly metrics = computed(() => {
    const state = this.state();
    const total = (origin: DocumentoFinanceiro['origem']) => {
      if (state.status !== 'ready') return { valor: '—', detail: 'No período selecionado' };
      const docs = state.documentos.filter(doc => doc.origem === origin);
      return { valor: currency.format(docs.reduce((sum, doc) => sum + doc.valor, 0)), detail: this.documentCount(docs.length) };
    };
    return [
      { label: 'Notas de saída', icon: 'receipt-text', tone: 'green', ...total('saida') },
      { label: 'Compras conferidas', icon: 'shopping-cart', tone: 'rose', ...total('compra') },
      { label: 'Transferências', icon: 'refresh-cw', tone: 'blue', valor: 'Indisponível', detail: 'Sem registros entre contas' },
      { label: 'Resultado financeiro', icon: 'bar-chart-3', tone: 'green', valor: 'Indisponível', detail: 'Sem entradas e saídas liquidadas' },
    ];
  });

  protected readonly models = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? [...new Set(state.documentos.map(doc => doc.tipo))].sort() : [];
  });
  protected readonly filtered = computed(() => {
    const state = this.state();
    if (state.status !== 'ready') return [];
    const origin = this.origin();
    const model = this.model();
    const query = normalize(this.text().trim());
    const { field, descending } = this.sorting();
    return state.documentos.filter(doc => (!origin || doc.origem === origin)
      && (!model || doc.tipo === model)
      && (!query || normalize(`${this.documentLabel(doc)} ${doc.nome ?? ''}`).includes(query)))
      .sort((a, b) => {
        const difference = field === 'valor' ? a.valor - b.valor : Date.parse(a.emissao) - Date.parse(b.emissao);
        return descending ? -difference : difference;
      });
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
    this.model.set('');
    this.period.set({ de, ate });
    this.page.set(1);
    disclosure.open = false;
    disclosure.querySelector('summary')?.focus();
  }

  protected sort(field: SortField): void {
    const current = this.sorting();
    this.sorting.set({ field, descending: current.field === field ? !current.descending : true });
    this.page.set(1);
  }

  protected direction(field: SortField): 'ascending' | 'descending' | null {
    const { field: sortedField, descending } = this.sorting();
    return sortedField === field ? (descending ? 'descending' : 'ascending') : null;
  }

  protected retry(): void { this.revision.update(value => value + 1); }
  protected formatCurrency(value: number): string { return currency.format(value); }
  protected formatDateTime(value: string): string { return dateTime.format(new Date(value)); }
  protected documentCount(count: number): string { return count === 1 ? '1 documento' : `${count} documentos`; }
  protected modelLabel(value: string): string {
    if (value === 'NFe') return 'NF-e';
    if (value === 'NFCe') return 'NFC-e';
    return value;
  }
  protected documentLabel(doc: DocumentoFinanceiro): string {
    return `${this.modelLabel(doc.tipo)} ${doc.numero}`;
  }
  protected openDetails(dialog: HTMLDialogElement, doc: DocumentoFinanceiro): void {
    this.selected.set(doc);
    dialog.showModal();
  }
  protected exportCsv(): void {
    const docs = this.filtered();
    if (!docs.length) return;
    downloadCsv([
      ['Emissão', 'Origem', 'Documento', 'Série', 'Cliente ou fornecedor', 'Valor fiscal (BRL)', 'Situação fiscal'],
      ...docs.map(doc => [this.formatDateTime(doc.emissao), this.descriptions[doc.origem].origin,
        this.documentLabel(doc), doc.serie, doc.nome ?? '', doc.valor.toFixed(2).replace('.', ','),
        this.descriptions[doc.origem].status]),
    ], 'movimentacoes-referencia-fiscal.csv');
  }
}
