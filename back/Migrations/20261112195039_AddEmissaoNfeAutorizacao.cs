using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmissaoNfeAutorizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AutorizadaEm",
                table: "NotasFiscais",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoAutorizacao",
                table: "NotasFiscais",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HashSolicitacao",
                table: "NotasFiscais",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MotivoAutorizacao",
                table: "NotasFiscais",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Recibo",
                table: "NotasFiscais",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SolicitacaoId",
                table: "NotasFiscais",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioEmissorId",
                table: "NotasFiscais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "XmlEnvioNfe",
                table: "NotasFiscais",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "XmlRetornoAutorizacao",
                table: "NotasFiscais",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "NotaFiscalItemId",
                table: "Estoques",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoMunicipio",
                table: "ConfiguracoesFiscais",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SeriePadrao",
                table: "ConfiguracoesFiscais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UnidadeComercial",
                table: "CadastroItens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "NotaFiscalItens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotaFiscalId = table.Column<long>(type: "bigint", nullable: false),
                    ProdutoId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(15,4)", precision: 15, scale: 4, nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "numeric(21,10)", precision: 21, scale: 10, nullable: false),
                    CustoUnitario = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaFiscalItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotaFiscalItens_CadastroItens_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "CadastroItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotaFiscalItens_NotasFiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "NotasFiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NumeracoesNfe",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Ambiente = table.Column<int>(type: "integer", nullable: false),
                    Serie = table.Column<int>(type: "integer", nullable: false),
                    ProximoNumero = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumeracoesNfe", x => new { x.EmpresaId, x.Ambiente, x.Serie });
                    table.ForeignKey(
                        name: "FK_NumeracoesNfe_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_EmpresaId_Ambiente_Tipo_Serie_Numero",
                table: "NotasFiscais",
                columns: new[] { "EmpresaId", "Ambiente", "Tipo", "Serie", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_EmpresaId_SolicitacaoId",
                table: "NotasFiscais",
                columns: new[] { "EmpresaId", "SolicitacaoId" },
                unique: true,
                filter: "\"SolicitacaoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotaFiscalEventos_NotaFiscalId",
                table: "NotaFiscalEventos",
                column: "NotaFiscalId",
                unique: true,
                filter: "\"Situacao\" IN ('Pendente', 'Indeterminado')");

            migrationBuilder.CreateIndex(
                name: "IX_Estoques_NotaFiscalItemId_Tipo",
                table: "Estoques",
                columns: new[] { "NotaFiscalItemId", "Tipo" },
                unique: true,
                filter: "\"NotaFiscalItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotaFiscalItens_NotaFiscalId",
                table: "NotaFiscalItens",
                column: "NotaFiscalId");

            migrationBuilder.CreateIndex(
                name: "IX_NotaFiscalItens_ProdutoId",
                table: "NotaFiscalItens",
                column: "ProdutoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Estoques_NotaFiscalItens_NotaFiscalItemId",
                table: "Estoques",
                column: "NotaFiscalItemId",
                principalTable: "NotaFiscalItens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Estoques_NotaFiscalItens_NotaFiscalItemId",
                table: "Estoques");

            migrationBuilder.DropTable(
                name: "NotaFiscalItens");

            migrationBuilder.DropTable(
                name: "NumeracoesNfe");

            migrationBuilder.DropIndex(
                name: "IX_NotasFiscais_EmpresaId_Ambiente_Tipo_Serie_Numero",
                table: "NotasFiscais");

            migrationBuilder.DropIndex(
                name: "IX_NotasFiscais_EmpresaId_SolicitacaoId",
                table: "NotasFiscais");

            migrationBuilder.DropIndex(
                name: "IX_NotaFiscalEventos_NotaFiscalId",
                table: "NotaFiscalEventos");

            migrationBuilder.DropIndex(
                name: "IX_Estoques_NotaFiscalItemId_Tipo",
                table: "Estoques");

            migrationBuilder.DropColumn(
                name: "AutorizadaEm",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "CodigoAutorizacao",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "HashSolicitacao",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "MotivoAutorizacao",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "Recibo",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "SolicitacaoId",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "UsuarioEmissorId",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "XmlEnvioNfe",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "XmlRetornoAutorizacao",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "NotaFiscalItemId",
                table: "Estoques");

            migrationBuilder.DropColumn(
                name: "CodigoMunicipio",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "SeriePadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "UnidadeComercial",
                table: "CadastroItens");
        }
    }
}
