import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { ApiService } from '../../core/api.service';
import { AuthSessionService } from '../../core/auth-session.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthSessionService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly loadingStatus = signal(true);
  protected readonly administradorSaasCriado = signal(false);
  protected readonly showBootstrapModal = signal(false);
  protected readonly loginLoading = signal(false);
  protected readonly bootstrapLoading = signal(false);
  protected readonly loginMessage = signal('');
  protected readonly bootstrapMessage = signal('');
  protected readonly logoUnavailable = signal(false);

  protected readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    senha: ['', [Validators.required]],
  });

  protected readonly bootstrapForm = this.fb.nonNullable.group({
    nome: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    senha: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit(): void {
    this.carregarStatusBootstrap();
  }

  protected carregarStatusBootstrap(): void {
    this.loadingStatus.set(true);
    this.api.getBootstrapStatus().subscribe({
      next: (status) => {
        this.administradorSaasCriado.set(status.administradorSaasCriado);
        this.showBootstrapModal.set(!status.administradorSaasCriado);
        this.loadingStatus.set(false);
      },
      error: () => {
        this.loginMessage.set('Nao foi possivel consultar o status do administrador SaaS.');
        this.loadingStatus.set(false);
      },
    });
  }

  protected criarAdministradorSaas(): void {
    if (this.bootstrapForm.invalid || this.administradorSaasCriado()) {
      this.bootstrapForm.markAllAsTouched();
      return;
    }

    this.bootstrapLoading.set(true);
    this.bootstrapMessage.set('');

    this.api.criarAdministradorSaas(this.bootstrapForm.getRawValue()).subscribe({
      next: () => {
        this.administradorSaasCriado.set(true);
        this.showBootstrapModal.set(false);
        this.bootstrapLoading.set(false);
        this.loginMessage.set('Administrador SaaS criado. Entre com o e-mail e senha cadastrados.');
        this.loginForm.patchValue({
          email: this.bootstrapForm.controls.email.value,
          senha: '',
        });
        this.bootstrapForm.reset();
      },
      error: (error) => {
        this.bootstrapMessage.set(error?.error || 'Nao foi possivel criar o administrador SaaS.');
        this.bootstrapLoading.set(false);
      },
    });
  }

  protected entrar(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loginLoading.set(true);
    this.loginMessage.set('');

    this.api.login(this.loginForm.getRawValue()).subscribe({
      next: (usuario) => {
        this.auth.entrar(usuario);
        this.loginLoading.set(false);

        if (usuario.perfil === 'AdministradorSaas') {
          this.router.navigateByUrl('/saas/empresas');
          return;
        }

        this.router.navigateByUrl('/app/dashboard');
      },
      error: () => {
        this.loginLoading.set(false);
        this.loginMessage.set('E-mail ou senha invalidos.');
      },
    });
  }

  protected abrirCriacaoSaas(): void {
    if (!this.administradorSaasCriado()) {
      this.showBootstrapModal.set(true);
    }
  }

  protected acessarDashboardDesenvolvimento(): void {
    this.auth.entrar({
      id: 0,
      empresaId: 1,
      nome: 'Usuario desenvolvimento',
      email: 'dev@uniflow.local',
      perfil: 'AdministradorEmpresa',
      ativo: true,
      criadoEm: new Date().toISOString(),
    });

    this.router.navigateByUrl('/app/dashboard');
  }
}
