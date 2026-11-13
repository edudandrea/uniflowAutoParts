using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class GlobalStockPriceMargin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MargemPrecoEstoque",
                table: "Empresas",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Empresas_MargemPrecoEstoque",
                table: "Empresas",
                sql: "\"MargemPrecoEstoque\" IS NULL OR \"MargemPrecoEstoque\" BETWEEN 0 AND 99999.9999");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Empresas_MargemPrecoEstoque",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "MargemPrecoEstoque",
                table: "Empresas");
        }
    }
}
