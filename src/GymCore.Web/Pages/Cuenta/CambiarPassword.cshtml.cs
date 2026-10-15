using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Cuenta;

// Cambio de la propia contraseña. Obligatorio tras un alta o un reseteo (contraseña temporal).
public class CambiarPasswordModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly Autenticacion _auth;
    public CambiarPasswordModel(GymDbContext db, IUsuarioActual usuario, Autenticacion auth) { _db = db; _usuario = usuario; _auth = auth; }

    [BindProperty(SupportsGet = true)] public bool Obligatorio { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    [BindProperty, Required(ErrorMessage = "Ingresá tu contraseña actual."), DataType(DataType.Password), Display(Name = "Contraseña actual")]
    public string Actual { get; set; } = "";

    [BindProperty, DataType(DataType.Password), Display(Name = "Nueva contraseña")]
    public string Nueva { get; set; } = "";

    [BindProperty, DataType(DataType.Password), Display(Name = "Repetir nueva contraseña")]
    public string Confirmacion { get; set; } = "";

    public string NombreUsuario => _usuario.NombreUsuario;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == _usuario.UsuarioId);
        if (u is null) return RedirectToPage("Login");

        if (!string.IsNullOrEmpty(Actual) && Autenticacion.Verificar(u, Actual) == PasswordVerificationResult.Failed)
            ModelState.AddModelError(nameof(Actual), "La contraseña actual no es correcta.");
        foreach (var e in PoliticaPassword.Validar(Nueva, u.NombreUsuario)) ModelState.AddModelError(nameof(Nueva), e);
        if (Nueva == Actual && !string.IsNullOrEmpty(Nueva)) ModelState.AddModelError(nameof(Nueva), "Tiene que ser distinta de la actual.");
        if (Nueva != Confirmacion) ModelState.AddModelError(nameof(Confirmacion), "Las contraseñas no coinciden.");
        if (!ModelState.IsValid) return Page();

        u.CambiarPassword(Autenticacion.Hash(Nueva));   // cambia el sello: cierra las otras sesiones abiertas
        _db.Auditar(TipoEventoSistema.PasswordCambiada, EntidadAuditada.Usuario, u.Id, u.NombreCompleto,
            Obligatorio ? "Reemplazó la contraseña temporal" : "Cambio voluntario");
        await _db.SaveChangesAsync();
        await _auth.RenovarSesionAsync(u.Id);            // esta sesión sigue abierta con el sello nuevo

        TempData["Flash"] = "Contraseña actualizada. Las otras sesiones abiertas se cerraron.";
        return !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("Perfil");
    }
}
