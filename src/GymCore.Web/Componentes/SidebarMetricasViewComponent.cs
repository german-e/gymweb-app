using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace GymCore.Web.Componentes;

// Métricas en vivo del layout: tarjeta de cobranza del sidebar ("Sidebar") y centro de alertas ("Alertas").
// Si la consulta falla, el layout se sigue mostrando sin cifras.
public class SidebarMetricasViewComponent : ViewComponent
{
    private readonly GymDbContext _db;
    private readonly ILogger<SidebarMetricasViewComponent> _log;
    private readonly IUsuarioActual _usuario;
    private readonly ServicioInventario _inventario;
    public SidebarMetricasViewComponent(GymDbContext db, ILogger<SidebarMetricasViewComponent> log, IUsuarioActual usuario, ServicioInventario inventario)
    { _db = db; _log = log; _usuario = usuario; _inventario = inventario; }

    public async Task<IViewComponentResult> InvokeAsync(string vista = "Sidebar")
    {
        IndicadoresGimnasio.Resumen? r = null;
        try { r = await IndicadoresGimnasio.DelRequestAsync(HttpContext, _db); }
        catch (Exception ex) { _log.LogWarning(ex, "No se pudieron calcular las métricas del layout"); }
        if (vista == "Alertas" && _usuario.Puede(Permisos.ProductosVer))
        {
            try { ViewData["Stock"] = await _inventario.ResumenDelRequestAsync(HttpContext); }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudieron calcular las alertas de stock"); }
        }
        return View(vista, r);
    }
}
