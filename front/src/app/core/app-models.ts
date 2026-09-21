export interface BootstrapStatusResponse {
  administradorSaasCriado: boolean;
}

export interface UsuarioResponse {
  id: number;
  empresaId: number | null;
  nome: string;
  email: string;
  perfil: string;
  ativo: boolean;
  criadoEm: string;
}

export interface EmpresaResumoResponse {
  id: number;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  ativo: boolean;
  acessoBloqueado: boolean;
  dataCadastro: string;
}

export interface CriarEmpresaContratantePayload {
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  telefone: string | null;
  email: string | null;
  emailFiscal: string | null;
  endereco: string | null;
  numero: string | null;
  complemento: string | null;
  bairro: string | null;
  cidade: string | null;
  estado: string | null;
  cep: string | null;
  inscricaoMunicipal: string | null;
  inscricaoEstadual: string | null;
  logoUrl: string | null;
  utilizaAPAssistant: boolean;
  administrador: {
    nome: string;
    email: string;
    senha: string;
  };
}

export interface CriarUsuarioEmpresaPayload {
  empresaId: number;
  nome: string;
  email: string;
  senha: string;
  perfil: number;
}

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
