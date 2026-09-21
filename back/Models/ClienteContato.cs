namespace back.Models
{
    public class ClienteContato
    {
        public long Id { get; set; }
        public long ClienteId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Cargo { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? Celular { get; set; }
        public bool Principal { get; set; }

        public Cliente Cliente { get; set; } = null!;
    }
}
