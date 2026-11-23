import { Component, computed, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Municipio } from '../../core/app-models';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

@Component({
  selector: 'app-municipalities',
  imports: [AppIconComponent, ReactiveFormsModule],
  templateUrl: './municipalities.component.html',
  styleUrl: './municipalities.component.scss',
})
export class MunicipalitiesComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('municipioDialog');
  private readonly municipios = signal<Municipio[]>([]);
  protected readonly busca = signal('');
  protected readonly uf = signal('');
  protected readonly pagina = signal(1);
  protected readonly carregando = signal(false);
  protected readonly erro = signal('');
  protected readonly salvando = signal(false);
  protected readonly erroCadastro = signal('');
  protected readonly mensagem = signal('');
  protected readonly form = inject(FormBuilder).nonNullable.group({
    nome: ['', [Validators.required, Validators.maxLength(120)]],
    uf: ['', Validators.required],
    codigoIbge: ['', [Validators.required, Validators.pattern(/^[0-9]{7}$/)]],
  });
  protected readonly ufs = computed(() => [...new Set(this.municipios().map(m => m.uf))].sort());
  protected readonly filtrados = computed(() => {
    const busca = this.normalizar(this.busca().trim());
    const uf = this.uf();
    return this.municipios().filter(m => (!uf || m.uf === uf) &&
      (!busca || m.codigoIbge.includes(busca) || this.normalizar(m.nome).includes(busca)));
  });
  private readonly porPagina = 25;
  protected readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.filtrados().length / this.porPagina)));
  protected readonly visiveis = computed(() => {
    const inicio = (this.pagina() - 1) * this.porPagina;
    return this.filtrados().slice(inicio, inicio + this.porPagina);
  });

  constructor() {
    this.carregar();
  }

  protected carregar(): void {
    this.carregando.set(true);
    this.erro.set('');
    this.api.listarMunicipios().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: municipios => { this.municipios.set(municipios); this.carregando.set(false); },
      error: () => { this.erro.set('Não foi possível carregar os municípios.'); this.carregando.set(false); },
    });
  }

  protected abrir(): void {
    this.form.reset();
    this.erroCadastro.set('');
    this.mensagem.set('');
    this.dialog().nativeElement.showModal();
  }

  protected salvar(): void {
    if (this.salvando()) return;
    this.erroCadastro.set('');
    const dados = this.form.getRawValue();
    if (this.form.invalid || !dados.nome.trim()) {
      this.erroCadastro.set('Informe o nome, a UF e um código IBGE de sete dígitos.');
      return;
    }
    this.salvando.set(true);
    this.api.criarMunicipio(dados).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: municipio => {
        this.salvando.set(false);
        this.dialog().nativeElement.close();
        this.mensagem.set(`${municipio.nome} (${municipio.uf}) foi cadastrado.`);
        this.municipios.update(municipios => [...municipios, municipio].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')));
        this.busca.set(municipio.codigoIbge);
        this.uf.set('');
        this.pagina.set(1);
      },
      error: erro => {
        this.salvando.set(false);
        this.erroCadastro.set(typeof erro.error === 'string' ? erro.error : 'Não foi possível cadastrar o município.');
      },
    });
  }

  private normalizar(texto: string): string {
    return texto.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase('pt-BR');
  }
}
