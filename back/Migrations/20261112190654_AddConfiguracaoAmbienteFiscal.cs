using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracaoAmbienteFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesFiscais",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Ambiente = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesFiscais", x => x.EmpresaId);
                    table.CheckConstraint("CK_ConfiguracoesFiscais_Ambiente", "\"Ambiente\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ConfiguracoesFiscais_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesFiscais");
        }
    }
}
