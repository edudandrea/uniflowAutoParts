namespace back.Models
{
    public class Marca
    {
        public long Id { get; set; }
        public long TenantId { get; set; }

        public string Nome { get; set; } = null!;

        public string? Codigo { get; set; }

        public string? Descricao { get; set; }

        public string? LogoUrl { get; set; }

        public string? Site { get; set; }

        public string? Observacao { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime CriadoEm { get; set; }
        public DateTime? AtualizadoEm { get; set; }

        public ICollection<CadastroItens> Produtos { get; set; } = [];
    }
}
