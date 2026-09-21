import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

import {
  BootstrapStatusResponse,
  Categoria,
  CadastroEndereco,
  ClienteResumoResponse,
  CriarCadastroEnderecoPayload,
  CriarCategoriaPayload,
  CriarClientePayload,
  CriarEmpresaContratantePayload,
  CriarMarcaPayload,
  CriarUsuarioEmpresaPayload,
  EmpresaResumoResponse,
  Marca,
  UsuarioResponse,
} from './app-models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5031/api';

  getBootstrapStatus() {
    return this.http.get<BootstrapStatusResponse>(`${this.apiUrl}/saas/bootstrap-status`);
  }

  criarAdministradorSaas(payload: { nome: string; email: string; senha: string }) {
    return this.http.post<UsuarioResponse>(`${this.apiUrl}/saas/administradores`, payload);
  }

  login(payload: { email: string; senha: string }) {
    return this.http.post<UsuarioResponse>(`${this.apiUrl}/auth/login`, payload);
  }

  listarEmpresas() {
    return this.http.get<EmpresaResumoResponse[]>(`${this.apiUrl}/saas/empresas`);
  }

  criarEmpresa(payload: CriarEmpresaContratantePayload) {
    return this.http.post(`${this.apiUrl}/saas/empresas`, payload);
  }

  criarUsuario(payload: CriarUsuarioEmpresaPayload) {
    return this.http.post(`${this.apiUrl}/saas/usuarios`, payload);
  }

  criarCliente(payload: CriarClientePayload) {
    return this.http.post<ClienteResumoResponse>(`${this.apiUrl}/clientes`, payload);
  }

  listarEnderecos(empresaId: number, busca = '') {
    return this.http.get<CadastroEndereco[]>(`${this.apiUrl}/enderecos`, {
      params: { empresaId, busca },
    });
  }

  criarEndereco(payload: CriarCadastroEnderecoPayload) {
    return this.http.post<CadastroEndereco>(`${this.apiUrl}/enderecos`, payload);
  }

  listarCategorias(tenantId: number, incluirInativas = false) {
    return this.http.get<Categoria[]>(`${this.apiUrl}/categorias`, {
      params: { tenantId, incluirInativas },
    });
  }

  criarCategoria(payload: CriarCategoriaPayload) {
    return this.http.post<Categoria>(`${this.apiUrl}/categorias`, payload);
  }

  listarMarcas(tenantId: number, incluirInativas = false) {
    return this.http.get<Marca[]>(`${this.apiUrl}/marcas`, {
      params: { tenantId, incluirInativas },
    });
  }

  criarMarca(payload: CriarMarcaPayload) {
    return this.http.post<Marca>(`${this.apiUrl}/marcas`, payload);
  }
}
