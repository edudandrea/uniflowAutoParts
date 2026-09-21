namespace back.Models
{
    public class Categoria
    {
        public long Id { get; set; }
        public long TenantId { get; set; }

        public string Nome { get; set; } = null!;
        public string? Descricao { get; set; }

        public long? CategoriaPaiId { get; set; }
        public Categoria? CategoriaPai { get; set; }

        public string? Icone { get; set; }

        public int Ordem { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime CriadoEm { get; set; }
        public DateTime? AtualizadoEm { get; set; }

        public ICollection<Categoria> Subcategorias { get; set; } = [];
        public ICollection<CadastroItens> Produtos { get; set; } = [];
    }
}
