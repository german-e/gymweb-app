using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Administracion.Usuarios;

[RequierePermiso(Permisos.UsuariosAdministrar)]
public class DetalleModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _actual;
    public DetalleModel(GymDbContext db, IUsuarioActual actual) { _db = db; _actual = actual; }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public Usuario Usuario { get; private set; } = null!;
    public bool EsYo => Usuario.Id == _actual.UsuarioId;
    public List<EventoSistema> Accesos { get; private set; } = new();
    public List<EventoSistema> Cuenta { get; private set; } = new();      // cambios sobre la cuenta (alta, rol, contraseña…)
    public List<EventoSistema> Actividad { get; private set; } = new();   // lo que hizo el usuario
    public int AccionesTotales { get; private set; }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }
    [TempData] public string? PasswordTemporal { get; set; }

    private static readonly List<TipoEventoSistema> TiposAcceso = new()
    {
        TipoEventoSistema.SesionIniciada, TipoEventoSistema.SesionCerrada, TipoEventoSistema.LoginFallido,
        TipoEventoSistema.UsuarioBloqueado, TipoEventoSistema.AccesoDenegado
    };

    public async Task<IActionResult> OnGetAsync()
    {
        var u = await _db.Usuarios.AsNoTracking().Include(x => x.Rol).ThenInclude(r => r.Permisos).Include(x => x.Entrenador)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (u is null) return NotFound();
        Usuario = u;

        var sobreLaCuenta = _db.EventosSistema.AsNoTracking().Where(e => e.Entidad == EntidadAuditada.Usuario && e.EntidadId == Id);
        Accesos = await sobreLaCuenta.Where(e => TiposAcceso.Contains(e.Tipo)).OrderByDescending(e => e.FechaUtc).Take(10).ToListAsync();
        Cuenta = await sobreLaCuenta.Where(e => !TiposAcceso.Contains(e.Tipo)).OrderByDescending(e => e.FechaUtc).Take(30).ToListAsync();
        var hechas = _db.EventosSistema.AsNoTracking().Where(e => e.UsuarioId == Id && !TiposAcceso.Contains(e.Tipo));
        AccionesTotales = await hechas.CountAsync();
        Actividad = await hechas.OrderByDescending(e => e.FechaUtc).ThenByDescending(e => e.Id).Take(20).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleEstadoAsync(int id) => Resultado(await UsuarioAcciones.AlternarEstadoAsync(_db, _actual, id));
    public async Task<IActionResult> OnPostResetearAsync(int id) => Resultado(await UsuarioAcciones.ResetearPasswordAsync(_db, id));
    public async Task<IActionResult> OnPostDesbloquearAsync(int id) => Resultado(await UsuarioAcciones.DesbloquearAsync(_db, id));

    private IActionResult Resultado(ResultadoAccion r)
    {
        if (r.Ok) { Flash = r.Mensaje; PasswordTemporal = r.PasswordTemporal; } else FlashError = r.Mensaje;
        return RedirectToPage(new { id = Id });
    }
}
