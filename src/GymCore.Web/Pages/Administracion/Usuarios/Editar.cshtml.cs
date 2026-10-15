using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Administracion.Usuarios;

// Alta (sin id) y edición de usuarios. El alta siempre emite una contraseña temporal.
[RequierePermiso(Permisos.UsuariosAdministrar)]
public class EditarModel : PageModel
{
    private readonly GymDbContext _db;
    public EditarModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    public bool EsEdicion => Id.HasValue;

    [BindProperty, Required(ErrorMessage = "Ingresá el nombre."), StringLength(120), Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [BindProperty, RegularExpression(@"^[a-zA-Z0-9._-]{3,60}$", ErrorMessage = "Entre 3 y 60 caracteres: letras, números, punto, guion o guion bajo.")]
    [Display(Name = "Usuario")]
    public string? NombreUsuario { get; set; }

    [BindProperty, EmailAddress(ErrorMessage = "Email inválido."), StringLength(160), Display(Name = "Email")]
    public string? Email { get; set; }

    [BindProperty, Required(ErrorMessage = "Elegí un rol."), Display(Name = "Rol")]
    public int? RolId { get; set; }

    [BindProperty, Display(Name = "Ficha de entrenador")]
    public int? EntrenadorId { get; set; }

    public List<Rol> Roles { get; private set; } = new();
    public List<Entrenador> Entrenadores { get; private set; } = new();
    public Usuario? Existente { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await CargarListasAsync();
        if (EsEdicion)
        {
            Existente = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == Id);
            if (Existente is null) return NotFound();
            NombreCompleto = Existente.NombreCompleto;
            NombreUsuario = Existente.NombreUsuario;
            Email = Existente.Email;
            RolId = Existente.RolId;
            EntrenadorId = Existente.EntrenadorId;
        }
        else RolId = Roles.FirstOrDefault(r => !r.AccesoTotal)?.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CargarListasAsync();
        var rol = Roles.FirstOrDefault(r => r.Id == RolId);
        if (rol is null) ModelState.AddModelError(nameof(RolId), "Elegí un rol.");
        if (EntrenadorId is { } eid && Entrenadores.All(e => e.Id != eid)) ModelState.AddModelError(nameof(EntrenadorId), "Entrenador inválido.");
        var email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();

        if (!EsEdicion)
        {
            var nombre = Usuario.Normalizar(NombreUsuario ?? "");
            if (nombre.Length == 0) ModelState.AddModelError(nameof(NombreUsuario), "Elegí un nombre de usuario.");
            else if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombre)) ModelState.AddModelError(nameof(NombreUsuario), "Ese usuario ya existe.");
            if (!ModelState.IsValid) return Page();

            var temporal = PoliticaPassword.GenerarTemporal();
            var u = new Usuario(NombreCompleto, nombre, Autenticacion.Hash(temporal), rol!.Id, debeCambiarPassword: true)
            {
                Email = email,
                EntrenadorId = EntrenadorId
            };
            _db.Usuarios.Add(u);
            _db.Auditar(TipoEventoSistema.UsuarioCreado, EntidadAuditada.Usuario, null, u.NombreCompleto, $"@{u.NombreUsuario} · rol {rol.Nombre}", referencia: u);
            await _db.SaveChangesAsync();

            TempData["Flash"] = $"Usuario {u.NombreCompleto} creado. Compartí la contraseña temporal por un canal seguro: no se vuelve a mostrar.";
            TempData["PasswordTemporal"] = temporal;
            return RedirectToPage("Detalle", new { id = u.Id });
        }

        var existente = await _db.Usuarios.Include(x => x.Rol).FirstOrDefaultAsync(x => x.Id == Id);
        if (existente is null) return NotFound();
        if (rol is not null && rol.Id != existente.RolId && !rol.Tiene(Permisos.UsuariosAdministrar)
            && !await UsuarioAcciones.QuedaAdministradorAsync(_db, existente.Id, rolDespues: rol.Id))
            ModelState.AddModelError(nameof(RolId), "Es el último usuario activo que administra usuarios: asigná ese permiso a otra persona antes de cambiarle el rol.");
        if (!ModelState.IsValid) { Existente = existente; return Page(); }

        var cambios = new List<string>();
        if (existente.NombreCompleto != NombreCompleto.Trim()) { cambios.Add("nombre"); existente.NombreCompleto = NombreCompleto.Trim(); }
        if (existente.Email != email) { cambios.Add("email"); existente.Email = email; }
        if (existente.EntrenadorId != EntrenadorId) { cambios.Add("ficha de entrenador"); existente.EntrenadorId = EntrenadorId; }
        if (cambios.Count > 0)
            _db.Auditar(TipoEventoSistema.UsuarioEditado, EntidadAuditada.Usuario, existente.Id, existente.NombreCompleto, "Cambió: " + string.Join(", ", cambios));
        if (rol!.Id != existente.RolId)
        {
            var anterior = existente.Rol.Nombre;
            existente.CambiarRol(rol.Id);   // cambia el sello: la sesión del usuario se renueva con los permisos nuevos
            _db.Auditar(TipoEventoSistema.RolCambiado, EntidadAuditada.Usuario, existente.Id, existente.NombreCompleto, $"{anterior} → {rol.Nombre}");
            cambios.Add("rol");
        }
        await _db.SaveChangesAsync();
        TempData["Flash"] = cambios.Count > 0 ? "Cambios guardados." : "No había cambios para guardar.";
        return RedirectToPage("Detalle", new { id = existente.Id });
    }

    private async Task CargarListasAsync()
    {
        Roles = await _db.Roles.AsNoTracking().Include(r => r.Permisos).Include(r => r.Usuarios)
            .OrderByDescending(r => r.AccesoTotal).ThenBy(r => r.Nombre).ToListAsync();
        Entrenadores = await _db.Entrenadores.AsNoTracking().OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToListAsync();
    }
}
