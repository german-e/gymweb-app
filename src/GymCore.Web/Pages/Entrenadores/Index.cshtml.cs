using GymCore.Web.Seguridad;
using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Entrenadores;

[RequierePermiso(Permisos.EntrenadoresVer)]
public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IWebHostEnvironment _env;
    public IndexModel(GymDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public record Fila(Entrenador Entrenador, int SociosAsignados);

    public List<Fila> Filas { get; private set; } = new();
    public List<string> Especialidades { get; private set; } = new();

    // KPIs
    public int Total { get; private set; }
    public int Activos { get; private set; }
    public int Inactivos { get; private set; }
    public int SociosAsignados { get; private set; }

    // Filtros iniciales (deep-link): ?estado=activo|inactivo&q=...
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }

    [TempData] public string? Flash { get; set; }

    public async Task OnGetAsync()
    {
        var entrenadores = await _db.Entrenadores.AsNoTracking()
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre)
            .ToListAsync();

        var conteos = await _db.EntrenadorSocios.AsNoTracking()
            .Where(a => a.Activa)
            .GroupBy(a => a.EntrenadorId)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad);

        Filas = entrenadores.Select(e => new Fila(e, conteos.GetValueOrDefault(e.Id))).ToList();
        Especialidades = entrenadores.Select(e => e.Especialidad).Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList()!;

        Total = entrenadores.Count;
        Activos = entrenadores.Count(e => e.Activo);
        Inactivos = Total - Activos;
        // Socios únicos con al menos un entrenador
        SociosAsignados = await _db.EntrenadorSocios.Where(a => a.Activa).Select(a => a.SocioId).Distinct().CountAsync();
    }

    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnPostToggleEstadoAsync(int id)
    {
        Flash = await EntrenadorAcciones.AlternarEstadoAsync(_db, id);
        return RedirectToPage();
    }

    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        Flash = await EntrenadorAcciones.EliminarAsync(_db, _env, id);
        return RedirectToPage();
    }
}
