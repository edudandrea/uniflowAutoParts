using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace back.Models
{
    public class Empresas
    {
        public int Id { get; set; }
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string? Telefone { get; set; }
        public string? Email { get; set; }
        public string? EmailFiscal { get; set; }
        public string? Endereco { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? Cep { get; set; }
        public string? InscricaoMunicipal { get; set; }
        public string? InscricaoEstadual { get; set; }
        public string? LogoUrl { get; set; }
        public bool Ativo { get; set; } = true;
        public bool UtilizaAPAssistant { get; set; } = false;
        public bool AcessoBloqueado { get; set; } = false;
        public string? MotivoBloqueio { get; set; }
        public DateTime? BloqueadoEm { get; set; }
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    }
}