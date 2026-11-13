using back.Contracts;
using back.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/estoque")]
public class EstoqueController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EstoqueResponse>>> Listar(
        [FromQuery] int empresaId,
        CancellationToken cancellationToken)
    {
        if (empresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        var saldos = db.Estoques.AsNoTracking()
            .Where(movimento => movimento.EmpresaId == empresaId)
            .GroupBy(movimento => movimento.ItemId)
            .Select(grupo => new
            {
                ItemId = grupo.Key,
                Quantidade = grupo.Sum(movimento =>
                    movimento.Tipo.Trim().ToLower() == "saida" ||
                    movimento.Tipo.Trim().ToLower() == "saída"
                        ? -Math.Abs(movimento.Quantidade)
                        : movimento.Quantidade)
            });

        var produtos = await (
            from produto in db.CadastroItens.AsNoTracking()
            where produto.EmpresaId == empresaId
            join saldo in saldos on produto.Id equals saldo.ItemId into saldosProduto
            from saldo in saldosProduto.DefaultIfEmpty()
            orderby produto.Descricao, produto.Id
            select new EstoqueResponse(
                produto.Id,
                produto.SKU.ToString(),
                produto.Descricao,
                produto.Marca != null && produto.Marca.TenantId == empresaId ? produto.Marca.Nome : "",
                produto.Categoria != null && produto.Categoria.TenantId == empresaId ? produto.Categoria.Nome : "",
                // O LEFT JOIN retorna NULL para produtos sem movimentos; tratar no SQL.
                (decimal?)saldo.Quantidade ?? 0,
                produto.EstoqueMinimo,
                produto.Preco,
                produto.Codfabricante == 0 ? "" : produto.Codfabricante.ToString(),
                produto.NCM == 0 ? "" : produto.NCM.ToString(),
                produto.Custo,
                produto.MargemPreco))
            .ToListAsync(cancellationToken);

        return Ok(produtos);
    }

    [HttpGet("margem")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> LerMargem([FromQuery] int empresaId, CancellationToken token)
    {
        if (empresaId <= 0) return BadRequest("EmpresaId e obrigatorio.");
        var empresa = await db.Empresas.AsNoTracking().Where(e => e.Id == empresaId)
            .Select(e => new { MargemPreco = e.MargemPrecoEstoque }).SingleOrDefaultAsync(token);
        return empresa is null ? NotFound() : Ok(empresa);
    }

    [HttpPut("margem")]
    public async Task<IActionResult> AtualizarMargemGeral(AtualizarMargemPrecoRequest request,
        [FromQuery] int empresaId, CancellationToken cancellationToken)
    {
        if (empresaId <= 0) return BadRequest("EmpresaId e obrigatorio.");
        if (request.MargemPreco is not decimal margem || margem < 0 || margem > 99999.9999m || decimal.Round(margem, 4) != margem)
            return BadRequest("Informe uma margem positiva ou zero, com ate quatro casas decimais.");
        var empresa = await db.Empresas.SingleOrDefaultAsync(e => e.Id == empresaId, cancellationToken);
        if (empresa is null) return NotFound();
        var produtos = await db.CadastroItens.Where(p => p.EmpresaId == empresaId).ToListAsync(cancellationToken);
        if (produtos.Any(p => !double.IsFinite(p.Custo) || p.Custo < 0 || p.Custo > (double)(decimal.MaxValue / (1 + margem / 100))))
            return BadRequest("Existe produto com custo invalido. Corrija os custos antes de aplicar a margem geral.");
        empresa.MargemPrecoEstoque = margem;
        foreach (var produto in produtos)
        {
            produto.MargemPreco = margem;
            produto.AtualizarPrecoPelaMargem();
        }
        // Configuração e todos os preços na mesma transação implícita, sem afetar quantidades ou notas já emitidas.
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { MargemPreco = margem, ProdutosAtualizados = produtos.Count });
    }

    [HttpPut("{id:int}/margem")]
    public IActionResult AtualizarMargem(int id, AtualizarMargemPrecoRequest request, [FromQuery] int empresaId)
        => Conflict("A margem agora e unica para todo o estoque. Utilize a configuracao geral de margem.");
}
