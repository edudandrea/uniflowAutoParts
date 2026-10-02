export interface CriarClientePayload {
  empresaId: number;
  tipoPessoa: number;
  relacionamento: number;
  nomeRazaoSocial: string;
  nomeFantasia: string | null;
  cpfCnpj: string;
  rg: string | null;
  orgaoEmissor: string | null;
  estadoCivil: string | null;
  dataNascimento: string | null;
  profissao: string | null;
  cnh: string | null;
  inscricaoEstadual: string | null;
  inscricaoMunicipal: string | null;
  indicadorIe: number;
  consumidorFinal: boolean;
  email: string | null;
  telefone: string | null;
  celular: string | null;
  clienteDesde: string | null;
  origem: string | null;
  vendedorPadrao: string | null;
  limiteCredito: number | null;
  observacao: string | null;
  ativo: boolean;
  enderecoId: number | null;
  endereco: {
    tipo: number;
    cep: string;
    logradouro: string;
    numero: string;
    complemento: string | null;
    bairro: string;
    codigoIbgeMunicipio: string;
    municipio: string;
    uf: string;
    pais: string;
    principal: boolean;
  } | null;
  contatos: Array<{
    nome: string;
    cargo: string | null;
    email: string | null;
    telefone: string | null;
    celular: string | null;
    principal: boolean;
  }>;
  dadosComerciais: {
    condicaoPagamento: string | null;
    limiteCredito: number | null;
    tabelaPreco: string | null;
    vendedorPadrao: string | null;
    origem: string | null;
    bloquearVenda: boolean;
    permiteFiado: boolean;
  };
}

export interface ClienteResumoResponse {
  id: number;
  empresaId: number;
  nomeRazaoSocial: string;
  nomeFantasia: string | null;
  cpfCnpj: string;
  tipoPessoa: number;
  relacionamento: number;
  email: string | null;
  telefone: string | null;
  celular: string | null;
  ativo: boolean;
  criadoEm: string;
}

export interface CadastroEndereco {
  id: number;
  empresaId: number;
  nome: string;
  cep: string;
  logradouro: string;
  numero: string;
  complemento: string | null;
  bairro: string;
  codigoIbgeMunicipio: string;
  municipio: string;
  uf: string;
  pais: string;
  referencia: string | null;
  ativo: boolean;
}

export interface CriarCadastroEnderecoPayload {
  empresaId: number;
  nome: string;
  cep: string;
  logradouro: string;
  numero: string;
  complemento: string | null;
  bairro: string;
  codigoIbgeMunicipio: string | null;
  municipio: string;
  uf: string;
  pais: string | null;
  referencia: string | null;
  ativo: boolean;
}

export interface Categoria {
  id: number;
  tenantId: number;
  nome: string;
  descricao: string | null;
  categoriaPaiId: number | null;
  categoriaPaiNome: string | null;
  icone: string | null;
  ordem: number;
  ativo: boolean;
  criadoEm: string;
  atualizadoEm: string | null;
  subcategorias: number;
  produtos: number;
}

export interface CriarCategoriaPayload {
  tenantId: number;
  nome: string;
  descricao: string | null;
  categoriaPaiId: number | null;
  icone: string | null;
  ordem: number;
  ativo: boolean;
}

export interface Marca {
  id: number;
  tenantId: number;
  nome: string;
  codigo: string | null;
  descricao: string | null;
  logoUrl: string | null;
  site: string | null;
  observacao: string | null;
  ativo: boolean;
  criadoEm: string;
  atualizadoEm: string | null;
  produtos: number;
}

export interface CriarMarcaPayload {
  tenantId: number;
  nome: string;
  codigo: string | null;
  descricao: string | null;
  logoUrl: string | null;
  site: string | null;
  observacao: string | null;
  ativo: boolean;
}

export type AtualizarMarcaPayload = CriarMarcaPayload;

export interface DocumentoUploadResponse {
  key: string;
  url: string;
  contentType: string;
  size: number;
}

export interface Produto {
  id: number;
  empresaId: number;
  sku: number;
  codintern: number;
  codfabricante: number;
  descricao: string;
  categoriaId: number;
  categoria: string;
  marcaId: number;
  marca: string;
  fabricanteId: number;
  custo: number;
  preco: number;
  estoqueMinimo: number;
  estoqueMaximo: number;
  ncm: number;
  cest: number;
  origemMercadoria: string;
  aliquotaIpi: number;
  aliquotaIcms: number;
  aliquotaMva: number;
  impostosFabricante: string;
  ativo: boolean;
}

export interface ProdutoListaResponse {
  pagina: number;
  tamanhoPagina: number;
  total: number;
  totalAtivos: number;
  totalInativos: number;
  produtos: Produto[];
}

export interface CriarProdutoPayload {
  empresaId: number;
  sku: number;
  codintern: number;
  codfabricante: number;
  descricao: string;
  categoriaId: number;
  marcaId: number;
  fabricanteId: number;
  custo: number;
  preco: number;
  estoqueMinimo: number;
  estoqueMaximo: number;
  ncm: number;
  cest: number;
  origemMercadoria: string | null;
  aliquotaIpi: number;
  aliquotaIcms: number;
  aliquotaMva: number;
  impostosFabricante: string | null;
  ativo: boolean;
}

export interface ImportarProdutosFabricanteResponse {
  fabricante: string;
  arquivo: string;
  linhasLidas: number;
  produtosCriados: number;
  produtosAtualizados: number;
  linhasIgnoradas: number;
  erros: string[];
  produtos: Produto[];
}

export interface ImportarNfeItemResponse {
  numeroItem: number;
  codigoFornecedor: string;
  ean: string | null;
  descricaoXml: string;
  ncm: string | null;
  cest: string | null;
  cfop: string | null;
  unidadeComercial: string;
  quantidadeComercial: number;
  valorUnitarioComercial: number;
  unidadeTributavel: string;
  quantidadeTributavel: number;
  valorUnitarioTributavel: number;
  valorProduto: number;
  valorFrete: number;
  valorSeguro: number;
  valorDesconto: number;
  valorOutrasDespesas: number;
  quantidadeRecebida: number;
  custoUnitario: number;
  produtoIdentificado: boolean;
  produtoId: number | null;
}

export interface ImportarNfeResponse {
  entradaCompraId: number;
  chaveNfe: string;
  numeroNfe: string;
  serie: string;
  modelo: string;
  dataEmissao: string | null;
  dataEntrada: string | null;
  naturezaOperacao: string;
  fornecedorNome: string;
  fornecedorDocumento: string;
  valorProdutos: number;
  valorFrete: number;
  valorSeguro: number;
  valorDesconto: number;
  valorOutrasDespesas: number;
  valorTotal: number;
  totalItens: number;
  produtosIdentificados: number;
  produtosPendentes: number;
  itens: ImportarNfeItemResponse[];
}
