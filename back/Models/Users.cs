namespace back.Models
{
    public class Users
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmailNormalizado { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public PerfilUsuario Perfil { get; set; }
        public bool Ativo { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime? AtualizadoEm { get; set; }
        public DateTime? UltimoAcessoEm { get; set; }

        public Empresas? Empresa { get; set; }
    }
}
