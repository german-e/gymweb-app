using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Administracion.Usuarios;

[RequierePermiso(Permisos.UsuariosAdministrar)]
public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _actual;
    public IndexModel(GymDbContext db, IUsuarioActual actual) { _db = db; _actual = actual; }

    public List<Usuario> Usuarios { get; private set; } = new();
    public List<Rol> Roles { get; private set; } = new();
    public int? MiId => _actual.UsuarioId;

    public int Activos => Usuarios.Count(u => u.Activo);
    public int Inactivos => Usuarios.Count(u => !u.Activo);
    public int Bloqueados => Usuarios.Count(u => u.Activo && u.EstaBloqueado(DateTime.UtcNow));
    public int Accesos24h => Usuarios.Count(u => u.UltimoAcceso >= DateTime.UtcNow.AddHours(-24));
    public Usuario? UltimoEnIngresar => Usuarios.Where(u => u.UltimoAcceso != null).MaxBy(u => u.UltimoAcceso);

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }
    [TempData] public string? PasswordTemporal { get; set; }

    public async Task OnGetAsync()
    {
        Usuarios = await _db.Usuarios.AsNoTracking().Include(u => u.Rol).Include(u => u.Entrenador)
            .OrderByDescending(u => u.Activo).ThenBy(u => u.NombreCompleto).ToListAsync();
        Roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Nombre).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleEstadoAsync(int id) => Resultado(await UsuarioAcciones.AlternarEstadoAsync(_db, _actual, id));
    public async Task<IActionResult> OnPostResetearAsync(int id) => Resultado(await UsuarioAcciones.ResetearPasswordAsync(_db, id));
    public async Task<IActionResult> OnPostDesbloquearAsync(int id) => Resultado(await UsuarioAcciones.DesbloquearAsync(_db, id));

    private IActionResult Resultado(ResultadoAccion r)
    {
        if (r.Ok) { Flash = r.Mensaje; PasswordTemporal = r.PasswordTemporal; } else FlashError = r.Mensaje;
        return RedirectToPage();
    }
}
