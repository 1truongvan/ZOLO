using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApplication1.Services;

namespace WebApplication1.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ModulePermissionAttribute(string moduleKey, string? operation = null) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.Filters.OfType<Microsoft.AspNetCore.Mvc.Authorization.IAllowAnonymousFilter>().Any()) return;
        var service = context.HttpContext.RequestServices.GetRequiredService<IModulePermissionService>();
        var allowed = await service.IsAllowedAsync(context.HttpContext.User, context.HttpContext, moduleKey, operation, context.HttpContext.RequestAborted);
        if (!allowed) context.Result = new ObjectResult(new { message = "Bạn không được cấp quyền thực hiện chức năng này." }) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
