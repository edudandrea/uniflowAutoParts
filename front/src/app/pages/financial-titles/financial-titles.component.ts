import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ParcelaFiscal } from '../../core/app-models';
import { downloadCsv } from '../../core/csv-download';
import { SessionService } from '../../core/session.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

interface Titulo extends ParcelaFiscal {
  documentoId: number;
  pessoaId: number | null;
  pessoa: string | null;
}

interface Relatorio {
  hoje: string;
  titulos: Titulo[];
  pessoas: { id: number; nome: string }[];
  naturezas: string[];
}

type LoadState = { status: 'loading' | 'error' } | { status: 'ready'; data: Relatorio };
type SortField = 'vencimento' | 'pessoa' | 'valor';

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const dateFormat = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeZone: 'UTC' });
const collator = new Intl.Collator('pt-BR');
const toIso = (date: Date) => date.toISOString().slice(0, 10);

@Component({
  selector: 'app-financial-titles',
  imports: [RouterLink, AppIconComponent],
  templateUrl: './financial-titles.component.html',
  styleUrl: './financial-titles.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FinancialTitlesComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  protected readonly receivable = inject(ActivatedRoute).snapshot.data['receber'] === true;
  protected readonly personLabel = this.receivable ? 'Cliente' : 'Fornecedor';
  private readonly refresh = signal(0);
  private readonly today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Sao_Paulo' }).format(new Date());
  private readonly lastDay = new Date(Date.UTC(+this.today.slice(0, 4), +this.today.slice(5, 7), 0));
  protected readonly period = signal({ de: this.today.slice(0, 7) + '-01', ate: toIso(this.lastDay) });
  protected readonly periodError = signal('');
  protected readonly person = signal('');
  protected readonly nature = signal('');
  protected readonly due = signal('');
  protected readonly sorting = signal<{ field: SortField; descending: boolean }>({ field: 'vencimento', descending: false });
  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly selected = signal<Titulo | null>(null);

  private readonly request = computed(() => ({
    ...this.period(), empresaId: this.session.empresaId, refresh: this.refresh(),
  }));
  protected readonly state = toSignal(toObservable(this.request).pipe(
    switchMap(({ empresaId, de, ate }) => this.loadTitles(empresaId, de, ate).pipe(
      map((data): LoadState => ({ status: 'ready', data })),
      startWith({ status: 'loading' } as const),
      catchError(() => of({ status: 'error' } as const)),
    )),
  ), { initialValue: { status: 'loading' } as const });

  private readonly data = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly people = computed(() => [...(this.data()?.pessoas ?? [])]
    .sort((a, b) => collator.compare(a.nome, b.nome)));
  protected readonly natures = computed(() => [...(this.data()?.naturezas ?? [])].sort(collator.compare));

  protected readonly metrics = computed(() => {
    const report = this.data();
    const titles = report?.titulos ?? [];
    const today = titles.filter(title => title.vencimento === report?.hoje);
    const earlier = titles.filter(title => report && title.vencimento < report.hoje);
    const summarize = (rows: Titulo[]) => ({
      value: report ? money.format(rows.reduce((total, row) => total + row.valor, 0)) : '—',
      detail: report ? this.countLabel(rows.length) : 'No período selecionado',
    });
    return [
      { label: 'Total informado', icon: 'coins', tone: 'green', ...summarize(titles) },
      { label: 'Vencimento hoje', icon: 'calendar', tone: 'amber', ...summarize(today) },
      { label: 'Vencimentos anteriores', icon: 'triangle-alert', tone: 'rose', ...summarize(earlier) },
      {
        label: this.receivable ? 'Recebido no mês' : 'Pago no mês', icon: 'wallet-cards', tone: 'blue',
        value: 'Indisponível', detail: this.receivable ? 'Sem registros de recebimento' : 'Sem registros de pagamento',
      },
    ];
  });

  protected readonly filtered = computed(() => {
    const report = this.data();
    if (!report) return [];
    const person = this.person();
    const nature = this.nature();
    const due = this.due();
    const { field, descending } = this.sorting();
    return report.titulos.filter(title => (!person || String(title.pessoaId) === person)
      && (!nature || title.natureza === nature) && (!due || this.dueStatus(title.vencimento) === due))
      .sort((a, b) => {
        const comparison = field === 'valor' ? a.valor - b.valor : collator.compare(a[field] ?? '', b[field] ?? '');
        return (descending ? -comparison : comparison) || a.documentoId - b.documentoId || a.indice - b.indice;
      });
  });
  protected readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize())));
  protected readonly currentPage = computed(() => Math.min(this.page(), this.pageCount()));
  protected readonly firstIndex = computed(() => (this.currentPage() - 1) * this.pageSize());
  protected readonly rows = computed(() => this.filtered().slice(this.firstIndex(), this.firstIndex() + this.pageSize()));
  protected readonly periodLabel = computed(() => {
    const { de, ate } = this.period();
    if (de && ate) return `${this.formatDate(de)} – ${this.formatDate(ate)}`;
    if (de) return `A partir de ${this.formatDate(de)}`;
    if (ate) return `Até ${this.formatDate(ate)}`;
    return 'Todos os vencimentos';
  });
  protected readonly dueLabels = { earlier: 'Data anterior', today: 'Hoje', upcoming: 'Data futura' };

  protected applyPeriod(event: Event, de: string, ate: string, disclosure: HTMLDetailsElement): void {
    event.preventDefault();
    if (de && ate && de > ate) {
      this.periodError.set('A data inicial deve ser anterior ou igual à data final.');
      return;
    }
    this.periodError.set('');
    this.period.set({ de, ate });
    this.page.set(1);
    disclosure.open = false;
    disclosure.querySelector('summary')?.focus();
  }

  protected clear(): void {
    this.periodError.set('');
    this.period.set({ de: '', ate: '' });
    this.person.set('');
    this.nature.set('');
    this.due.set('');
    this.page.set(1);
  }

  protected sort(field: SortField): void {
    const current = this.sorting();
    this.sorting.set({ field, descending: field === current.field && !current.descending });
    this.page.set(1);
  }

  protected sortDirection(field: SortField): 'ascending' | 'descending' | null {
    const sort = this.sorting();
    return sort.field === field ? (sort.descending ? 'descending' : 'ascending') : null;
  }

  protected dueStatus(date: string): keyof typeof this.dueLabels {
    const today = this.data()?.hoje ?? this.today;
    return date === today ? 'today' : date < today ? 'earlier' : 'upcoming';
  }

  protected showDetails(dialog: HTMLDialogElement, title: Titulo): void {
    this.selected.set(title);
    dialog.showModal();
  }

  protected retry(): void { this.refresh.update(value => value + 1); }
  protected formatCurrency(value: number): string { return money.format(value); }
  protected formatDate(value: string): string { return dateFormat.format(new Date(value)); }
  protected countLabel(count: number): string { return count === 1 ? '1 título' : `${count} títulos`; }

  protected exportCsv(): void {
    const titles = this.filtered();
    if (!titles.length) return;
    const rows = [
      ['Vencimento', 'Cliente', 'NF-e', 'Série', 'Parcela', 'Natureza da operação', 'Valor (BRL)', 'Faixa de vencimento', 'Recebimento'],
      ...titles.map(title => [
        this.formatDate(title.vencimento), title.pessoa ?? '', title.nota, title.serie,
        title.parcela || String(title.indice), title.natureza, title.valor.toFixed(2).replace('.', ','),
        this.dueLabels[this.dueStatus(title.vencimento)], 'Sem registro',
      ]),
    ];
    downloadCsv(rows, 'contas-a-receber.csv');
  }

  private loadTitles(empresaId: number, de: string, ate: string) {
    if (this.receivable) {
      return this.api.listarContasReceber(empresaId, de, ate).pipe(map(({ clientes, titulos, ...report }): Relatorio => ({
        ...report, pessoas: clientes,
        titulos: titulos.map(({ notaId, clienteId, cliente, ...parcela }) =>
          ({ ...parcela, documentoId: notaId, pessoaId: clienteId, pessoa: cliente })),
      })));
    }
    return this.api.listarContasPagar(empresaId, de, ate).pipe(map(({ fornecedores, titulos, ...report }): Relatorio => ({
      ...report, pessoas: fornecedores,
      titulos: titulos.map(({ entradaId, fornecedorId, fornecedor, ...parcela }) =>
        ({ ...parcela, documentoId: entradaId, pessoaId: fornecedorId, pessoa: fornecedor })),
    })));
  }
}
