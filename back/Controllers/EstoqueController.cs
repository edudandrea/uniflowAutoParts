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

    [HttpPut("{id:int}/margem")]
    public async Task<IActionResult> AtualizarMargem(int id, AtualizarMargemPrecoRequest request,
        [FromQuery] int empresaId, CancellationToken cancellationToken)
    {
        if (empresaId <= 0) return BadRequest("EmpresaId e obrigatorio.");
        if (request.MargemPreco is not decimal margem || margem < 0 || margem > 99999.9999m ||
            decimal.Round(margem, 4) != margem)
            return BadRequest("Informe uma margem positiva ou zero, com ate quatro casas decimais.");
        var produto = await db.CadastroItens.SingleOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId, cancellationToken);
        if (produto is null) return NotFound();
        if (!double.IsFinite(produto.Custo) || produto.Custo < 0)
            return BadRequest("O custo do produto deve ser valido para calcular o preco de venda.");
        produto.MargemPreco = margem;
        produto.AtualizarPrecoPelaMargem();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { produto.Id, Custo = produto.Custo, MargemPreco = produto.MargemPreco, PrecoVenda = produto.Preco });
    }
}
