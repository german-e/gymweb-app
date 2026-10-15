using GymCore.Web.Seguridad;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

// Ficha del plan (solo lectura): reglas, indicadores y socios suscriptos.
[RequierePermiso(Permisos.PlanesVer)]
public class DetalleModel : PageModel
{
    private readonly GymDbContext _db;
    public DetalleModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    /// <summary>Última suscripción de cada socio a este plan.</summary>
    public record FilaSocio(Socio Socio, SocioPlan Suscripcion, int Periodos, decimal PagadoPeriodo, bool Vigente);

    public Plan Plan { get; private set; } = null!;
    public List<FilaSocio> Socios { get; private set; } = new();

    // KPIs
    public int SociosActivos { get; private set; }
    public int TotalSuscripciones { get; private set; }
    public int Renovaciones { get; private set; }        // suscripciones de socios que ya habían tenido este plan
    public decimal IngresosGenerados { get; private set; } // pagos asociados a suscripciones del plan
    public decimal IngresoMensualEstimado => SociosActivos * Plan.Precio;

    public List<EventoSistema> Historial { get; private set; } = new();

    [TempData] public string? Flash { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var plan = await _db.Planes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == Id);
        if (plan is null) return NotFound();
        Plan = plan;

        var hoy = DateTime.Today;
        var suscripciones = await _db.SocioPlanes.AsNoTracking()
            .Include(sp => sp.Socio)
            .Where(sp => sp.PlanId == Id)
            .ToListAsync();

        // Suma en memoria: SQLite no agrega decimal del lado del servidor
        var ids = suscripciones.Select(sp => sp.Id).ToList();
        var pagado = (await _db.Pagos.AsNoTracking()
                .Where(p => p.SocioPlanId != null && ids.Contains(p.SocioPlanId.Value))
                .Select(p => new { Id = p.SocioPlanId!.Value, p.Importe })
                .ToListAsync())
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Importe));

        bool EsVigente(SocioPlan sp) => sp.Estado == EstadoSuscripcion.Activo && sp.FechaFin >= hoy;

        Socios = suscripciones
            .GroupBy(sp => sp.SocioId)
            .Select(g =>
            {
                var ultima = g.OrderByDescending(sp => sp.FechaFin).First();
                return new FilaSocio(ultima.Socio, ultima, g.Count(), pagado.GetValueOrDefault(ultima.Id), EsVigente(ultima));
            })
            .OrderByDescending(f => f.Vigente).ThenBy(f => f.Socio.Apellido).ThenBy(f => f.Socio.Nombre)
            .ToList();

        SociosActivos = suscripciones.Count(EsVigente);
        TotalSuscripciones = suscripciones.Count;
        Renovaciones = TotalSuscripciones - Socios.Count;
        IngresosGenerados = pagado.Values.Sum();
        Historial = await _db.EventosSistema.AsNoTracking()
            .Where(e => e.Entidad == EntidadAuditada.Plan && e.EntidadId == Id)
            .OrderByDescending(e => e.FechaUtc).ThenByDescending(e => e.Id)
            .Take(200)
            .ToListAsync();
        return Page();
    }

    [RequierePermiso(Permisos.PlanesEditar)]
    public async Task<IActionResult> OnPostToggleEstadoAsync()
    {
        Flash = await PlanAcciones.AlternarEstadoAsync(_db, Id);
        return RedirectToPage(new { id = Id });
    }
}
