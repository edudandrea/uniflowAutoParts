using back.Services;
using Microsoft.AspNetCore.Mvc;

namespace back.Controllers;

[ApiController]
[Route("api/documentos")]
public class DocumentosController(ICloudflareR2StorageService storage) : ControllerBase
{
    private const long MaxLogoBytes = 5 * 1024 * 1024;

    [HttpPost("empresas/{tenantId:long}/marcas/logos")]
    [RequestSizeLimit(MaxLogoBytes)]
    public async Task<ActionResult<DocumentoUploadResponse>> UploadLogoMarca(
        long tenantId,
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
        {
            return BadRequest("TenantId e obrigatorio.");
        }

        if (arquivo.Length <= 0)
        {
            return BadRequest("Arquivo vazio.");
        }

        if (arquivo.Length > MaxLogoBytes)
        {
            return BadRequest("A logo deve ter no maximo 5 MB.");
        }

        if (!arquivo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Selecione um arquivo de imagem.");
        }

        var documento = await storage.UploadAsync(tenantId, "marcas/logos", arquivo, cancellationToken);

        return Ok(new DocumentoUploadResponse(documento.Key, documento.Url, documento.ContentType, documento.Size));
    }

    [HttpGet("empresas/{tenantId:long}/arquivo")]
    public async Task<IActionResult> ObterArquivo(long tenantId, [FromQuery] string key, CancellationToken cancellationToken)
    {
        var documento = await storage.DownloadAsync(tenantId, key, cancellationToken);
        if (documento is null)
        {
            return NotFound();
        }

        HttpContext.Response.RegisterForDisposeAsync(documento);
        return File(documento.Stream, documento.ContentType, documento.FileName);
    }
}

public record DocumentoUploadResponse(string Key, string Url, string ContentType, long Size);
