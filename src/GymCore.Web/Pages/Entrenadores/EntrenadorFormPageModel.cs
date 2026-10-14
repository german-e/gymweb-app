using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Entrenadores;

// Base compartida por Alta y Edición: mismo formulario (_EntrenadorForm), validaciones y manejo de foto.
public abstract class EntrenadorFormPageModel : PageModel
{
    protected readonly GymDbContext Db;
    private readonly IWebHostEnvironment _env;

    protected EntrenadorFormPageModel(GymDbContext db, IWebHostEnvironment env)
    {
        Db = db;
        _env = env;
    }

    [BindProperty]
    public EntrenadorInput Input { get; set; } = new();

    // Usados por el parcial del formulario
    public abstract bool EsEdicion { get; }
    public string? FotoActualUrl { get; protected set; }
    public string Iniciales =>
        $"{Input.Nombre.FirstOrDefault()}{Input.Apellido.FirstOrDefault()}".Trim().ToUpperInvariant() is { Length: > 0 } i ? i : "?";

    protected async Task ValidarDniUnicoAsync(int? excluirId = null)
    {
        var dni = Input.Dni?.Trim();
        if (string.IsNullOrEmpty(dni)) return;
        var existe = await Db.Entrenadores.AnyAsync(e => e.Dni == dni && (excluirId == null || e.Id != excluirId));
        if (existe)
            ModelState.AddModelError("Input.Dni", "Ya existe un entrenador con ese DNI.");
    }

    // Guarda/quita la foto y actualiza FotoUrl. Devuelve true si cambió.
    protected async Task<bool> ProcesarFotoAsync(Entrenador e)
    {
        var (url, cambio) = await FotoStorage.ProcesarAsync(_env, "entrenadores", e.FotoUrl, Input.Foto, Input.QuitarFoto);
        e.FotoUrl = url;
        return cambio;
    }
}
