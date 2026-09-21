using back.Contracts;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

var saasGroup = app.MapGroup("/api/saas").WithTags("SaaS");
var authGroup = app.MapGroup("/api/auth").WithTags("Auth");

saasGroup.MapGet("/bootstrap-status", async (AppDbContext db) =>
{
    var administradorSaasCriado = await db.Users.AnyAsync(u => u.Perfil == PerfilUsuario.AdministradorSaas);
    return Results.Ok(new BootstrapStatusResponse(administradorSaasCriado));
})
.WithName("ObterStatusBootstrapSaas");

saasGroup.MapPost("/administradores", async (
    CriarAdministradorSaasRequest request,
    AppDbContext db,
    IPasswordService passwordService) =>
{
    var validationError = ValidarUsuario(request.Nome, request.Email, request.Senha);
    if (validationError is not null)
    {
        return Results.BadRequest(validationError);
    }

    var hasAdministradorSaas = await db.Users.AnyAsync(u => u.Perfil == PerfilUsuario.AdministradorSaas);
    if (hasAdministradorSaas)
    {
        return Results.Conflict("Ja existe um administrador SaaS cadastrado.");
    }

    var emailNormalizado = NormalizarEmail(request.Email);
    var emailEmUso = await db.Users.AnyAsync(u => u.EmailNormalizado == emailNormalizado);
    if (emailEmUso)
    {
        return Results.Conflict("E-mail ja cadastrado.");
    }

    var usuario = new Users
    {
        Nome = request.Nome.Trim(),
        Email = request.Email.Trim(),
        EmailNormalizado = emailNormalizado,
        Perfil = PerfilUsuario.AdministradorSaas
    };

    usuario.PasswordHash = passwordService.HashPassword(usuario, request.Senha);

    db.Users.Add(usuario);
    await db.SaveChangesAsync();

    return Results.Created($"/api/saas/administradores/{usuario.Id}", ToUsuarioResponse(usuario));
})
.WithName("CriarAdministradorSaas");

saasGroup.MapPost("/empresas", async (
    CriarEmpresaContratanteRequest request,
    AppDbContext db,
    IPasswordService passwordService) =>
{
    var empresaValidationError = ValidarEmpresa(request);
    if (empresaValidationError is not null)
    {
        return Results.BadRequest(empresaValidationError);
    }

    var usuarioValidationError = ValidarUsuario(
        request.Administrador.Nome,
        request.Administrador.Email,
        request.Administrador.Senha);

    if (usuarioValidationError is not null)
    {
        return Results.BadRequest(usuarioValidationError);
    }

    var cnpjNormalizado = ApenasDigitos(request.Cnpj);
    var empresaExiste = await db.Empresas.AnyAsync(e => e.Cnpj == cnpjNormalizado);
    if (empresaExiste)
    {
        return Results.Conflict("CNPJ ja cadastrado.");
    }

    var emailNormalizado = NormalizarEmail(request.Administrador.Email);
    var emailEmUso = await db.Users.AnyAsync(u => u.EmailNormalizado == emailNormalizado);
    if (emailEmUso)
    {
        return Results.Conflict("E-mail do administrador ja cadastrado.");
    }

    await using var transaction = await db.Database.BeginTransactionAsync();

    var empresa = new Empresas
    {
        RazaoSocial = request.RazaoSocial.Trim(),
        NomeFantasia = request.NomeFantasia.Trim(),
        Cnpj = cnpjNormalizado,
        Telefone = TextoOuNull(request.Telefone),
        Email = TextoOuNull(request.Email),
        EmailFiscal = TextoOuNull(request.EmailFiscal),
        Endereco = TextoOuNull(request.Endereco),
        Numero = TextoOuNull(request.Numero),
        Complemento = TextoOuNull(request.Complemento),
        Bairro = TextoOuNull(request.Bairro),
        Cidade = TextoOuNull(request.Cidade),
        Estado = TextoOuNull(request.Estado),
        Cep = TextoOuNull(request.Cep) is string cep ? ApenasDigitos(cep) : null,
        InscricaoMunicipal = TextoOuNull(request.InscricaoMunicipal),
        InscricaoEstadual = TextoOuNull(request.InscricaoEstadual),
        LogoUrl = TextoOuNull(request.LogoUrl),
        UtilizaAPAssistant = request.UtilizaAPAssistant
    };

    db.Empresas.Add(empresa);
    await db.SaveChangesAsync();

    var administrador = new Users
    {
        EmpresaId = empresa.Id,
        Nome = request.Administrador.Nome.Trim(),
        Email = request.Administrador.Email.Trim(),
        EmailNormalizado = emailNormalizado,
        Perfil = PerfilUsuario.AdministradorEmpresa
    };

    administrador.PasswordHash = passwordService.HashPassword(administrador, request.Administrador.Senha);

    db.Users.Add(administrador);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = new EmpresaContratanteResponse(
        empresa.Id,
        empresa.RazaoSocial,
        empresa.NomeFantasia,
        empresa.Cnpj,
        ToUsuarioResponse(administrador));

    return Results.Created($"/api/saas/empresas/{empresa.Id}", response);
})
.WithName("CriarEmpresaContratante");

