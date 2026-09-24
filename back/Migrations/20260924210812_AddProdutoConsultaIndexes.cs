using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProdutoConsultaIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CadastroItens_EmpresaId_Ativo",
                table: "CadastroItens",
                columns: new[] { "EmpresaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_CadastroItens_EmpresaId_Descricao",
                table: "CadastroItens",
                columns: new[] { "EmpresaId", "Descricao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CadastroItens_EmpresaId_Ativo",
                table: "CadastroItens");

            migrationBuilder.DropIndex(
                name: "IX_CadastroItens_EmpresaId_Descricao",
                table: "CadastroItens");
        }
    }
}
