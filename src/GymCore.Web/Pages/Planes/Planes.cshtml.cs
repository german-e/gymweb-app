using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using GymCore.Dominio.Enums;

namespace GymCore.Web.Pages.Planes;

public class PlanesModel : PageModel
{
    private readonly GymDbContext _db;
    public PlanesModel(GymDbContext db) => _db = db;

    public List<Plan> Items { get; set; } = new();
        public Dictionary<int, int> ActivosPorPlan { get; set; } = new();

    public async Task OnGetAsync()
    {
        Items = await _db.Planes.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();

        var hoy = DateTime.Today;
        var planIds = Items.Select(p => p.Id).ToList();
        if (planIds.Count > 0)
        {
            ActivosPorPlan = await _db.SocioPlanes.AsNoTracking()
                .Where(sp => planIds.Contains(sp.PlanId)
                             && sp.Estado == EstadoSuscripcion.Activo
                             && sp.FechaFin >= hoy)
                .GroupBy(sp => sp.PlanId)
                .Select(g => new { PlanId = g.Key, Cant = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Cant);
        }
    }
}
