import { computed, Injectable, signal } from '@angular/core';

import { UsuarioResponse } from './app-models';

@Injectable({ providedIn: 'root' })
export class AuthSessionService {
  readonly usuario = signal<UsuarioResponse | null>(null);
  readonly isAdministradorSaas = computed(() => this.usuario()?.perfil === 'AdministradorSaas');

  entrar(usuario: UsuarioResponse) {
    this.usuario.set(usuario);
  }

  sair() {
    this.usuario.set(null);
  }
}
