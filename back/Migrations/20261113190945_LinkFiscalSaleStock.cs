using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkFiscalSaleStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "VendaOrigemId",
                table: "NotasFiscais",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_EmpresaId_VendaOrigemId",
                table: "NotasFiscais",
                columns: new[] { "EmpresaId", "VendaOrigemId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_VendaOrigemId",
                table: "NotasFiscais",
                column: "VendaOrigemId");

            migrationBuilder.AddForeignKey(
                name: "FK_NotasFiscais_NotasFiscais_VendaOrigemId",
                table: "NotasFiscais",
                column: "VendaOrigemId",
                principalTable: "NotasFiscais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotasFiscais_NotasFiscais_VendaOrigemId",
                table: "NotasFiscais");

            migrationBuilder.DropIndex(
                name: "IX_NotasFiscais_EmpresaId_VendaOrigemId",
                table: "NotasFiscais");

            migrationBuilder.DropIndex(
                name: "IX_NotasFiscais_VendaOrigemId",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "VendaOrigemId",
                table: "NotasFiscais");
        }
    }
}
