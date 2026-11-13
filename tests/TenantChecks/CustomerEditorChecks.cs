using back.Contracts;
using back.Controllers;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal static class CustomerEditorChecks
{
    public static async Task RunAsync()
    {
        var options = await NfeTestDatabase.CreateAsync(1, 2);
        TenantContext Tenant(int id) => new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(id) } });
        var checks = 0;
        void Check(bool condition, string error) { if (!condition) throw new Exception(error); checks++; }
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var customer = NfeFixtures.Cliente(7); customer.TipoPessoa = TipoPessoa.juridica;
            customer.InscricaoEstadual = "0290000000"; customer.CriadoEm = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
            customer.Contatos.Add(new ClienteContato { Nome = "Contato principal", Cargo = "Gerente", Principal = true });
            customer.Contatos.Add(new ClienteContato { Nome = "Contato adicional", Email = "outro@example.test" });
            customer.DadosComerciais = new ClienteDadosComerciais { LimiteCredito = 1500, CondicaoPagamento = "30 dias", PermiteFiado = true };
            db.Clientes.Add(customer);
            var duplicate = NfeFixtures.Cliente(8); duplicate.CpfCnpj = "11444777000161"; duplicate.TipoPessoa = TipoPessoa.juridica; db.Clientes.Add(duplicate);
            await db.SaveChangesAsync();
        }
        await using (var db = new AppDbContext(options, Tenant(2)))
        {
            db.CadastroEnderecos.Add(new CadastroEndereco { Id = 700, EmpresaId = 2, Nome = "Endereco de outra empresa", Cep = "90000000", Logradouro = "Rua estrangeira", Numero = "1", Bairro = "Centro", Municipio = "Porto Alegre", CodigoIbgeMunicipio = "4314902", Uf = "RS" });
            await db.SaveChangesAsync();
        }
        CriarClienteRequest request;
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var controller = new ClientesController(db);
            var result = await controller.Obter(7, 1);
            var detail = (result.Result as OkObjectResult)?.Value as ClienteDetalheResponse ?? throw new Exception("Nao retornou cadastro completo.");
            Check(detail.Dados.InscricaoEstadual == "0290000000" && detail.Dados.Contatos?.Count == 2 && detail.Dados.DadosComerciais?.LimiteCredito == 1500 && detail.Dados.Endereco?.CodigoIbgeMunicipio == "4314902", "Consulta perdeu dados do cadastro.");
            request = detail.Dados with { NomeRazaoSocial = "Cliente atualizado", InscricaoEstadual = "0290123456", Email = "atualizado@example.test", ConsumidorFinal = false };
            Check(await controller.Atualizar(7, request) is OkObjectResult, "Edicao do cliente falhou.");
            var edited = await db.Clientes.Include(c => c.Contatos).Include(c => c.DadosComerciais).SingleAsync(c => c.Id == 7);
            Check(edited.NomeRazaoSocial == "Cliente atualizado" && edited.InscricaoEstadual == "0290123456" && edited.Email == "atualizado@example.test" && !edited.ConsumidorFinal, "Edicao nao persistiu campos gerais e fiscais.");
            Check(edited.CriadoEm == detail.CriadoEm && edited.AtualizadoEm.HasValue && edited.TenantId == 1 && await db.Clientes.CountAsync() == 2, "Edicao recriou cliente ou alterou metadados de origem.");
            Check(edited.CadastroEnderecoId == detail.Dados.EnderecoId && edited.Contatos.Count == 2 && edited.Contatos.Any(c => c.Nome == "Contato adicional") && edited.DadosComerciais?.LimiteCredito == 1500, "Edicao apagou endereco, contatos ou dados comerciais.");
            var fiscal = await new EmitenteFiscalController(db, Tenant(1)).Destinatario(7, default);
            Check(fiscal is OkObjectResult { Value: DestinatarioFiscalRequest { InscricaoEstadual: "0290123456", ConsumidorFinal: false } }, "Emissao fiscal nao recebeu os dados atualizados do cliente.");
            Check(await controller.Atualizar(7, request with { InscricaoEstadual = "" }) is BadRequestObjectResult, "Aceitou contribuinte sem IE.");
            Check(await controller.Atualizar(7, request with { CpfCnpj = "12345678909" }) is BadRequestObjectResult, "Aceitou CPF no cadastro PJ.");
            Check(await controller.Atualizar(7, request with { CpfCnpj = "11444777000161" }) is ConflictObjectResult, "Aceitou documento duplicado.");
            Check(await controller.Atualizar(7, request with { EnderecoId = 700 }) is BadRequestObjectResult, "Aceitou endereco de outra empresa.");
            Check((await controller.Criar(request with { CpfCnpj = "12345678909", TipoPessoa = TipoPessoa.fisica, IndicadorIe = IndicadorIe.NaoContribuinte, InscricaoEstadual = null, NomeRazaoSocial = "Pessoa fisica" })).Result is CreatedResult, "Criacao de pessoa fisica regrediu.");
        }
        await using (var db = new AppDbContext(options, Tenant(2)))
        {
            var controller = new ClientesController(db);
            Check((await controller.Obter(7, 2)).Result is NotFoundObjectResult, "Consulta acessou cliente de outra empresa.");
            Check(await controller.Atualizar(7, request with { EmpresaId = 2 }) is NotFoundObjectResult, "Edicao acessou cliente de outra empresa.");
        }
        Console.WriteLine($"Customer editor checks: {checks} passed; cadastro completo, IE, dados associados e isolamento de empresas.");
    }
}
