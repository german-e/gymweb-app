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

// Primer arranque: crea el administrador inicial. Solo está disponible mientras no exista ningún usuario
// (no hay credenciales por defecto que alguien pueda adivinar).
public class ConfigurarModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly Autenticacion _auth;
    public ConfigurarModel(GymDbContext db, Autenticacion auth) { _db = db; _auth = auth; }

    [BindProperty, Required(ErrorMessage = "Ingresá tu nombre."), StringLength(120), Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Elegí un usuario."), RegularExpression(@"^[a-zA-Z0-9._-]{3,60}$", ErrorMessage = "Entre 3 y 60 caracteres: letras, números, punto, guion o guion bajo.")]
    [Display(Name = "Usuario")]
    public string NombreUsuario { get; set; } = "";

    [BindProperty, EmailAddress(ErrorMessage = "Email inválido."), StringLength(160), Display(Name = "Email (opcional)")]
    public string? Email { get; set; }

    [BindProperty, DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    [BindProperty, DataType(DataType.Password), Display(Name = "Repetir contraseña")]
    public string Confirmacion { get; set; } = "";

    public async Task<IActionResult> OnGetAsync() =>
        await _db.Usuarios.AnyAsync() ? RedirectToPage("Login") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (await _db.Usuarios.AnyAsync()) return RedirectToPage("Login");

        foreach (var e in PoliticaPassword.Validar(Password, NombreUsuario)) ModelState.AddModelError(nameof(Password), e);
        if (Password != Confirmacion) ModelState.AddModelError(nameof(Confirmacion), "Las contraseñas no coinciden.");
        if (!ModelState.IsValid) return Page();

        var rol = await _db.Roles.FirstAsync(r => r.AccesoTotal);
        var u = new Usuario(NombreCompleto, NombreUsuario, Autenticacion.Hash(Password), rol.Id, debeCambiarPassword: false)
        {
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim()
        };
        _db.Usuarios.Add(u);
        var ev = _db.Auditar(TipoEventoSistema.UsuarioCreado, EntidadAuditada.Usuario, null, u.NombreCompleto,
            $"Administrador inicial · @{u.NombreUsuario} · rol {rol.Nombre}", referencia: u);
        ev.Usuario = "Configuración inicial";
        await _db.SaveChangesAsync();

        await _auth.IniciarSesionAsync(NombreUsuario, Password, recordar: false);
        TempData["Flash"] = $"¡Listo, {u.NombreCompleto}! Ya podés crear los usuarios del equipo desde Administración › Usuarios.";
        return RedirectToPage("/Panel");
    }
}
