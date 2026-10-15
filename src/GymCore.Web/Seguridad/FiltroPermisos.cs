using System.Reflection;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymCore.Web.Seguridad;

// Protege una página (en el PageModel) o un handler (en el método OnPost.../OnGet...).
// Con varios códigos alcanza con tener uno; con varios atributos hay que cumplir todos.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequierePermisoAttribute : Attribute
{
    public RequierePermisoAttribute(params string[] permisos) => Permisos = permisos;
    public string[] Permisos { get; }
}

// Filtro global de Razor Pages:
//  1. Contraseña temporal: obliga a cambiarla antes de usar el sistema.
//  2. Permisos declarados con [RequierePermiso] en la página y en el handler elegido.
// La autenticación en sí la exige la política por defecto (FallbackPolicy) configurada en Program.cs.
public class FiltroPermisos : IAsyncPageFilter
{
    private static readonly string[] PaginasLibresConPasswordTemporal = { "/Cuenta/CambiarPassword", "/Cuenta/Logout", "/Cuenta/AccesoDenegado" };

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext ctx, PageHandlerExecutionDelegate next)
    {
        var usuario = ctx.HttpContext.RequestServices.GetRequiredService<IUsuarioActual>();
        var pagina = ctx.ActionDescriptor.ViewEnginePath;

        if (usuario.Autenticado && ctx.HttpContext.User.HasClaim(Reclamos.DebeCambiarPassword, "true")
            && !PaginasLibresConPasswordTemporal.Contains(pagina, StringComparer.OrdinalIgnoreCase))
        {
            ctx.Result = new RedirectToPageResult("/Cuenta/CambiarPassword", new { obligatorio = true });
            return;
        }

        var requeridos = ctx.HandlerInstance.GetType().GetCustomAttributes<RequierePermisoAttribute>(true)
            .Concat(ctx.HandlerMethod?.MethodInfo.GetCustomAttributes<RequierePermisoAttribute>(true) ?? Enumerable.Empty<RequierePermisoAttribute>());
        var faltante = requeridos.FirstOrDefault(r => !usuario.PuedeAlguno(r.Permisos));
        if (faltante is not null)
        {
            if (usuario.Autenticado)
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<GymDbContext>();
                db.Auditar(TipoEventoSistema.AccesoDenegado, EntidadAuditada.Usuario, usuario.UsuarioId, usuario.Nombre,
                    $"{pagina}{(ctx.HandlerMethod?.Name is { } h ? $" · {h}" : "")}", $"Requiere: {string.Join(" o ", faltante.Permisos)}");
                await db.SaveChangesAsync();
            }
            ctx.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
