using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Cuenta;

// Mi perfil: datos propios, permisos efectivos, accesos y actividad.
public class PerfilModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _actual;
    private readonly Autenticacion _auth;
    public PerfilModel(GymDbContext db, IUsuarioActual actual, Autenticacion auth) { _db = db; _actual = actual; _auth = auth; }

    public Usuario Usuario { get; private set; } = null!;
    public List<EventoSistema> Accesos { get; private set; } = new();     // inicios/cierres/fallidos de la cuenta
    public List<EventoSistema> Actividad { get; private set; } = new();   // acciones que realizó

    [BindProperty, Required(ErrorMessage = "Ingresá tu nombre."), StringLength(120), Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [BindProperty, EmailAddress(ErrorMessage = "Email inválido."), StringLength(160), Display(Name = "Email")]
    public string? Email { get; set; }

    [TempData] public string? Flash { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CargarAsync()) return RedirectToPage("Login");
        NombreCompleto = Usuario.NombreCompleto;
        Email = Usuario.Email;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == _actual.UsuarioId);
        if (u is null) return RedirectToPage("Login");
        if (!ModelState.IsValid) { await CargarAsync(); return Page(); }

        var cambios = new List<string>();
        if (u.NombreCompleto != NombreCompleto.Trim()) cambios.Add("nombre");
        var email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        if (u.Email != email) cambios.Add("email");
        if (cambios.Count > 0)
        {
            u.NombreCompleto = NombreCompleto.Trim();
            u.Email = email;
            _db.Auditar(TipoEventoSistema.UsuarioEditado, EntidadAuditada.Usuario, u.Id, u.NombreCompleto, "Editó su perfil · cambió: " + string.Join(", ", cambios));
            await _db.SaveChangesAsync();
            await _auth.RenovarSesionAsync(u.Id);   // el nombre se muestra desde la sesión
        }
        Flash = cambios.Count > 0 ? "Perfil actualizado." : "No había cambios para guardar.";
        return RedirectToPage();
    }

    private async Task<bool> CargarAsync()
    {
        var u = await _db.Usuarios.AsNoTracking().Include(x => x.Rol).ThenInclude(r => r.Permisos).Include(x => x.Entrenador)
            .FirstOrDefaultAsync(x => x.Id == _actual.UsuarioId);
        if (u is null) return false;
        Usuario = u;
        var tiposAcceso = new List<TipoEventoSistema> { TipoEventoSistema.SesionIniciada, TipoEventoSistema.SesionCerrada, TipoEventoSistema.LoginFallido, TipoEventoSistema.UsuarioBloqueado };
        Accesos = await _db.EventosSistema.AsNoTracking()
            .Where(e => e.Entidad == EntidadAuditada.Usuario && e.EntidadId == u.Id && tiposAcceso.Contains(e.Tipo))
            .OrderByDescending(e => e.FechaUtc).Take(8).ToListAsync();
        Actividad = await _db.EventosSistema.AsNoTracking()
            .Where(e => e.UsuarioId == u.Id && !tiposAcceso.Contains(e.Tipo))
            .OrderByDescending(e => e.FechaUtc).ThenByDescending(e => e.Id).Take(15).ToListAsync();
        return true;
    }
}
