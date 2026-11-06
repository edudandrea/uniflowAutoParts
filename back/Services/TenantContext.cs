using System.Security.Claims;

namespace back.Services;

public sealed class TenantContext(IHttpContextAccessor accessor)
{
    public const string EmpresaClaim = "autoparts_empresa_id";
    public int EmpresaId => accessor.HttpContext?.User.Identity?.IsAuthenticated == true &&
        int.TryParse(accessor.HttpContext.User.FindFirstValue(EmpresaClaim), out var id) && id > 0 ? id : 0;
}
