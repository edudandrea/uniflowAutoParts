import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { CompaniesStoreService } from '../../core/companies-store.service';

@Component({
  selector: 'app-users',
  imports: [ReactiveFormsModule],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss',
})
export class UsersComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  protected readonly companiesStore = inject(CompaniesStoreService);

  protected readonly loading = signal(false);
  protected readonly message = signal('');

  protected readonly usuarioForm = this.fb.nonNullable.group({
    empresaId: ['', [Validators.required]],
    nome: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    senha: ['', [Validators.required, Validators.minLength(8)]],
    perfil: ['3', [Validators.required]],
  });

  ngOnInit(): void {
    if (this.companiesStore.empresas().length === 0) {
      this.companiesStore.carregar();
    }
  }

  protected cadastrarUsuario(): void {
    if (this.usuarioForm.invalid) {
      this.usuarioForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.message.set('');
    const value = this.usuarioForm.getRawValue();

    this.api
      .criarUsuario({
        empresaId: Number(value.empresaId),
        nome: value.nome,
        email: value.email,
        senha: value.senha,
        perfil: Number(value.perfil),
      })
      .subscribe({
        next: () => {
          this.loading.set(false);
          this.message.set('Usuario cadastrado com sucesso.');
          this.usuarioForm.reset({ perfil: '3', empresaId: '' });
        },
        error: (error) => {
          this.loading.set(false);
          this.message.set(error?.error || 'Nao foi possivel cadastrar o usuario.');
        },
      });
  }
}
