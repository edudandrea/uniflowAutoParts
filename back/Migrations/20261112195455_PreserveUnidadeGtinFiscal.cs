using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniflowAutoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class PreserveUnidadeGtinFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UnidadeComercial",
                table: "CadastroItens",
                type: "text",
                nullable: false,
                defaultValue: "UN",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Ean",
                table: "CadastroItens",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "CadastroItens" SET "UnidadeComercial" = 'UN' WHERE "UnidadeComercial" = '';
                UPDATE "CadastroItens" p
                SET "UnidadeComercial" = source."UnidadeComercial"
                FROM (
                    SELECT DISTINCT ON (i."ProdutoId", e."TenantId") i."ProdutoId", e."TenantId", i."UnidadeComercial"
                    FROM "EntradaCompraItens" i JOIN "EntradasCompra" e ON e."Id" = i."EntradaCompraId"
                    WHERE i."ProdutoId" IS NOT NULL AND length(i."UnidadeComercial") BETWEEN 1 AND 6
                    ORDER BY i."ProdutoId", e."TenantId", e."ImportadoEm" DESC, i."Id" DESC
                ) source WHERE p."Id" = source."ProdutoId" AND p."EmpresaId" = source."TenantId";
                UPDATE "CadastroItens" p SET "Ean" = source."Ean"
                FROM (
                    SELECT DISTINCT ON (i."ProdutoId", e."TenantId") i."ProdutoId", e."TenantId", i."Ean"
                    FROM "EntradaCompraItens" i JOIN "EntradasCompra" e ON e."Id" = i."EntradaCompraId"
                    WHERE i."ProdutoId" IS NOT NULL AND i."Ean" ~ '^[0-9]{8}$|^[0-9]{12,14}$' AND i."Ean" !~ '^0+$'
                    ORDER BY i."ProdutoId", e."TenantId", e."ImportadoEm" DESC, i."Id" DESC
                ) source WHERE p."Id" = source."ProdutoId" AND p."EmpresaId" = source."TenantId" AND p."Ean" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ean",
                table: "CadastroItens");

            migrationBuilder.AlterColumn<string>(
                name: "UnidadeComercial",
                table: "CadastroItens",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "UN");
        }
    }
}
