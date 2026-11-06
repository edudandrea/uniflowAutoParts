using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace back.Services;

// Os identificadores enviados pelo navegador nao concedem acesso a outra empresa.
public sealed class TenantActionFilter(TenantContext tenant) : IAsyncActionFilter, IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not TenantAccessException) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = 403, Detail = "Operacao nao permitida para a empresa autenticada." }) { StatusCode = 403 };
        context.ExceptionHandled = true;
    }
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null)
        { await next(); return; }
        if (tenant.EmpresaId <= 0) { context.Result = new UnauthorizedResult(); return; }
        foreach (var parameter in context.ActionDescriptor.Parameters.Where(p => p.Name is "empresaId" or "tenantId"))
        {
            context.ActionArguments.TryGetValue(parameter.Name, out var supplied);
            if (supplied is not null && Convert.ToInt64(supplied) != 0 && Convert.ToInt64(supplied) != tenant.EmpresaId)
            { context.Result = new ForbidResult(); return; }
            context.ActionArguments[parameter.Name] = Convert.ChangeType(tenant.EmpresaId, parameter.ParameterType);
        }
        foreach (var (name, value) in context.ActionArguments.ToArray())
        {
            if (name is "empresaId" or "tenantId")
            {
                if (value is not null && Convert.ToInt64(value) != 0 && Convert.ToInt64(value) != tenant.EmpresaId)
                { context.Result = new ForbidResult(); return; }
                context.ActionArguments[name] = value is long ? (object)(long)tenant.EmpresaId : tenant.EmpresaId;
            }
            else if (value is not null)
            {
                var property = value.GetType().GetProperty("EmpresaId") ?? value.GetType().GetProperty("TenantId");
                if (property is null) continue;
                var supplied = Convert.ToInt64(property.GetValue(value));
                if (supplied != 0 && supplied != tenant.EmpresaId)
                { context.Result = new ForbidResult(); return; }
                property.SetValue(value, Convert.ChangeType(tenant.EmpresaId, property.PropertyType));
            }
        }
        await next();
    }
}

public sealed class TenantAccessException : InvalidOperationException
{
    public TenantAccessException() : base("Operacao nao permitida para a empresa autenticada.") { }
}
