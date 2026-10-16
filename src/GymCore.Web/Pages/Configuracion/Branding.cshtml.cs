using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Marca;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Configuracion;

[RequierePermiso(Permisos.ConfiguracionAdministrar)]
public class BrandingModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ServicioBranding _branding;
    private readonly IWebHostEnvironment _env;
    private readonly IUsuarioActual _usuario;
    public BrandingModel(GymDbContext db, ServicioBranding branding, IWebHostEnvironment env, IUsuarioActual usuario)
    { _db = db; _branding = branding; _env = env; _usuario = usuario; }

    [BindProperty] public InputModel Input { get; set; } = new();
    public ConfiguracionGimnasio Actual { get; private set; } = null!;
    public Branding Vista { get; private set; } = null!;

    [TempData] public string? Flash { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresá el nombre comercial."), StringLength(80), Display(Name = "Nombre comercial")]
        public string NombreComercial { get; set; } = "";
        [StringLength(80), Display(Name = "Eslogan o subtítulo")] public string? Eslogan { get; set; }
        [Display(Name = "Color principal")] public string ColorPrincipal { get; set; } = ConfiguracionGimnasio.ColorPrincipalPorDefecto;
        [Display(Name = "Color secundario")] public string ColorSecundario { get; set; } = ConfiguracionGimnasio.ColorSecundarioPorDefecto;
        [StringLength(160), Display(Name = "Dirección")] public string? Direccion { get; set; }
        [StringLength(40), Display(Name = "Teléfono")] public string? Telefono { get; set; }
        [StringLength(120), EmailAddress(ErrorMessage = "El email no es válido."), Display(Name = "Email")] public string? Email { get; set; }
        [StringLength(160), Display(Name = "Sitio web o red social")] public string? SitioWeb { get; set; }
        [StringLength(120), Display(Name = "Texto de la credencial")] public string? TextoCredencial { get; set; }

        public IFormFile? Logo { get; set; }
        public IFormFile? LogoReducido { get; set; }
        public IFormFile? Favicon { get; set; }
        public IFormFile? ImagenBienvenida { get; set; }
        public bool QuitarLogo { get; set; }
        public bool QuitarLogoReducido { get; set; }
        public bool QuitarFavicon { get; set; }
        public bool QuitarImagenBienvenida { get; set; }
    }

    private static readonly IReadOnlyDictionary<string, string> Etiquetas = new Dictionary<string, string>
    {
        ["NombreComercial"] = "nombre comercial", ["Eslogan"] = "eslogan", ["ColorPrincipal"] = "color principal",
        ["ColorSecundario"] = "color secundario", ["LogoUrl"] = "logo", ["LogoReducidoUrl"] = "logo reducido",
        ["FaviconUrl"] = "favicon", ["ImagenBienvenidaUrl"] = "imagen de bienvenida", ["Direccion"] = "dirección",
        ["Telefono"] = "teléfono", ["Email"] = "email", ["SitioWeb"] = "sitio web", ["TextoCredencial"] = "texto de la credencial"
    };

    public async Task OnGetAsync()
    {
        Actual = await _branding.EditableAsync();
        Input = new InputModel
        {
            NombreComercial = Actual.NombreComercial, Eslogan = Actual.Eslogan, ColorPrincipal = Actual.ColorPrincipal,
            ColorSecundario = Actual.ColorSecundario, Direccion = Actual.Direccion, Telefono = Actual.Telefono, Email = Actual.Email,
            SitioWeb = Actual.SitioWeb, TextoCredencial = Actual.TextoCredencial
        };
        Vista = ServicioBranding.Construir(Actual);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Actual = await _branding.EditableAsync();
        var principal = Colores.Normalizar(Input.ColorPrincipal);
        var secundario = Colores.Normalizar(Input.ColorSecundario);
        if (principal is null) ModelState.AddModelError("Input.ColorPrincipal", "Usá un color hexadecimal, por ejemplo #2563EB.");
        if (secundario is null) ModelState.AddModelError("Input.ColorSecundario", "Usá un color hexadecimal, por ejemplo #4F46E5.");
        foreach (var (archivo, uso, campo) in new[]
                 {
                     (Input.Logo, ImagenesMarca.Uso.Logo, "Input.Logo"), (Input.LogoReducido, ImagenesMarca.Uso.Isotipo, "Input.LogoReducido"),
                     (Input.Favicon, ImagenesMarca.Uso.Favicon, "Input.Favicon"), (Input.ImagenBienvenida, ImagenesMarca.Uso.Bienvenida, "Input.ImagenBienvenida")
                 })
        {
            if (await ImagenesMarca.ValidarAsync(archivo, uso) is { } error) ModelState.AddModelError(campo, error);
        }
        if (!ModelState.IsValid)
        {
            Vista = ServicioBranding.Construir(Actual);
            return Page();
        }

        Actual.NombreComercial = Input.NombreComercial.Trim();
        Actual.Eslogan = Limpio(Input.Eslogan);
        Actual.ColorPrincipal = principal!;
        Actual.ColorSecundario = secundario!;
        Actual.Direccion = Limpio(Input.Direccion);
        Actual.Telefono = Limpio(Input.Telefono);
        Actual.Email = Limpio(Input.Email);
        Actual.SitioWeb = Limpio(Input.SitioWeb);
        Actual.TextoCredencial = Limpio(Input.TextoCredencial);
        Actual.LogoUrl = (await FotoStorage.ProcesarAsync(_env, ImagenesMarca.Carpeta, Actual.LogoUrl, Input.Logo, Input.QuitarLogo)).Url;
        Actual.LogoReducidoUrl = (await FotoStorage.ProcesarAsync(_env, ImagenesMarca.Carpeta, Actual.LogoReducidoUrl, Input.LogoReducido, Input.QuitarLogoReducido)).Url;
        Actual.FaviconUrl = (await FotoStorage.ProcesarAsync(_env, ImagenesMarca.Carpeta, Actual.FaviconUrl, Input.Favicon, Input.QuitarFavicon)).Url;
        Actual.ImagenBienvenidaUrl = (await FotoStorage.ProcesarAsync(_env, ImagenesMarca.Carpeta, Actual.ImagenBienvenidaUrl, Input.ImagenBienvenida, Input.QuitarImagenBienvenida)).Url;

        var nueva = _db.Entry(Actual).State == Microsoft.EntityFrameworkCore.EntityState.Added;
        var cambios = nueva ? Etiquetas.Values.Take(4).ToList() : _db.CamposModificados(Actual, Etiquetas);
        if (cambios.Count == 0 && !nueva)
        {
            Flash = "No había cambios para guardar.";
            return RedirectToPage();
        }
        Actual.FechaModificacion = DateTime.UtcNow;
        Actual.UsuarioModifico = _usuario.Nombre;
        _db.Auditar(TipoEventoSistema.BrandingModificado, EntidadAuditada.Configuracion, Actual.Id, Actual.NombreComercial,
            nueva ? "Identidad del gimnasio configurada" : "Cambió " + string.Join(", ", cambios),
            $"Colores {Actual.ColorPrincipal} / {Actual.ColorSecundario}", referencia: nueva ? Actual : null);
        await _db.SaveChangesAsync();
        _branding.Invalidar();
        Flash = "Identidad visual actualizada. Ya se aplica en menú, login, credenciales, kiosco, tickets y reportes.";
        return RedirectToPage();
    }

    // Restablece la paleta del producto (no toca logos ni datos)
    public async Task<IActionResult> OnPostColoresPorDefectoAsync()
    {
        var cfg = await _branding.EditableAsync();
        cfg.ColorPrincipal = ConfiguracionGimnasio.ColorPrincipalPorDefecto;
        cfg.ColorSecundario = ConfiguracionGimnasio.ColorSecundarioPorDefecto;
        cfg.FechaModificacion = DateTime.UtcNow;
        cfg.UsuarioModifico = _usuario.Nombre;
        _db.Auditar(TipoEventoSistema.BrandingModificado, EntidadAuditada.Configuracion, cfg.Id, cfg.NombreComercial, "Colores restablecidos a los del producto", referencia: cfg.Id == 0 ? cfg : null);
        await _db.SaveChangesAsync();
        _branding.Invalidar();
        Flash = "Se restablecieron los colores originales.";
        return RedirectToPage();
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
