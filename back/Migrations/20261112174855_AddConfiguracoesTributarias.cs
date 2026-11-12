using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracoesTributarias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesTributarias",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Regime = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    IcmsCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PisCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CofinsCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IcmsRate = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    PisRate = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    CofinsRate = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesTributarias", x => x.EmpresaId);
                    table.ForeignKey(
                        name: "FK_ConfiguracoesTributarias_Empresas_EmpresaId",
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
                name: "ConfiguracoesTributarias");
        }
    }
}
