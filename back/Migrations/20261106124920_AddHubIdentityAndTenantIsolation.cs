using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHubIdentityAndTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EmailNormalizado",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "HubSubject",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Cnpj",
                table: "Empresas",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "HubIssuer",
                table: "Empresas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HubTenantId",
                table: "Empresas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailNormalizado",
                table: "Users",
                column: "EmailNormalizado");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmpresaId_HubSubject",
                table: "Users",
                columns: new[] { "EmpresaId", "HubSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_HubIssuer_HubTenantId",
                table: "Empresas",
                columns: new[] { "HubIssuer", "HubTenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EmailNormalizado",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmpresaId_HubSubject",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Empresas_HubIssuer_HubTenantId",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "HubSubject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HubIssuer",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "HubTenantId",
                table: "Empresas");

            migrationBuilder.AlterColumn<string>(
                name: "Cnpj",
                table: "Empresas",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailNormalizado",
                table: "Users",
                column: "EmailNormalizado",
                unique: true);
        }
    }
}
