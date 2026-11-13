using back.Contracts;
using back.Controllers;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal static class CustomerFiscalAddressChecks
{
    public static async Task RunAsync()
    {
        var options = await NfeTestDatabase.CreateAsync(1, 2);
        TenantContext Tenant(int id) => new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(id) } });
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var linked = NfeFixtures.Cliente(11);
            linked.CadastroEndereco!.Logradouro = "Rua vinculada no cadastro";
            linked.CadastroEndereco.Numero = "1450"; linked.CadastroEndereco.Complemento = "Sala 2";
            linked.Enderecos.Add(new Endereco { Logradouro = "Endereco antigo", Numero = "1", Bairro = "Centro", Municipio = "Porto Alegre", CodigoIbgeMunicipio = "4314902", Uf = "RS", Cep = "90000000", Principal = true });
            var legacy = NfeFixtures.Cliente(12); legacy.CadastroEndereco = null;
            legacy.Enderecos.Add(new Endereco { Logradouro = "Endereco secundario", Numero = "2", Bairro = "Centro", Municipio = "Porto Alegre", CodigoIbgeMunicipio = "4314902", Uf = "RS", Cep = "90000000", Principal = false });
            legacy.Enderecos.Add(new Endereco { Logradouro = "Endereco principal", Numero = "3", Bairro = "Centro", Municipio = "Porto Alegre", CodigoIbgeMunicipio = "4314902", Uf = "RS", Cep = "90000000", Principal = true });
            db.Clientes.AddRange(linked, legacy); await db.SaveChangesAsync();
        }
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var controller = new EmitenteFiscalController(db, Tenant(1));
            if (await controller.Destinatario(11, default) is not OkObjectResult { Value: DestinatarioFiscalRequest { Logradouro: "Rua vinculada no cadastro", Numero: "1450", Complemento: "Sala 2", CodigoMunicipio: "4314902", Cep: "90000000" } })
                throw new Exception("Endereco fiscal nao corresponde ao endereco vinculado no cadastro do cliente.");
            if (await controller.Destinatario(12, default) is not OkObjectResult { Value: DestinatarioFiscalRequest { Logradouro: "Endereco principal", Numero: "3" } })
                throw new Exception("Cadastro antigo nao carregou endereco principal.");
        }
        await using (var db = new AppDbContext(options, Tenant(2)))
        {
            if (await new EmitenteFiscalController(db, Tenant(2)).Destinatario(11, default) is not NotFoundResult)
                throw new Exception("Endereco do cliente vazou para outra empresa.");
        }
        Console.WriteLine("Customer fiscal address checks: linked registry, legacy primary address and tenant isolation passed.");
    }
}
