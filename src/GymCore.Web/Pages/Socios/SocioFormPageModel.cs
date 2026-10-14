using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

// Base compartida por Alta y Edición (_SocioForm).
public abstract class SocioFormPageModel : PageModel
{
    protected readonly GymDbContext Db;
    private readonly IWebHostEnvironment _env;

    protected SocioFormPageModel(GymDbContext db, IWebHostEnvironment env) { Db = db; _env = env; }

    [BindProperty] public SocioInput Input { get; set; } = new();

    public abstract bool EsEdicion { get; }
    public virtual int? IdSocio => null;
    public string? FotoActualUrl { get; protected set; }
    public List<Plan> Planes { get; private set; } = new();
    public List<Entrenador> Entrenadores { get; private set; } = new();

    // Solo lectura en edición (se gestionan desde la ficha)
    public string? PlanActual { get; protected set; }
    public string? EntrenadorActual { get; protected set; }

    protected async Task CargarListasAsync()
    {
        Planes = await Db.Planes.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();
        Entrenadores = await Db.Entrenadores.AsNoTracking().Where(e => e.Activo)
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToListAsync();
    }

    protected async Task ValidarUnicidadAsync(int? excluirId = null)
    {
        var dni = Input.Dni?.Trim();
        if (!string.IsNullOrEmpty(dni) && await Db.Socios.AnyAsync(s => (s.Dni == dni || s.TarjetaCodigo == dni) && s.Id != excluirId))
            ModelState.AddModelError("Input.Dni", "Ya existe un socio con ese DNI.");

        if (Input.NumeroSocio is { } n && await Db.Socios.AnyAsync(s => (s.NumeroSocio == n || (s.NumeroSocio == null && s.Id == n)) && s.Id != excluirId))
            ModelState.AddModelError("Input.NumeroSocio", "Ese número de socio ya está en uso.");
    }

    protected async Task ProcesarFotoAsync(Socio s)
    {
        var (url, _) = await FotoStorage.ProcesarAsync(_env, "socios", s.FotoUrl, Input.Foto, Input.QuitarFoto);
        s.FotoUrl = url;
    }
}
