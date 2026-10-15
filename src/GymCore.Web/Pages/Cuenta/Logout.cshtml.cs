using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Cuenta;

// Cierre de sesión: solo por POST (con antiforgery) para que un enlace externo no pueda desloguear al usuario.
public class LogoutModel : PageModel
{
    private readonly Autenticacion _auth;
    private readonly IUsuarioActual _usuario;
    public LogoutModel(Autenticacion auth, IUsuarioActual usuario) { _auth = auth; _usuario = usuario; }

    public IActionResult OnGet() => RedirectToPage("/Panel");

    public async Task<IActionResult> OnPostAsync()
    {
        await _auth.CerrarSesionAsync(_usuario);
        TempData["Mensaje"] = "Cerraste sesión correctamente.";
        return RedirectToPage("Login");
    }
}
