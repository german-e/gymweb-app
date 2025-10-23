using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

public class SuscriptosModel : PageModel
{
    private readonly GymDbContext _db;
    public SuscriptosModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int PlanId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool SoloActivos { get; set; } = true;

    public Plan? Plan { get; set; }
    public List<(Socio Socio, SocioPlan Suscripcion)> Socios { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Plan = await _db.Planes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == PlanId);
        if (Plan == null)
        {
            return NotFound();
        }

        var hoy = DateTime.Today;
        var query = _db.SocioPlanes
            .AsNoTracking()
            .Include(sp => sp.Socio)
            .Where(sp => sp.PlanId == PlanId);

        if (SoloActivos)
        {
            query = query.Where(sp => sp.Estado == EstadoSuscripcion.Activo && sp.FechaFin >= hoy);
        }

        var lista = await query
            .OrderByDescending(sp => sp.FechaFin)
            .ToListAsync();

        Socios = lista.Select(sp => (sp.Socio, sp)).ToList();
        return Page();
    }
}
