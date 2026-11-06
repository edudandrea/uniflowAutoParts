import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { FiscalCertificate } from './fiscal-certificate.service';

import {
  Categoria,
  EstoqueProduto,
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
  NotaFiscalFiltros,
  NotaFiscalListaResponse,
  NotaFiscalEventoResponse,
} from './app-models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  listarNotasFiscais(empresaId: number, pagina: number, filtros: NotaFiscalFiltros) {
    const params: Record<string, string | number> = { empresaId, pagina };
    for (const [key, value] of Object.entries(filtros)) {
      if (value !== '' && value !== null) params[key] = value;
    }
    return this.http.get<NotaFiscalListaResponse>(`${this.apiUrl}/notas-fiscais`, { params });
  }

  baixarNotaFiscal(empresaId: number, id: number, formato: 'pdf' | 'xml') {
    return this.http.get(`${this.apiUrl}/notas-fiscais/${id}/${formato}`, {
      params: { empresaId }, responseType: 'blob',
    });
  }

  registrarEventoFiscal(empresaId: number, id: number, acao: 'cancelar' | 'carta-correcao', texto: string, certificado: FiscalCertificate) {
    return this.http.post<NotaFiscalEventoResponse>(`${this.apiUrl}/notas-fiscais/${id}/${acao}`, {
      texto, base64: certificado.base64, password: certificado.password,
    }, { params: { empresaId } });
  }

  importarNotaFiscal(empresaId: number, xml: File, pdf: File | null) {
    const form = new FormData();
    form.append('empresaId', String(empresaId));
    form.append('xml', xml);
    if (pdf) form.append('pdf', pdf);
    return this.http.post<{ id: number }>(`${this.apiUrl}/notas-fiscais/importar`, form);
  }

  consultarNotaFiscal(empresaId: number, id: number, certificado: FiscalCertificate) {
    return this.http.post<{ codigo: string; motivo: string; situacao: string; pendente: boolean }>(
      `${this.apiUrl}/notas-fiscais/${id}/consultar`,
      { base64: certificado.base64, password: certificado.password }, { params: { empresaId } },
    );
  }

  validarCertificado(certificate: FiscalCertificate) {
    return this.http.post<{ validade: string }>(`${this.apiUrl}/fiscal/certificado/validar`, certificate);
  }

  consultarStatusSefaz(certificate: FiscalCertificate) {
    return this.http.post<{ codigo: string; motivo: string }>(`${this.apiUrl}/fiscal/sefaz/status`, certificate);
  }

  listarEstoque(empresaId: number) {
    return this.http.get<EstoqueProduto[]>(`${this.apiUrl}/estoque`, { params: { empresaId } });
  }

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
