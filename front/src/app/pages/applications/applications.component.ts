import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

type ApplicationTab = 'search' | 'details';

interface PartApplication {
  id: number;
  montadora: string;
  modelo: string;
  versao: string;
  motor: string;
  anoInicial: number;
  anoFinal: number;
  posicao: string;
  observacao: string;
}

interface ApplicationPart {
  id: number;
  codigo: string;
  codigoFabricante: string;
  descricao: string;
  marca: string;
  categoria: string;
  ncm: string;
  codigoBarras: string;
  unidade: string;
  peso: string;
  dimensoes: string;
  preco: number;
  estoque: number;
  status: 'Ativo' | 'Inativo';
  applications: PartApplication[];
}

@Component({
  selector: 'app-applications',
  imports: [FormsModule, AppIconComponent],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss',
})
export class ApplicationsComponent {
  protected readonly activeTab = signal<ApplicationTab>('search');
  protected readonly searchTerm = signal('W610/3');
  protected readonly selectedPartId = signal(1);
  protected readonly montadoraFiltro = signal('Todas');
  protected readonly modeloFiltro = signal('Todos');
  protected readonly anoDeFiltro = signal('Todos');
  protected readonly anoAteFiltro = signal('Todos');
  protected readonly motorFiltro = signal('Todos');

  protected novaAplicacao = {
    montadora: 'Volkswagen',
    modelo: 'Polo',
    versao: '1.0 TSI',
    motor: 'EA211 1.0 Turbo',
    anoInicial: 2018,
    anoFinal: 2026,
    posicao: 'Motor',
    observacao: 'Flex',
  };

  protected readonly parts = signal<ApplicationPart[]>([
    {
      id: 1,
      codigo: 'W610/3',
      codigoFabricante: 'W610/3',
      descricao: 'Filtro de oleo do motor',
      marca: 'Mann Filter',
      categoria: 'Filtros',
      ncm: '8421.23.00',
      codigoBarras: '4011558123456',
      unidade: 'UN',
      peso: '0,280 kg',
      dimensoes: '76 x 76 x 93 mm',
      preco: 42.9,
      estoque: 18,
      status: 'Ativo',
      applications: [
        { id: 1, montadora: 'Volkswagen', modelo: 'Gol', versao: '1.0 MPI', motor: 'EA211 12v', anoInicial: 2018, anoFinal: 2023, posicao: 'Motor', observacao: 'Flex' },
        { id: 2, montadora: 'Volkswagen', modelo: 'Gol', versao: '1.6 MSI', motor: 'EA211 16v', anoInicial: 2018, anoFinal: 2023, posicao: 'Motor', observacao: 'Flex' },
        { id: 3, montadora: 'Volkswagen', modelo: 'Polo', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2018, anoFinal: 2026, posicao: 'Motor', observacao: 'Flex' },
        { id: 4, montadora: 'Volkswagen', modelo: 'Virtus', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2018, anoFinal: 2026, posicao: 'Motor', observacao: 'Flex' },
        { id: 5, montadora: 'Volkswagen', modelo: 'T-Cross', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2019, anoFinal: 2026, posicao: 'Motor', observacao: 'Flex' },
        { id: 6, montadora: 'Volkswagen', modelo: 'Nivus', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2020, anoFinal: 2026, posicao: 'Motor', observacao: 'Flex' },
        { id: 7, montadora: 'Volkswagen', modelo: 'Saveiro', versao: '1.6 MSI', motor: 'EA211 16v', anoInicial: 2018, anoFinal: 2023, posicao: 'Motor', observacao: 'Flex' },
        { id: 8, montadora: 'Volkswagen', modelo: 'Voyage', versao: '1.6 MSI', motor: 'EA211 16v', anoInicial: 2018, anoFinal: 2023, posicao: 'Motor', observacao: 'Flex' },
        { id: 9, montadora: 'Volkswagen', modelo: 'Up', versao: '1.0 MPI', motor: 'EA211 12v', anoInicial: 2018, anoFinal: 2021, posicao: 'Motor', observacao: 'Flex' },
        { id: 10, montadora: 'Volkswagen', modelo: 'Fox', versao: '1.6 MSI', motor: 'EA211 16v', anoInicial: 2018, anoFinal: 2021, posicao: 'Motor', observacao: 'Flex' },
        { id: 11, montadora: 'Volkswagen', modelo: 'Jetta', versao: '1.4 TSI', motor: 'EA211 1.4 Turbo', anoInicial: 2019, anoFinal: 2022, posicao: 'Motor', observacao: 'Flex' },
        { id: 12, montadora: 'Volkswagen', modelo: 'Tiguan', versao: '1.4 TSI', motor: 'EA211 1.4 Turbo', anoInicial: 2019, anoFinal: 2022, posicao: 'Motor', observacao: 'Flex' },
      ],
    },
    {
      id: 2,
      codigo: 'BP5678',
      codigoFabricante: 'BP5678',
      descricao: 'Pastilha de freio dianteira',
      marca: 'Bosch',
      categoria: 'Freios',
      ncm: '8708.30.19',
      codigoBarras: '7891104567890',
      unidade: 'JG',
      peso: '1,120 kg',
      dimensoes: '142 x 56 x 17 mm',
      preco: 289.9,
      estoque: 8,
      status: 'Ativo',
      applications: [
        { id: 21, montadora: 'Volkswagen', modelo: 'Polo', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2018, anoFinal: 2022, posicao: 'Dianteira', observacao: 'Sistema Bosch' },
        { id: 22, montadora: 'Volkswagen', modelo: 'Virtus', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2018, anoFinal: 2022, posicao: 'Dianteira', observacao: 'Sistema Bosch' },
        { id: 23, montadora: 'Volkswagen', modelo: 'T-Cross', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2019, anoFinal: 2023, posicao: 'Dianteira', observacao: 'Sistema Bosch' },
      ],
    },
    {
      id: 3,
      codigo: 'P3312',
      codigoFabricante: 'P3312',
      descricao: 'Filtro de oleo equivalente',
      marca: 'Bosch',
      categoria: 'Filtros',
      ncm: '8421.23.00',
      codigoBarras: '7891104003312',
      unidade: 'UN',
      peso: '0,300 kg',
      dimensoes: '78 x 78 x 95 mm',
      preco: 45.8,
      estoque: 6,
      status: 'Ativo',
      applications: [
        { id: 31, montadora: 'Volkswagen', modelo: 'Polo', versao: '1.0 TSI', motor: 'EA211 1.0 Turbo', anoInicial: 2018, anoFinal: 2026, posicao: 'Motor', observacao: 'Flex' },
      ],
    },
  ]);

