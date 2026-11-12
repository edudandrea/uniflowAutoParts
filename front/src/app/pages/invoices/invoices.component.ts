import { SessionService } from '../../core/session.service';
import { DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, catchError, of, switchMap, tap } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { NotaFiscal, NotaFiscalEventoHistorico, NotaFiscalFiltros, NotaFiscalListaResponse } from '../../core/app-models';
import { FiscalCertificateService } from '../../core/fiscal-certificate.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

const emptyFilters = (): NotaFiscalFiltros => ({
  busca: '', tipo: '', situacao: '', de: '', ate: '', serie: '', ambiente: '', valorMinimo: null, valorMaximo: null,
});

@Component({
  selector: 'app-invoices',
  imports: [FormsModule, DatePipe, AppIconComponent],
  templateUrl: './invoices.component.html',
  styleUrl: './invoices.component.scss',
})
export class InvoicesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  private readonly certificates = inject(FiscalCertificateService);
  private readonly destroyRef = inject(DestroyRef);
  private get tenantId(): number { return this.session.empresaId; }
  private readonly requests = new Subject<{ pagina: number; filtros: NotaFiscalFiltros }>();
  private appliedFilters = emptyFilters();
  private returnFocus: HTMLElement | null = null;
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('eventDialog');
  private readonly importDialog = viewChild<ElementRef<HTMLDialogElement>>('importDialog');

  protected filters = emptyFilters();
  protected eventText = '';
  protected noCirculation = false;
  protected readonly history = signal<NotaFiscalEventoHistorico[]>([]);
  protected readonly historyNote = signal<NotaFiscal | null>(null);
  protected readonly historyBusy = signal(false);
  protected readonly historyError = signal('');
  private readonly historyDialog = viewChild<ElementRef<HTMLDialogElement>>('historyDialog');
  protected readonly data = signal<NotaFiscalListaResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal('');
  protected readonly message = signal('');
  protected readonly showAdvanced = signal(false);
  protected readonly event = signal<{ nota: NotaFiscal; acao: 'cancelar' | 'carta-correcao' } | null>(null);
  protected readonly eventError = signal('');
  protected readonly busy = signal(false);
  protected readonly downloading = signal<string | null>(null);
  protected readonly importError = signal('');
  protected readonly importing = signal(false);
  protected readonly consulting = signal<number | null>(null);
  protected xmlFile: File | null = null;
  protected pdfFile: File | null = null;

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil((this.data()?.total ?? 0) / 10)));
  protected readonly page = computed(() => this.data()?.pagina ?? 1);
  protected readonly firstResult = computed(() => this.data()?.total ? (this.page() - 1) * 10 + 1 : 0);
  protected readonly lastResult = computed(() => Math.min(this.page() * 10, this.data()?.total ?? 0));
  protected readonly pages = computed(() => {
    const start = Math.max(1, Math.min(this.page() - 2, this.totalPages() - 4));
    return Array.from({ length: Math.min(5, this.totalPages()) }, (_, i) => start + i);
  });

  ngOnInit(): void {
    this.requests.pipe(
      tap(() => { this.loading.set(true); this.loadError.set(''); }),
      switchMap(({ pagina, filtros }) => this.api.listarNotasFiscais(this.tenantId, pagina, filtros).pipe(
        catchError((error: HttpErrorResponse) => {
          this.loadError.set(this.errorMessage(error, 'Nao foi possivel carregar as notas fiscais.'));
          return of(null);
        }),
      )),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((data) => { this.data.set(data); this.loading.set(false); });
    this.loadPage(1);
  }

  protected search(): void {
    if ((this.filters.de && this.filters.ate && this.filters.de > this.filters.ate)
      || (this.filters.valorMinimo !== null && this.filters.valorMinimo < 0)
      || (this.filters.valorMaximo !== null && this.filters.valorMaximo < 0)
      || (this.filters.valorMinimo !== null && this.filters.valorMaximo !== null && this.filters.valorMinimo > this.filters.valorMaximo)) {
      this.message.set('Revise o periodo e o intervalo de valores dos filtros.');
      return;
    }
    this.message.set('');
    this.appliedFilters = { ...this.filters };
    this.loadPage(1);
  }

  protected clearFilters(): void {
    this.filters = emptyFilters();
    this.search();
  }

  protected loadPage(pagina: number): void {
    this.requests.next({ pagina, filtros: { ...this.appliedFilters } });
  }

  protected openEvent(nota: NotaFiscal, acao: 'cancelar' | 'carta-correcao'): void {
    if (acao === 'cancelar' ? !nota.podeCancelar : !nota.podeCorrigir) return;
    this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.event.set({ nota, acao });
    this.eventText = ''; this.noCirculation = false;
    this.eventError.set('');
    this.dialog()?.nativeElement.showModal();
  }

  protected closeEvent(): void {
    if (this.busy()) return;
    this.dialog()?.nativeElement.close();
    this.event.set(null);
    this.returnFocus?.focus();
  }

  protected submitEvent(): void {
    const event = this.event();
    if (!event || this.busy()) return;
    if (event.acao === 'cancelar' && !this.noCirculation) { this.eventError.set('Confirme que a mercadoria não circulou e a operação não foi realizada.'); return; }
    const texto = this.eventText.trim();
    const max = event.acao === 'cancelar' ? 255 : 1000;
    if (texto.length < 15 || texto.length > max) {
      this.eventError.set(`Informe um texto entre 15 e ${max} caracteres.`);
      return;
    }
    try {
      const certificate = this.certificates.load();
      if (!certificate) {
        this.eventError.set('Cadastre o certificado A1 em Configuracoes > Certificado e Tributacoes.');
        return;
      }
      this.busy.set(true);
      this.eventError.set('');
      this.api.registrarEventoFiscal(this.tenantId, event.nota.id, event.acao, texto, certificate, this.noCirculation)
        .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
          next: (result) => {
            this.busy.set(false);
            this.closeEvent();
            this.message.set(`${result.motivo} Protocolo: ${result.protocolo}.`);
            this.loadPage(this.page());
          },
          error: (error: HttpErrorResponse) => {
            this.busy.set(false);
            this.eventError.set(this.errorMessage(error, 'Nao foi possivel confirmar o evento na SEFAZ.'));
            this.loadPage(this.page());
          },
        });
    } catch {
      this.eventError.set('Certificado salvo invalido. Cadastre novamente nas configuracoes.');
    }
  }

  protected showHistory(nota: NotaFiscal): void {
    this.historyNote.set(nota); this.history.set([]); this.historyError.set(''); this.historyBusy.set(true); this.historyDialog()?.nativeElement.showModal();
    this.api.listarEventosFiscais(this.tenantId, nota.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: events => { this.history.set(events); this.historyBusy.set(false); }, error: () => { this.historyBusy.set(false); this.historyError.set('Não foi possível carregar os eventos fiscais.'); } });
  }
  protected closeHistory(): void { this.historyDialog()?.nativeElement.close(); }
  protected downloadEvent(evento: NotaFiscalEventoHistorico): void {
    const nota = this.historyNote(); if (!nota || evento.situacao !== 'Confirmado') return;
    this.api.baixarEventoFiscal(this.tenantId, nota.id, evento.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: blob => {
      const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = `evento-${nota.numero}-${evento.tipo}-${evento.sequencia}.xml`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    }, error: () => this.historyError.set('Não foi possível baixar o XML do evento.') });
  }
  protected download(nota: NotaFiscal, formato: 'pdf' | 'xml'): void {
    if (this.downloading()) return;
    this.downloading.set(`${nota.id}-${formato}`);
    this.api.baixarNotaFiscal(this.tenantId, nota.id, formato).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (blob) => {
        this.saveFile(blob, `nota-${nota.numero}-${nota.serie}.${formato}`);
        this.downloading.set(null);
      },
      error: () => {
        this.downloading.set(null);
        this.message.set(`Nao foi possivel baixar o ${formato.toUpperCase()}. Tente novamente.`);
      },
    });
  }

  protected async copyKey(nota: NotaFiscal): Promise<void> {
    try { await navigator.clipboard.writeText(nota.chaveAcesso); this.message.set('Chave de acesso copiada.'); }
    catch { this.message.set('Nao foi possivel copiar. Selecione e copie a chave de acesso na tabela.'); }
  }

  protected consult(nota: NotaFiscal): void {
    if (this.consulting()) return;
    try {
      const certificate = this.certificates.load();
      if (!certificate) { this.message.set('Cadastre o certificado A1 nas configuracoes para consultar a SEFAZ.'); return; }
      this.consulting.set(nota.id);
      this.api.consultarNotaFiscal(this.tenantId, nota.id, certificate).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (result) => {
          this.consulting.set(null);
          this.message.set(`SEFAZ ${result.codigo}: ${result.motivo}.${result.pendente ? ' Existe evento ainda sem confirmacao; novos envios permanecem bloqueados.' : ''}`);
          this.loadPage(this.page());
        },
        error: (error: HttpErrorResponse) => {
          this.consulting.set(null);
          this.message.set(this.errorMessage(error, 'Nao foi possivel consultar a SEFAZ.'));
        },
      });
    } catch { this.message.set('Certificado salvo invalido. Cadastre novamente nas configuracoes.'); }
  }

  protected exportPage(): void {
    const notas = this.data()?.notas ?? [];
    if (!notas.length) return;
    // Prefixa celulas iniciadas por formula antes de exportar para planilhas.
    const cell = (value: unknown) => {
      let text = String(value);
      if (/^[=+@\-\t\r]/.test(text)) text = "'" + text;
      return '"' + text.replace(/"/g, '""') + '"';
    };
    const rows = [['Numero', 'Serie', 'Emissao', 'Cliente', 'Tipo', 'Valor', 'Situacao', 'Chave de acesso'],
      ...notas.map(n => [n.numero, n.serie, n.emissao, n.cliente, this.typeLabel(n.tipo), n.valor.toFixed(2), n.situacao, "'" + n.chaveAcesso])];
    this.saveFile(new Blob(['\uFEFF' + rows.map(row => row.map(cell).join(';')).join('\r\n')], { type: 'text/csv;charset=utf-8' }), `notas-pagina-${this.page()}.csv`);
  }

  protected openImport(): void {
    this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.importError.set('');
    this.importDialog()?.nativeElement.showModal();
  }

  protected closeImport(): void {
    if (this.importing()) return;
    this.importDialog()?.nativeElement.close();
    this.returnFocus?.focus();
  }

  protected selectFile(event: Event, formato: 'xml' | 'pdf'): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (formato === 'xml') this.xmlFile = file;
    else this.pdfFile = file;
  }

  protected importInvoice(): void {
    if (this.importing()) return;
    if (!this.xmlFile) { this.importError.set('Selecione o XML autorizado da nota de saida.'); return; }
    if (this.xmlFile.size > 5_000_000 || (this.pdfFile?.size ?? 0) > 5_000_000) {
      this.importError.set('Cada arquivo deve ter ate 5 MB.'); return;
    }
    this.importing.set(true);
    this.importError.set('');
    this.api.importarNotaFiscal(this.tenantId, this.xmlFile, this.pdfFile).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.importing.set(false);
        this.closeImport();
        this.message.set('Nota fiscal importada. O status reflete o protocolo do XML; eventos posteriores devem ser conferidos na SEFAZ.');
        this.loadPage(1);
      },
      error: (error: HttpErrorResponse) => {
        this.importing.set(false);
        this.importError.set(this.errorMessage(error, 'Nao foi possivel importar a nota fiscal.'));
      },
    });
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
  }

  protected typeLabel(tipo: string): string { return tipo === 'NFCe' ? 'NFC-e' : 'NF-e'; }

  private saveFile(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  private errorMessage(error: HttpErrorResponse, fallback: string): string {
    return typeof error.error === 'string' ? error.error : error.error?.detail ?? fallback;
  }
}
