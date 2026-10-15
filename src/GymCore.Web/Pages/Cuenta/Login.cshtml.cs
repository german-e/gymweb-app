using System.ComponentModel.DataAnnotations;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Cuenta;

public class LoginModel : PageModel
{
    private readonly Autenticacion _auth;
    public LoginModel(Autenticacion auth) => _auth = auth;

    [BindProperty, Required(ErrorMessage = "Ingresá tu usuario."), Display(Name = "Usuario")]
    public string NombreUsuario { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Ingresá tu contraseña."), DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    [BindProperty] public bool Recordar { get; set; }

    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }
    public bool Bloqueado { get; private set; }
    [TempData] public string? Mensaje { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _auth.HayUsuariosAsync()) return RedirectToPage("Configurar");
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(Destino());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var r = await _auth.IniciarSesionAsync(NombreUsuario, Password, Recordar);
        switch (r.Resultado)
        {
            case ResultadoLogin.Exitoso:
                return r.Usuario!.DebeCambiarPassword
                    ? RedirectToPage("CambiarPassword", new { obligatorio = true, returnUrl = Destino() })
                    : LocalRedirect(Destino());
            case ResultadoLogin.Bloqueado:
                Bloqueado = true;
                var minutos = Math.Max(1, (int)Math.Ceiling(((r.BloqueadoHasta ?? DateTime.UtcNow) - DateTime.UtcNow).TotalMinutes));
                Error = $"Por seguridad, la cuenta quedó bloqueada temporalmente por demasiados intentos fallidos. Probá de nuevo en {minutos} {(minutos == 1 ? "minuto" : "minutos")} o pedí a un administrador que restablezca tu contraseña.";
                break;
            case ResultadoLogin.Inactivo:
                Error = "Tu usuario está desactivado. Consultá con un administrador.";
                break;
            default:
                // Mismo mensaje exista o no el usuario: no revela qué cuentas hay
                Error = "Usuario o contraseña incorrectos.";
                break;
        }
        Password = "";
        return Page();
    }

    private string Destino() => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("/Panel")!;
}
