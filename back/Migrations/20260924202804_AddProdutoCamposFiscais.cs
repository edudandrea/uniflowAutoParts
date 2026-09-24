using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProdutoCamposFiscais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaIcms",
                table: "CadastroItens",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaIpi",
                table: "CadastroItens",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaMva",
                table: "CadastroItens",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ImpostosFabricante",
                table: "CadastroItens",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AliquotaIcms",
                table: "CadastroItens");

            migrationBuilder.DropColumn(
                name: "AliquotaIpi",
                table: "CadastroItens");

            migrationBuilder.DropColumn(
                name: "AliquotaMva",
                table: "CadastroItens");

            migrationBuilder.DropColumn(
                name: "ImpostosFabricante",
                table: "CadastroItens");
        }
    }
}
