import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ClienteResumoResponse, Produto } from '../../core/app-models';

interface SaleItem { produto: Produto; quantidade: number; preco: number; desconto: number }
interface Payment { id: number; forma: string; valor: number }
interface SaleDraft {
  cliente: ClienteResumoResponse | null; itens: SaleItem[]; pagamentos: Payment[];
  tipo: string; vendedor: string; emitir: boolean; modelo: string; finalidade: string;
  natureza: string; cfop: string; serie: string; desconto: number; frete: number;
  despesas: number; observacoes: string;
}

@Component({
  selector: 'app-sales', imports: [FormsModule, RouterLink],
  templateUrl: './sales.component.html', styleUrl: './sales.component.scss',
})
export class SalesComponent {
  private readonly api = inject(ApiService);
  private readonly draftKey = 'uniflow:venda:rascunho:empresa-1';
  protected readonly clientes = signal<ClienteResumoResponse[]>([]);
  protected readonly produtos = signal<Produto[]>([]);
  protected readonly cliente = signal<ClienteResumoResponse | null>(null);
  protected readonly itens = signal<SaleItem[]>([]);
  protected readonly pagamentos = signal<Payment[]>([{ id: 1, forma: 'Pix', valor: 0 }]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal('');
  protected readonly message = signal('');
  protected readonly customerSearch = signal('');
  protected readonly productSearch = signal('');
  protected readonly customerResults = computed(() => {
    const term = this.customerSearch().trim().toLocaleLowerCase('pt-BR');
    return term ? this.clientes().filter(c => `${c.nomeRazaoSocial} ${c.cpfCnpj} ${c.id}`.toLocaleLowerCase('pt-BR').includes(term)).slice(0, 8) : [];
  });
  protected readonly productResults = computed(() => {
    const term = this.productSearch().trim().toLocaleLowerCase('pt-BR');
    return term ? this.produtos().filter(p => `${p.descricao} ${p.sku} ${p.codintern} ${p.codfabricante}`.toLocaleLowerCase('pt-BR').includes(term)).slice(0, 8) : [];
  });
  protected tipo = 'Venda';
  protected vendedor = '';
  protected emitir = true;
  protected modelo = '55';
  protected finalidade = 'Normal';
  protected natureza = 'Venda de mercadoria adquirida ou recebida de terceiros';
  protected cfop = '5102';
  protected serie = '1';
  protected desconto = 0;
  protected frete = 0;
  protected despesas = 0;
  protected observacoes = '';
  protected showExtras = false;
  protected readonly subtotal = computed(() => this.round(this.itens().reduce((sum, i) => sum + i.quantidade * i.preco, 0)));
  protected readonly itemDiscount = computed(() => this.round(this.itens().reduce((sum, i) => sum + i.quantidade * i.preco * i.desconto / 100, 0)));

  constructor() { this.restore(); this.loadCatalog(); }

  protected loadCatalog(): void {
    this.loading.set(true); this.loadError.set('');
    forkJoin({ clientes: this.api.listarClientes(1), produtos: this.api.listarProdutos(1, 1, 100) }).subscribe({
      next: ({ clientes, produtos }) => {
        this.clientes.set(clientes.filter(c => c.ativo && c.relacionamento !== 2));
        this.produtos.set(produtos.produtos.filter(p => p.ativo));
        // Load the remaining catalog pages so product search is not limited to the first page.
        const pages = Math.ceil(produtos.total / 100);
        if (pages > 1) forkJoin(Array.from({ length: pages - 1 }, (_, i) => this.api.listarProdutos(1, i + 2, 100))).subscribe({
          next: results => { this.produtos.update(p => [...p, ...results.flatMap(r => r.produtos.filter(x => x.ativo))]); this.loading.set(false); },
          error: () => { this.loading.set(false); this.loadError.set('Não foi possível carregar todo o catálogo. Tente novamente.'); },
        });
        else this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.loadError.set('Não foi possível carregar clientes e produtos. Verifique se o backend está disponível e tente novamente.'); },
    });
  }
  protected selectCustomer(c: ClienteResumoResponse): void { this.cliente.set(c); this.customerSearch.set(''); }
  protected addProduct(produto: Produto): void {
    this.itens.update(items => {
      const existing = items.find(i => i.produto.id === produto.id);
      return existing ? items.map(i => i === existing ? { ...i, quantidade: i.quantidade + 1 } : i) : [...items, { produto, quantidade: 1, preco: produto.preco, desconto: 0 }];
    });
    this.productSearch.set(''); this.message.set('');
  }
  protected updateItem(id: number, field: 'quantidade' | 'preco' | 'desconto', value: number | null): void {
    const number = Number.isFinite(value) ? Number(value) : 0;
    this.itens.update(items => items.map(i => i.produto.id === id ? { ...i, [field]: Math.max(field === 'quantidade' ? 0.01 : 0, field === 'desconto' ? Math.min(100, number) : number) } : i));
  }
  protected removeItem(id: number): void { this.itens.update(items => items.filter(i => i.produto.id !== id)); }
  protected itemTotal(i: SaleItem): number { return this.round(i.quantidade * i.preco * (1 - i.desconto / 100)); }
  protected total(): number { return this.round(Math.max(0, this.subtotal() - this.itemDiscount() - (this.desconto || 0) + (this.frete || 0) + (this.despesas || 0))); }
  protected paid(): number { return this.round(this.pagamentos().reduce((sum, p) => sum + p.valor, 0)); }
  protected balance(): number { return this.round(this.total() - this.paid()); }
  protected addPayment(): void { this.pagamentos.update(p => [...p, { id: Date.now(), forma: 'Pix', valor: Math.max(0, this.balance()) }]); }
  protected updatePayment(id: number, field: 'forma' | 'valor', value: string | number | null): void {
    this.pagamentos.update(payments => payments.map(p => p.id === id ? { ...p, [field]: field === 'valor' ? Math.max(0, Number(value) || 0) : value } : p));
  }
  protected removePayment(id: number): void { this.pagamentos.update(p => p.filter(x => x.id !== id)); }
  protected fillPayment(): void { this.pagamentos.set([{ id: Date.now(), forma: this.pagamentos()[0]?.forma ?? 'Pix', valor: this.total() }]); }
  protected currency(value: number): string { return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }); }
  private round(value: number): number { return Math.round((value + Number.EPSILON) * 100) / 100; }
  private draft(): SaleDraft {
    return { cliente: this.cliente(), itens: this.itens(), pagamentos: this.pagamentos(), tipo: this.tipo, vendedor: this.vendedor, emitir: this.emitir, modelo: this.modelo, finalidade: this.finalidade, natureza: this.natureza, cfop: this.cfop, serie: this.serie, desconto: this.desconto, frete: this.frete, despesas: this.despesas, observacoes: this.observacoes };
  }
  protected saveDraft(): boolean {
    try { localStorage.setItem(this.draftKey, JSON.stringify(this.draft())); this.message.set('Rascunho salvo neste navegador. Você pode continuar depois.'); return true; }
    catch { this.message.set('Não foi possível salvar o rascunho. Verifique o armazenamento do navegador.'); return false; }
  }
  private restore(): void {
    try {
      const raw = localStorage.getItem(this.draftKey); if (!raw) return;
      const draft: SaleDraft = JSON.parse(raw);
      if (!Array.isArray(draft.itens) || !Array.isArray(draft.pagamentos)) return;
      this.cliente.set(draft.cliente); this.itens.set(draft.itens); this.pagamentos.set(draft.pagamentos);
      this.tipo = draft.tipo; this.vendedor = draft.vendedor; this.emitir = draft.emitir;
      this.modelo = draft.modelo; this.finalidade = draft.finalidade; this.natureza = draft.natureza;
      this.cfop = draft.cfop; this.serie = draft.serie; this.desconto = draft.desconto;
      this.frete = draft.frete; this.despesas = draft.despesas; this.observacoes = draft.observacoes;
      this.message.set('Seu último rascunho foi recuperado.');
    } catch { this.message.set('Não foi possível recuperar o rascunho salvo.'); }
  }
  protected clear(): void {
    if ((this.itens().length || this.cliente()) && !window.confirm('Limpar a venda e excluir o rascunho salvo?')) return;
    this.cliente.set(null); this.itens.set([]); this.pagamentos.set([{ id: 1, forma: 'Pix', valor: 0 }]);
    this.desconto = 0; this.frete = 0; this.despesas = 0; this.observacoes = ''; this.vendedor = '';
    this.customerSearch.set(''); this.productSearch.set(''); this.message.set('');
    try { localStorage.removeItem(this.draftKey); } catch { this.message.set('A venda foi limpa, mas não foi possível excluir o rascunho salvo.'); }
  }
  protected finish(): void {
    if (!this.cliente()) { this.message.set('Selecione um cliente para continuar.'); return; }
    if (!this.itens().length) { this.message.set('Adicione pelo menos um produto à venda.'); return; }
    if (this.desconto < 0 || this.frete < 0 || this.despesas < 0 || this.desconto > this.subtotal() - this.itemDiscount()) { this.message.set('Revise os descontos, frete e despesas da venda.'); return; }
    if (this.balance() !== 0) { this.message.set('O valor dos pagamentos deve ser igual ao total da venda.'); return; }
    if (this.emitir && (!/^\d{4}$/.test(this.cfop) || !/^\d{1,3}$/.test(this.serie) || !this.natureza.trim())) { this.message.set('Preencha a natureza da operação, um CFOP de 4 dígitos e uma série válida.'); return; }
    if (this.saveDraft()) this.message.set(this.emitir ? 'Rascunho salvo. A emissão da nota aguarda a configuração do serviço emissor. Nenhuma nota fiscal foi emitida.' : 'Rascunho salvo. A conclusão da venda e a baixa de estoque aguardam a integração de vendas no backend.');
  }
}