  protected readonly selectedPart = computed(() => {
    return this.parts().find((part) => part.id === this.selectedPartId()) ?? this.parts()[0];
  });

  protected readonly searchResults = computed(() => {
    const term = this.normalize(this.searchTerm());
    if (!term) {
      return this.parts();
    }

    return this.parts().filter((part) => {
      const content = `${part.codigo} ${part.codigoFabricante} ${part.descricao} ${part.marca} ${part.categoria}`;
      return this.normalize(content).includes(term);
    });
  });

  protected readonly filteredApplications = computed(() => {
    const anoDe = this.anoDeFiltro() === 'Todos' ? null : Number(this.anoDeFiltro());
    const anoAte = this.anoAteFiltro() === 'Todos' ? null : Number(this.anoAteFiltro());

    return this.selectedPart().applications.filter((application) => {
      return (this.montadoraFiltro() === 'Todas' || application.montadora === this.montadoraFiltro())
        && (this.modeloFiltro() === 'Todos' || application.modelo === this.modeloFiltro())
        && (this.motorFiltro() === 'Todos' || application.motor === this.motorFiltro())
        && (!anoDe || application.anoFinal >= anoDe)
        && (!anoAte || application.anoInicial <= anoAte);
    });
  });

  protected readonly montadoras = computed(() => this.unique(this.selectedPart().applications.map((item) => item.montadora)));
  protected readonly modelos = computed(() => this.unique(this.selectedPart().applications.map((item) => item.modelo)));
  protected readonly motores = computed(() => this.unique(this.selectedPart().applications.map((item) => item.motor)));
  protected readonly years = Array.from({ length: 10 }, (_, index) => String(2018 + index));

  protected selectPart(part: ApplicationPart): void {
    this.selectedPartId.set(part.id);
    this.activeTab.set('details');
    this.resetFilters();
  }

  protected salvarAplicacao(): void {
    const part = this.selectedPart();
    const nextApplication: PartApplication = {
      id: Date.now(),
      ...this.novaAplicacao,
    };

    this.parts.update((parts) => parts.map((item) => {
      if (item.id !== part.id) {
        return item;
      }

      return { ...item, applications: [...item.applications, nextApplication] };
    }));
    this.resetFilters();
  }

  protected removerAplicacao(id: number): void {
    const part = this.selectedPart();
    this.parts.update((parts) => parts.map((item) => {
      if (item.id !== part.id) {
        return item;
      }

      return { ...item, applications: item.applications.filter((application) => application.id !== id) };
    }));
  }

  protected resetFilters(): void {
    this.montadoraFiltro.set('Todas');
    this.modeloFiltro.set('Todos');
    this.anoDeFiltro.set('Todos');
    this.anoAteFiltro.set('Todos');
    this.motorFiltro.set('Todos');
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
  }

  private unique(values: string[]): string[] {
    return [...new Set(values)].sort((a, b) => a.localeCompare(b));
  }

  private normalize(value: string): string {
    return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }
}
