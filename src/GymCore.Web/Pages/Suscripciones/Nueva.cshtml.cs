using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Suscripciones;

public class NuevaModel : PageModel
{
    private readonly GymDbContext _db;
    public NuevaModel(GymDbContext db) => _db = db;

    [BindProperty, Required] public int SocioId { get; set; }
    [BindProperty, Required] public int PlanId { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime FechaInicio { get; set; } = DateTime.Now.Date;

    public string? Mensaje { get; set; }

    public List<SelectListItem> Socios { get; set; } = new();
    public List<SelectListItem> Planes { get; set; } = new();

    public async Task OnGetAsync()
    {
        await CargarCombos();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CargarCombos();
        if (!ModelState.IsValid) return Page();

        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == PlanId);
        var socio = await _db.Socios.FirstOrDefaultAsync(s => s.Id == SocioId);
        if (plan is null || socio is null)
        {
            Mensaje = "Socio o Plan inválido.";
            return Page();
        }

        var hoy = DateTime.Now.Date;
        var activo = await _db.SocioPlanes.AnyAsync(sp => sp.SocioId == SocioId
            && sp.Estado == EstadoSuscripcion.Activo
            && sp.FechaFin >= hoy);
        if (activo)
        {
            Mensaje = "El socio ya tiene una suscripción activa.";
            return Page();
        }

        var inicio = FechaInicio.Date;
        var fin = inicio.AddDays(plan.DuracionDias);
        var sp = new SocioPlan
        {
            SocioId = SocioId,
            PlanId = PlanId,
            FechaInicio = inicio,
            FechaFin = fin,
            Estado = EstadoSuscripcion.Activo
        };
        _db.SocioPlanes.Add(sp);
        await _db.SaveChangesAsync();
        return RedirectToPage("/Panel");
    }

    private async Task CargarCombos()
    {
        Socios = await _db.Socios.AsNoTracking()
            .OrderBy(s => s.Apellido).ThenBy(s => s.Nombre)
            .Select(s => new SelectListItem($"{s.Apellido}, {s.Nombre} - {s.Dni}", s.Id.ToString()))
            .ToListAsync();
        Planes = await _db.Planes.AsNoTracking()
            .OrderBy(p => p.Nombre)
            .Select(p => new SelectListItem($"{p.Nombre} ($ {p.Precio} {p.Moneda})", p.Id.ToString()))
            .ToListAsync();
    }
}