saasGroup.MapGet("/empresas", async (AppDbContext db) =>
{
    var empresas = await db.Empresas
        .OrderBy(e => e.NomeFantasia)
        .Select(e => new EmpresaResumoResponse(
            e.Id,
            e.RazaoSocial,
            e.NomeFantasia,
            e.Cnpj,
            e.Ativo,
            e.AcessoBloqueado,
            e.DataCadastro))
        .ToListAsync();

    return Results.Ok(empresas);
})
.WithName("ListarEmpresasContratantes");

saasGroup.MapPost("/usuarios", async (
    CriarUsuarioEmpresaRequest request,
    AppDbContext db,
    IPasswordService passwordService) =>
{
    var validationError = ValidarUsuario(request.Nome, request.Email, request.Senha);
    if (validationError is not null)
    {
        return Results.BadRequest(validationError);
    }

    if (!Enum.IsDefined(typeof(PerfilUsuario), request.Perfil) ||
        request.Perfil == (int)PerfilUsuario.AdministradorSaas)
    {
        return Results.BadRequest("Perfil de usuario invalido para empresa contratante.");
    }

    var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == request.EmpresaId);
    if (empresa is null)
    {
        return Results.NotFound("Empresa nao encontrada.");
    }

    if (!empresa.Ativo || empresa.AcessoBloqueado)
    {
        return Results.Problem("Empresa bloqueada ou inativa.", statusCode: StatusCodes.Status403Forbidden);
    }

    var emailNormalizado = NormalizarEmail(request.Email);
    var emailEmUso = await db.Users.AnyAsync(u => u.EmailNormalizado == emailNormalizado);
    if (emailEmUso)
    {
        return Results.Conflict("E-mail ja cadastrado.");
    }

    var usuario = new Users
    {
        EmpresaId = empresa.Id,
        Nome = request.Nome.Trim(),
        Email = request.Email.Trim(),
        EmailNormalizado = emailNormalizado,
        Perfil = (PerfilUsuario)request.Perfil
    };

    usuario.PasswordHash = passwordService.HashPassword(usuario, request.Senha);

    db.Users.Add(usuario);
    await db.SaveChangesAsync();

    return Results.Created($"/api/saas/usuarios/{usuario.Id}", ToUsuarioResponse(usuario));
})
.WithName("CriarUsuarioEmpresa");

authGroup.MapPost("/login", async (
    LoginRequest request,
    AppDbContext db,
    IPasswordService passwordService) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
    {
        return Results.BadRequest("E-mail e senha sao obrigatorios.");
    }

    var emailNormalizado = NormalizarEmail(request.Email);
    var usuario = await db.Users
        .Include(u => u.Empresa)
        .FirstOrDefaultAsync(u => u.EmailNormalizado == emailNormalizado);

    if (usuario is null || !usuario.Ativo || !passwordService.VerifyPassword(usuario, request.Senha))
    {
        return Results.Unauthorized();
    }

    if (usuario.Empresa is not null && (!usuario.Empresa.Ativo || usuario.Empresa.AcessoBloqueado))
    {
        return Results.Problem("Acesso da empresa bloqueado ou inativo.", statusCode: StatusCodes.Status403Forbidden);
    }

    usuario.UltimoAcessoEm = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(ToUsuarioResponse(usuario));
})
.WithName("Login");

app.Run();

static string? ValidarUsuario(string nome, string email, string senha)
{
    if (string.IsNullOrWhiteSpace(nome))
    {
        return "Nome do usuario e obrigatorio.";
    }

    if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
    {
        return "E-mail do usuario e obrigatorio e deve ser valido.";
    }

    if (string.IsNullOrWhiteSpace(senha) || senha.Length < 8)
    {
        return "Senha deve ter pelo menos 8 caracteres.";
    }

    return null;
}

static string? ValidarEmpresa(CriarEmpresaContratanteRequest request)
{
    if (string.IsNullOrWhiteSpace(request.RazaoSocial))
    {
        return "Razao social e obrigatoria.";
    }

    if (string.IsNullOrWhiteSpace(request.NomeFantasia))
    {
        return "Nome fantasia e obrigatorio.";
    }

    var cnpj = ApenasDigitos(request.Cnpj);
    if (cnpj.Length != 14)
    {
        return "CNPJ deve conter 14 digitos.";
    }

    return null;
}

static string NormalizarEmail(string email)
{
    return email.Trim().ToUpperInvariant();
}

static string ApenasDigitos(string value)
{
    return new string(value.Where(char.IsDigit).ToArray());
}

static string? TextoOuNull(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static UsuarioResponse ToUsuarioResponse(Users usuario)
{
    return new UsuarioResponse(
        usuario.Id,
        usuario.EmpresaId,
        usuario.Nome,
        usuario.Email,
        usuario.Perfil.ToString(),
        usuario.Ativo,
        usuario.CriadoEm);
}
