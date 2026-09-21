namespace back.Models
{
    public class CadastroEndereco
    {
        public long Id { get; set; }
        public int EmpresaId { get; set; }
        public string Nome { get; set; } = null!;
        public string Cep { get; set; } = null!;
        public string Logradouro { get; set; } = null!;
        public string Numero { get; set; } = null!;
        public string? Complemento { get; set; }
        public string Bairro { get; set; } = null!;
        public string CodigoIbgeMunicipio { get; set; } = "0000000";
        public string Municipio { get; set; } = null!;
        public string Uf { get; set; } = null!;
        public string Pais { get; set; } = "Brasil";
        public string? Referencia { get; set; }
        public bool Ativo { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime? AtualizadoEm { get; set; }

        public Empresas Empresa { get; set; } = null!;
        public ICollection<Cliente> Clientes { get; set; } = [];
    }
}
