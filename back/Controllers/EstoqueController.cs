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
                saldo == null ? 0 : saldo.Quantidade,
                produto.EstoqueMinimo,
                produto.Preco,
                produto.Codfabricante == 0 ? "" : produto.Codfabricante.ToString(),
                produto.NCM == 0 ? "" : produto.NCM.ToString()))
            .ToListAsync(cancellationToken);

        return Ok(produtos);
    }
}
