using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CadastroEnderecoId",
                table: "Clientes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CadastroEnderecos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Logradouro = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Numero = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Complemento = table.Column<string>(type: "text", nullable: true),
                    Bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CodigoIbgeMunicipio = table.Column<string>(type: "text", nullable: false),
                    Municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Pais = table.Column<string>(type: "text", nullable: false),
                    Referencia = table.Column<string>(type: "text", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CadastroEnderecos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CadastroEnderecos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CadastroEnderecoId",
                table: "Clientes",
                column: "CadastroEnderecoId");

            migrationBuilder.CreateIndex(
                name: "IX_CadastroEnderecos_EmpresaId",
                table: "CadastroEnderecos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_CadastroEnderecos_EmpresaId_Cep_Logradouro",
                table: "CadastroEnderecos",
                columns: new[] { "EmpresaId", "Cep", "Logradouro" });

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_CadastroEnderecos_CadastroEnderecoId",
                table: "Clientes",
                column: "CadastroEnderecoId",
                principalTable: "CadastroEnderecos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_CadastroEnderecos_CadastroEnderecoId",
                table: "Clientes");

            migrationBuilder.DropTable(
                name: "CadastroEnderecos");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_CadastroEnderecoId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CadastroEnderecoId",
                table: "Clientes");
        }
    }
}
