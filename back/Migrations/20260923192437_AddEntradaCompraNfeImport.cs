using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEntradaCompraNfeImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntradasCompra",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    FornecedorId = table.Column<long>(type: "bigint", nullable: true),
                    ChaveNfe = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    NumeroNfe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Serie = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Modelo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataEntrada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TipoOperacao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Finalidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NaturezaOperacao = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ValorProdutos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorFrete = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorSeguro = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorOutrasDespesas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    XmlOriginal = table.Column<string>(type: "text", nullable: false),
                    ImportadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntradasCompra_Clientes_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EntradaCompraItens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntradaCompraId = table.Column<long>(type: "bigint", nullable: false),
                    ProdutoId = table.Column<int>(type: "integer", nullable: true),
                    NumeroItem = table.Column<int>(type: "integer", nullable: false),
                    CodigoFornecedor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Ean = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DescricaoXml = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Ncm = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Cest = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Cfop = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    UnidadeComercial = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    QuantidadeComercial = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ValorUnitarioComercial = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnidadeTributavel = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    QuantidadeTributavel = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ValorUnitarioTributavel = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ValorProduto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorFrete = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorSeguro = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorOutrasDespesas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeRecebida = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PedidoCompra = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ItemPedido = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TributacaoXml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradaCompraItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntradaCompraItens_CadastroItens_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "CadastroItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EntradaCompraItens_EntradasCompra_EntradaCompraId",
                        column: x => x.EntradaCompraId,
                        principalTable: "EntradasCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntradaCompraItens_EntradaCompraId",
                table: "EntradaCompraItens",
                column: "EntradaCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_EntradaCompraItens_ProdutoId",
                table: "EntradaCompraItens",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasCompra_FornecedorId",
                table: "EntradasCompra",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasCompra_TenantId_ChaveNfe",
                table: "EntradasCompra",
                columns: new[] { "TenantId", "ChaveNfe" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntradaCompraItens");

            migrationBuilder.DropTable(
                name: "EntradasCompra");
        }
    }
}
