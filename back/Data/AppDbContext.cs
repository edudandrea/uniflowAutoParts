using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Empresas> Empresas => Set<Empresas>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<ClienteContato> ClienteContatos => Set<ClienteContato>();
        public DbSet<ClienteDadosComerciais> ClienteDadosComerciais => Set<ClienteDadosComerciais>();
        public DbSet<ClienteVeiculo> ClienteVeiculos => Set<ClienteVeiculo>();
        public DbSet<Endereco> Enderecos => Set<Endereco>();
        public DbSet<CadastroEndereco> CadastroEnderecos => Set<CadastroEndereco>();
        public DbSet<Categoria> Categorias => Set<Categoria>();
        public DbSet<Marca> Marcas => Set<Marca>();
        public DbSet<CadastroItens> CadastroItens => Set<CadastroItens>();
        public DbSet<Estoque> Estoques => Set<Estoque>();
        public DbSet<Users> Users => Set<Users>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Empresas>(entity =>
            {
                entity.HasIndex(e => e.Cnpj).IsUnique();

                entity.HasMany<Users>()
                    .WithOne(e => e.Empresa)
                    .HasForeignKey(e => e.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Users>(entity =>
            {
                entity.HasIndex(e => e.EmailNormalizado).IsUnique();
                entity.HasIndex(e => e.EmpresaId);

                entity.Property(e => e.Nome).HasMaxLength(160);
                entity.Property(e => e.Email).HasMaxLength(180);
                entity.Property(e => e.EmailNormalizado).HasMaxLength(180);
                entity.Property(e => e.PasswordHash).HasMaxLength(512);
            });

            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasIndex(e => new { e.TenantId, e.CpfCnpj }).IsUnique();
                entity.Property(e => e.LimiteCredito).HasPrecision(18, 2);
                entity.Property(e => e.NomeRazaoSocial).HasMaxLength(180);
                entity.Property(e => e.NomeFantasia).HasMaxLength(180);
                entity.Property(e => e.CpfCnpj).HasMaxLength(18);
                entity.Property(e => e.Email).HasMaxLength(180);
                entity.HasIndex(e => e.CadastroEnderecoId);

                entity.HasOne(e => e.CadastroEndereco)
                    .WithMany(e => e.Clientes)
                    .HasForeignKey(e => e.CadastroEnderecoId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.Enderecos)
                    .WithOne(e => e.Cliente)
                    .HasForeignKey(e => e.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Veiculos)
                    .WithOne(v => v.Cliente)
                    .HasForeignKey(v => v.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Contatos)
                    .WithOne(e => e.Cliente)
                    .HasForeignKey(e => e.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DadosComerciais)
                    .WithOne(e => e.Cliente)
                    .HasForeignKey<ClienteDadosComerciais>(e => e.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ClienteContato>(entity =>
            {
                entity.HasIndex(e => e.ClienteId);
                entity.Property(e => e.Nome).HasMaxLength(160);
                entity.Property(e => e.Email).HasMaxLength(180);
            });

            modelBuilder.Entity<ClienteDadosComerciais>(entity =>
            {
                entity.HasIndex(e => e.ClienteId).IsUnique();
                entity.Property(e => e.LimiteCredito).HasPrecision(18, 2);
            });

            modelBuilder.Entity<ClienteVeiculo>(entity =>
            {
                entity.HasIndex(e => e.Placa);
            });

            modelBuilder.Entity<Endereco>(entity =>
            {
                entity.HasIndex(e => e.ClienteId);
            });

            modelBuilder.Entity<CadastroEndereco>(entity =>
            {
                entity.HasIndex(e => e.EmpresaId);
                entity.HasIndex(e => new { e.EmpresaId, e.Cep, e.Logradouro });
                entity.Property(e => e.Nome).HasMaxLength(140);
                entity.Property(e => e.Cep).HasMaxLength(8);
                entity.Property(e => e.Logradouro).HasMaxLength(180);
                entity.Property(e => e.Numero).HasMaxLength(24);
                entity.Property(e => e.Bairro).HasMaxLength(120);
                entity.Property(e => e.Municipio).HasMaxLength(120);
                entity.Property(e => e.Uf).HasMaxLength(2);

                entity.HasOne(e => e.Empresa)
                    .WithMany()
                    .HasForeignKey(e => e.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CadastroItens>(entity =>
            {
                entity.HasIndex(e => new { e.EmpresaId, e.SKU }).IsUnique();
                entity.HasIndex(e => e.CategoriaId);
                entity.HasIndex(e => e.MarcaId);

                entity.HasOne(e => e.Categoria)
                    .WithMany(e => e.Produtos)
                    .HasForeignKey(e => e.CategoriaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Marca)
                    .WithMany(e => e.Produtos)
                    .HasForeignKey(e => e.MarcaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.TenantId, e.Nome, e.CategoriaPaiId }).IsUnique();
                entity.Property(e => e.Nome).HasMaxLength(120);
                entity.Property(e => e.Descricao).HasMaxLength(500);
                entity.Property(e => e.Icone).HasMaxLength(48);

                entity.HasOne(e => e.CategoriaPai)
                    .WithMany(e => e.Subcategorias)
                    .HasForeignKey(e => e.CategoriaPaiId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Marca>(entity =>
            {
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.TenantId, e.Nome }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.Codigo }).IsUnique();
                entity.Property(e => e.Nome).HasMaxLength(120);
                entity.Property(e => e.Codigo).HasMaxLength(40);
                entity.Property(e => e.Descricao).HasMaxLength(500);
                entity.Property(e => e.LogoUrl).HasMaxLength(500);
                entity.Property(e => e.Site).HasMaxLength(240);
                entity.Property(e => e.Observacao).HasMaxLength(500);
            });

            modelBuilder.Entity<Estoque>(entity =>
            {
                entity.HasIndex(e => new { e.EmpresaId, e.ItemId });
            });
        }
    }
}
