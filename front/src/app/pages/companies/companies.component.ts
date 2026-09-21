import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { CompaniesStoreService } from '../../core/companies-store.service';
import { CriarEmpresaContratantePayload } from '../../core/app-models';

@Component({
  selector: 'app-companies',
  imports: [ReactiveFormsModule],
  templateUrl: './companies.component.html',
  styleUrl: './companies.component.scss',
})
export class CompaniesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  protected readonly companiesStore = inject(CompaniesStoreService);

  protected readonly loading = signal(false);
  protected readonly message = signal('');

  protected readonly empresaForm = this.fb.nonNullable.group({
    razaoSocial: ['', [Validators.required, Validators.minLength(3)]],
    nomeFantasia: ['', [Validators.required, Validators.minLength(2)]],
    cnpj: ['', [Validators.required, Validators.minLength(14)]],
    telefone: [''],
    email: ['', [Validators.email]],
    emailFiscal: ['', [Validators.email]],
    endereco: [''],
    numero: [''],
    complemento: [''],
    bairro: [''],
    cidade: [''],
    estado: [''],
    cep: [''],
    inscricaoMunicipal: [''],
    inscricaoEstadual: [''],
    logoUrl: [''],
    utilizaAPAssistant: [false],
    administradorNome: ['', [Validators.required, Validators.minLength(3)]],
    administradorEmail: ['', [Validators.required, Validators.email]],
    administradorSenha: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit(): void {
    this.companiesStore.carregar();
  }

  protected cadastrarEmpresa(): void {
    if (this.empresaForm.invalid) {
      this.empresaForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.message.set('');
    const value = this.empresaForm.getRawValue();
    const payload: CriarEmpresaContratantePayload = {
      razaoSocial: value.razaoSocial,
      nomeFantasia: value.nomeFantasia,
      cnpj: value.cnpj,
      telefone: value.telefone || null,
      email: value.email || null,
      emailFiscal: value.emailFiscal || null,
      endereco: value.endereco || null,
      numero: value.numero || null,
      complemento: value.complemento || null,
      bairro: value.bairro || null,
      cidade: value.cidade || null,
      estado: value.estado || null,
      cep: value.cep || null,
      inscricaoMunicipal: value.inscricaoMunicipal || null,
      inscricaoEstadual: value.inscricaoEstadual || null,
      logoUrl: value.logoUrl || null,
      utilizaAPAssistant: value.utilizaAPAssistant,
      administrador: {
        nome: value.administradorNome,
        email: value.administradorEmail,
        senha: value.administradorSenha,
      },
    };

    this.api.criarEmpresa(payload).subscribe({
      next: () => {
        this.loading.set(false);
        this.message.set('Empresa contratante cadastrada com administrador inicial.');
        this.empresaForm.reset({ utilizaAPAssistant: false });
        this.companiesStore.carregar();
      },
      error: (error) => {
        this.loading.set(false);
        this.message.set(error?.error || 'Nao foi possivel cadastrar a empresa.');
      },
    });
  }
}
