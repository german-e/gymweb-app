using GymCore.Web.Seguridad;
using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GymCore.Dominio.Enums;

namespace GymCore.Web.Pages.Planes;

[RequierePermiso(Permisos.PlanesVer)]
public class PlanesModel : PageModel
{
    private readonly GymDbContext _db;
    public PlanesModel(GymDbContext db) => _db = db;

    /// <param name="Activos">socios con suscripción vigente al plan</param>
    /// <param name="Suscripciones">suscripciones históricas (impiden eliminar el plan)</param>
    public record Fila(Plan Plan, int Activos, int Suscripciones);

    public List<Fila> Filas { get; private set; } = new();

    // KPIs
    public int Total => Filas.Count;
    public int ActivosCount => Filas.Count(f => f.Plan.Activo);
    public int EnUso => Filas.Count(f => f.Activos > 0);
    public Fila? MasContratado => Filas.Where(f => f.Suscripciones > 0).MaxBy(f => f.Suscripciones);
    // Suma del precio de las suscripciones vigentes (referencia de ingreso recurrente)
    public decimal IngresoMensualEstimado => Filas.Sum(f => f.Activos * f.Plan.Precio);

    // Filtro inicial (deep-link desde el centro de alertas): ?estado=activo|inactivo
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        var planes = await _db.Planes.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();

        var hoy = DateTime.Today;
        var planIds = planes.Select(p => p.Id).ToList();
        var activosPorPlan = new Dictionary<int, int>();
        var suscripcionesPorPlan = new Dictionary<int, int>();
        if (planIds.Count > 0)
        {
            activosPorPlan = await _db.SocioPlanes.AsNoTracking()
                .Where(sp => planIds.Contains(sp.PlanId)
                             && sp.Estado == EstadoSuscripcion.Activo
                             && sp.FechaFin >= hoy)
                .GroupBy(sp => sp.PlanId)
                .Select(g => new { PlanId = g.Key, Cant = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Cant);

            suscripcionesPorPlan = await _db.SocioPlanes.AsNoTracking()
                .Where(sp => planIds.Contains(sp.PlanId))
                .GroupBy(sp => sp.PlanId)
                .Select(g => new { PlanId = g.Key, Cant = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Cant);
        }

        Filas = planes.Select(p => new Fila(p, activosPorPlan.GetValueOrDefault(p.Id), suscripcionesPorPlan.GetValueOrDefault(p.Id))).ToList();
    }

    [RequierePermiso(Permisos.PlanesEditar)]
    public async Task<IActionResult> OnPostToggleEstadoAsync(int id)
    {
        Flash = await PlanAcciones.AlternarEstadoAsync(_db, id);
        return RedirectToPage();
    }
}
