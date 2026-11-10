using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNfeStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantidade",
                table: "Estoques",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "EntradaCompraItemId",
                table: "Estoques",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VersaoEstoque",
                table: "EntradasCompra",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Estoques_EntradaCompraItemId",
                table: "Estoques",
                column: "EntradaCompraItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Estoques_ItemId",
                table: "Estoques",
                column: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Estoques_CadastroItens_ItemId",
                table: "Estoques",
                column: "ItemId",
                principalTable: "CadastroItens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Estoques_EntradaCompraItens_EntradaCompraItemId",
                table: "Estoques",
                column: "EntradaCompraItemId",
                principalTable: "EntradaCompraItens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Estoques_CadastroItens_ItemId",
                table: "Estoques");

            migrationBuilder.DropForeignKey(
                name: "FK_Estoques_EntradaCompraItens_EntradaCompraItemId",
                table: "Estoques");

            migrationBuilder.DropIndex(
                name: "IX_Estoques_EntradaCompraItemId",
                table: "Estoques");

            migrationBuilder.DropIndex(
                name: "IX_Estoques_ItemId",
                table: "Estoques");

            migrationBuilder.DropColumn(
                name: "EntradaCompraItemId",
                table: "Estoques");

            migrationBuilder.DropColumn(
                name: "VersaoEstoque",
                table: "EntradasCompra");

            migrationBuilder.AlterColumn<int>(
                name: "Quantidade",
                table: "Estoques",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}
