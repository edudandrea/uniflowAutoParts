using back.Models;
using back.Services;
using Microsoft.EntityFrameworkCore;

namespace back.Data
{
    public partial class AppDbContext : DbContext
    {
        private readonly TenantContext? tenant;
        private int CurrentEmpresaId => tenant?.EmpresaId ?? 0;
        internal int CurrentUsuarioId => tenant?.UsuarioId ?? 0;

        public AppDbContext(DbContextOptions<AppDbContext> options, TenantContext? tenant = null) : base(options)
        {
            this.tenant = tenant;
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
        public DbSet<EntradaCompra> EntradasCompra => Set<EntradaCompra>();
        public DbSet<EntradaCompraItem> EntradaCompraItens => Set<EntradaCompraItem>();
        public DbSet<Users> Users => Set<Users>();
        public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
        public DbSet<NotaFiscalEvento> NotaFiscalEventos => Set<NotaFiscalEvento>();
        public DbSet<ConfiguracaoTributaria> ConfiguracoesTributarias => Set<ConfiguracaoTributaria>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ConfiguracaoTributaria>(entity =>
            {
                entity.HasKey(e => e.EmpresaId);
                entity.Property(e => e.EmpresaId).ValueGeneratedNever();
                entity.HasQueryFilter(e => e.EmpresaId == CurrentEmpresaId);
                entity.Property(e => e.Regime).HasMaxLength(1);
                entity.Property(e => e.IcmsCode).HasMaxLength(3);
                entity.Property(e => e.PisCode).HasMaxLength(2);
                entity.Property(e => e.CofinsCode).HasMaxLength(2);
                entity.Property(e => e.IcmsRate).HasPrecision(7, 4);
                entity.Property(e => e.PisRate).HasPrecision(7, 4);
                entity.Property(e => e.CofinsRate).HasPrecision(7, 4);
                entity.HasOne<Empresas>().WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Empresas>().HasQueryFilter(e => e.Id == CurrentEmpresaId);
            modelBuilder.Entity<Users>().HasQueryFilter(e => e.EmpresaId == CurrentEmpresaId);
            modelBuilder.Entity<Cliente>().HasQueryFilter(e => e.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<CadastroEndereco>().HasQueryFilter(e => e.EmpresaId == CurrentEmpresaId);
            modelBuilder.Entity<Categoria>().HasQueryFilter(e => e.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<Marca>().HasQueryFilter(e => e.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<CadastroItens>().HasQueryFilter(e => e.EmpresaId == CurrentEmpresaId);
            modelBuilder.Entity<Estoque>().HasQueryFilter(e => e.EmpresaId == CurrentEmpresaId);
            modelBuilder.Entity<EntradaCompra>().HasQueryFilter(e => e.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<NotaFiscal>().HasQueryFilter(e => e.EmpresaId == CurrentEmpresaId);
            modelBuilder.Entity<Endereco>().HasQueryFilter(e => e.Cliente != null && e.Cliente.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<ClienteContato>().HasQueryFilter(e => e.Cliente.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<ClienteDadosComerciais>().HasQueryFilter(e => e.Cliente.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<ClienteVeiculo>().HasQueryFilter(e => e.Cliente != null && e.Cliente.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<EntradaCompraItem>().HasQueryFilter(e => e.EntradaCompra != null && e.EntradaCompra.TenantId == CurrentEmpresaId);
            modelBuilder.Entity<NotaFiscalEvento>().HasQueryFilter(e => e.NotaFiscal.EmpresaId == CurrentEmpresaId);

            modelBuilder.Entity<NotaFiscal>(entity =>
            {
                entity.HasIndex(e => new { e.EmpresaId, e.ChaveAcesso, e.Ambiente }).IsUnique();
                entity.HasIndex(e => new { e.EmpresaId, e.Emissao });
                entity.Property(e => e.Valor).HasPrecision(18, 2);
                entity.Property(e => e.ChaveAcesso).HasMaxLength(44);
                entity.HasOne<Empresas>().WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(e => e.Eventos).WithOne(e => e.NotaFiscal)
                    .HasForeignKey(e => e.NotaFiscalId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<NotaFiscalEvento>(entity =>
            {
                entity.HasIndex(e => new { e.NotaFiscalId, e.Tipo, e.Sequencia });
            });

            modelBuilder.Entity<Empresas>(entity =>
            {
                entity.Property(e => e.HubIssuer).HasMaxLength(500);
                entity.Property(e => e.HubTenantId).HasMaxLength(200);
                entity.HasIndex(e => new { e.HubIssuer, e.HubTenantId }).IsUnique();
                entity.HasIndex(e => e.Cnpj).IsUnique();

                entity.HasMany<Users>()
                    .WithOne(e => e.Empresa)
                    .HasForeignKey(e => e.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Users>(entity =>
            {
                entity.HasIndex(e => e.EmailNormalizado);
                entity.Property(e => e.HubSubject).HasMaxLength(200);
                entity.HasIndex(e => new { e.EmpresaId, e.HubSubject }).IsUnique();
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
                entity.HasIndex(e => new { e.EmpresaId, e.Descricao });
                entity.HasIndex(e => new { e.EmpresaId, e.Ativo });
                entity.HasIndex(e => e.CategoriaId);
                entity.HasIndex(e => e.MarcaId);
                entity.Property(e => e.AliquotaIpi).HasPrecision(9, 4);
                entity.Property(e => e.AliquotaIcms).HasPrecision(9, 4);
                entity.Property(e => e.AliquotaMva).HasPrecision(9, 4);
                entity.Property(e => e.ImpostosFabricante).HasMaxLength(80);

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
                entity.Property(e => e.Quantidade).HasPrecision(18, 4);
                entity.HasIndex(e => e.EntradaCompraItemId).IsUnique();
                entity.HasOne(e => e.EntradaCompraItem).WithOne().HasForeignKey<Estoque>(e => e.EntradaCompraItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Produto).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EntradaCompra>(entity =>
            {
                entity.Property(e => e.VersaoEstoque).IsConcurrencyToken();
                entity.HasIndex(e => new { e.TenantId, e.ChaveNfe }).IsUnique();
                entity.HasIndex(e => e.FornecedorId);
                entity.Property(e => e.ChaveNfe).HasMaxLength(44);
                entity.Property(e => e.NumeroNfe).HasMaxLength(20);
                entity.Property(e => e.Serie).HasMaxLength(10);
                entity.Property(e => e.Modelo).HasMaxLength(10);
                entity.Property(e => e.TipoOperacao).HasMaxLength(10);
                entity.Property(e => e.Finalidade).HasMaxLength(10);
                entity.Property(e => e.NaturezaOperacao).HasMaxLength(180);
                entity.Property(e => e.Status).HasMaxLength(40);
                entity.Property(e => e.ValorProdutos).HasPrecision(18, 2);
                entity.Property(e => e.ValorFrete).HasPrecision(18, 2);
                entity.Property(e => e.ValorSeguro).HasPrecision(18, 2);
                entity.Property(e => e.ValorDesconto).HasPrecision(18, 2);
                entity.Property(e => e.ValorOutrasDespesas).HasPrecision(18, 2);
                entity.Property(e => e.ValorTotal).HasPrecision(18, 2);

                entity.HasOne(e => e.Fornecedor)
                    .WithMany()
                    .HasForeignKey(e => e.FornecedorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<EntradaCompraItem>(entity =>
            {
                entity.HasIndex(e => e.EntradaCompraId);
                entity.HasIndex(e => e.ProdutoId);
                entity.Property(e => e.CodigoFornecedor).HasMaxLength(80);
                entity.Property(e => e.Ean).HasMaxLength(32);
                entity.Property(e => e.DescricaoXml).HasMaxLength(500);
                entity.Property(e => e.Ncm).HasMaxLength(16);
                entity.Property(e => e.Cest).HasMaxLength(16);
                entity.Property(e => e.Cfop).HasMaxLength(8);
                entity.Property(e => e.UnidadeComercial).HasMaxLength(12);
                entity.Property(e => e.UnidadeTributavel).HasMaxLength(12);
                entity.Property(e => e.PedidoCompra).HasMaxLength(80);
                entity.Property(e => e.ItemPedido).HasMaxLength(40);
                entity.Property(e => e.QuantidadeComercial).HasPrecision(18, 4);
                entity.Property(e => e.ValorUnitarioComercial).HasPrecision(18, 6);
                entity.Property(e => e.QuantidadeTributavel).HasPrecision(18, 4);
                entity.Property(e => e.ValorUnitarioTributavel).HasPrecision(18, 6);
                entity.Property(e => e.ValorProduto).HasPrecision(18, 2);
                entity.Property(e => e.ValorFrete).HasPrecision(18, 2);
                entity.Property(e => e.ValorSeguro).HasPrecision(18, 2);
                entity.Property(e => e.ValorDesconto).HasPrecision(18, 2);
                entity.Property(e => e.ValorOutrasDespesas).HasPrecision(18, 2);
                entity.Property(e => e.QuantidadeRecebida).HasPrecision(18, 4);
                entity.Property(e => e.CustoUnitario).HasPrecision(18, 6);

                entity.HasOne(e => e.EntradaCompra)
                    .WithMany(e => e.Itens)
                    .HasForeignKey(e => e.EntradaCompraId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Produto)
                    .WithMany()
                    .HasForeignKey(e => e.ProdutoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
