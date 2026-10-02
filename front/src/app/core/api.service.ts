import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

import {
  Categoria,
  CadastroEndereco,
  ClienteResumoResponse,
  CriarCadastroEnderecoPayload,
  CriarCategoriaPayload,
  CriarClientePayload,
  CriarMarcaPayload,
  AtualizarMarcaPayload,
  CriarProdutoPayload,
  DocumentoUploadResponse,
  ImportarProdutosFabricanteResponse,
  ImportarNfeResponse,
  Marca,
  Produto,
  ProdutoListaResponse,
} from './app-models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5031/api';

  criarCliente(payload: CriarClientePayload) {
    return this.http.post<ClienteResumoResponse>(`${this.apiUrl}/clientes`, payload);
  }

  listarClientes(empresaId: number) {
    return this.http.get<ClienteResumoResponse[]>(`${this.apiUrl}/clientes`, { params: { empresaId } });
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

  atualizarMarca(id: number, payload: AtualizarMarcaPayload) {
    return this.http.put<Marca>(`${this.apiUrl}/marcas/${id}`, payload);
  }

  enviarLogoMarca(tenantId: number, arquivo: File) {
    const formData = new FormData();
    formData.append('arquivo', arquivo);

    return this.http.post<DocumentoUploadResponse>(`${this.apiUrl}/documentos/empresas/${tenantId}/marcas/logos`, formData);
  }

  listarProdutos(empresaId: number, pagina = 1, tamanhoPagina = 50, busca = '') {
    return this.http.get<ProdutoListaResponse>(`${this.apiUrl}/produtos`, {
      params: { empresaId, pagina, tamanhoPagina, busca },
    });
  }

  criarProduto(payload: CriarProdutoPayload) {
    return this.http.post<Produto>(`${this.apiUrl}/produtos`, payload);
  }

  importarProdutosFabricante(empresaId: number, fabricante: string, atualizarPrecos: boolean, arquivo: File) {
    const formData = new FormData();
    formData.append('empresaId', String(empresaId));
    formData.append('fabricante', fabricante);
    formData.append('atualizarPrecos', String(atualizarPrecos));
    formData.append('arquivo', arquivo);

    return this.http.post<ImportarProdutosFabricanteResponse>(`${this.apiUrl}/produtos/importar-fabricante`, formData);
  }

  importarXmlNfeCompra(tenantId: number, arquivo: File) {
    const formData = new FormData();
    formData.append('tenantId', String(tenantId));
    formData.append('arquivo', arquivo);

    return this.http.post<ImportarNfeResponse>(`${this.apiUrl}/estoque/importar-nfe`, formData);
  }
}
