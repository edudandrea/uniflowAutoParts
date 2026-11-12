using System.Security.Claims;
using back.Contracts;
using back.Controllers;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal static class HubCompanyProfileChecks
{
    public static async Task RunAsync()
    {
        var company = new Empresas { Id = 1, HubTenantId = "central-7", Bairro = "Bairro preenchido localmente" };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("tenant_name", "Nome fantasia HUB"), new Claim("tenant_razao_social", "Razao social HUB"),
            new Claim("tenant_cnpj", "12.345.678/0001-95"), new Claim("tenant_inscricao_estadual", "1234567890"),
            new Claim("tenant_logradouro", "Rua do HUB"), new Claim("tenant_numero", "123"), new Claim("tenant_complemento", "Sala 2"),
            new Claim("tenant_municipio", "Porto Alegre"), new Claim("tenant_uf", "rs"), new Claim("tenant_cep", "90010-000"), new Claim("tenant_bairro", " ")
        }, "fixture"));
        HubCompanyProfile.Aplicar(company, principal);
        if (company.RazaoSocial != "Razao social HUB" || company.NomeFantasia != "Nome fantasia HUB" || company.Cnpj != "12345678000195"
            || company.Endereco != "Rua do HUB" || company.Numero != "123" || company.Complemento != "Sala 2" || company.Estado != "RS" || company.Cep != "90010000"
            || company.Cidade != "Porto Alegre" || company.InscricaoEstadual != "1234567890" || company.HubTenantId != "central-7" || company.Bairro != "Bairro preenchido localmente")
            throw new Exception("Dados fiscais do HUB nao foram mapeados corretamente.");
        HubCompanyProfile.Aplicar(company, new ClaimsPrincipal(new ClaimsIdentity()));
        if (company.Cnpj != "12345678000195") throw new Exception("Claims ausentes apagaram dados locais.");

        var options = await NfeTestDatabase.CreateAsync(1, 2);
        var tenant = new TenantContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(1) } });
        await using var db = new AppDbContext(options, tenant);
        var existing = await db.Empresas.SingleAsync(); existing.Cnpj = null; await db.SaveChangesAsync();
        var controller = new EmitenteFiscalController(db, tenant);
        var request = new EmitenteFiscalRequest("Empresa do HUB", "1234567890", "Rua do HUB", "123", "Sala 2", "Centro", "Porto Alegre", "4314902", "RS", "90010000", 1, 1, "12.345.678/0001-95");
        if (await controller.Salvar(request, default) is not OkObjectResult { Value: EmitenteFiscalResponse { Cnpj: "12345678000195" } })
            throw new Exception("Nao permitiu completar CNPJ ausente no emitente.");
        if ((await db.Empresas.SingleAsync()).Cnpj != "12345678000195") throw new Exception("CNPJ nao foi persistido.");
        if (await controller.Salvar(request with { Cnpj = "123" }, default) is not BadRequestObjectResult) throw new Exception("Aceitou CNPJ incompleto.");
        Console.WriteLine("HUB company fiscal profile and editable CNPJ checks passed. Sem acesso ao HUB real.");
    }
}
