using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GymCore.Web.Marca;

// Identidad visual lista para las vistas: nombres, imágenes con respaldo y tokens de color ya calculados.
public sealed record Branding(
    string Nombre, string Eslogan, string? Logo, string? LogoReducido, string? Favicon, string? ImagenBienvenida,
    string ColorPrincipal, string ColorSecundario,
    Colores.Rgb Primario, Colores.Rgb PrimarioFuerte, Colores.Rgb Secundario, Colores.Rgb TextoSobrePrimario,
    bool TonoAjustado,
    string? Direccion, string? Telefono, string? Email, string? SitioWeb, string TextoCredencial,
    ConfiguracionKiosco Kiosco)
{
    public string Iniciales => string.Concat(Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
    public string? Isotipo => LogoReducido ?? Logo;
    public string Contacto => string.Join(" · ", new[] { Direccion, Telefono, SitioWeb }.Where(x => !string.IsNullOrWhiteSpace(x)));

    // Variables CSS que reemplazan la paleta por defecto en toda la aplicación
    public string VariablesCss =>
        $"--primary:{Primario.Hex};--primary-rgb:{Primario.Css};--primary-strong:{PrimarioFuerte.Hex};" +
        $"--primary-soft:rgb({Primario.Css} / 0.12);--secondary:{Secundario.Hex};--secondary-rgb:{Secundario.Css};" +
        $"--on-primary:{TextoSobrePrimario.Hex};" +
        (EsPaletaPorDefecto ? "" : $"--cred-brand-a:{PrimarioFuerte.Hex};--cred-brand-b:{Secundario.Hex};");

    // Sin colores propios se conserva exactamente la paleta original del producto
    public bool EsPaletaPorDefecto => ColorPrincipal == ConfiguracionGimnasio.ColorPrincipalPorDefecto
                                      && ColorSecundario == ConfiguracionGimnasio.ColorSecundarioPorDefecto;
}

public sealed record ConfiguracionKiosco(string Mensaje, int SegundosResultado, bool Sonido, bool PantallaEspera,
    int SegundosInactividad, int SegundosRotacion);

// Resuelve el branding del gimnasio actual. Cacheado en memoria (se invalida al guardar).
// Punto único de extensión para multi gimnasio / white label: elegir la configuración según el tenant del request.
public class ServicioBranding
{
    private readonly GymDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private const string Clave = "GymCore.Branding";

    public ServicioBranding(GymDbContext db, IMemoryCache cache, IConfiguration config) { _db = db; _cache = cache; _config = config; }

    public const string MensajeKioscoPorDefecto = "Escanee su credencial";
    public const string EsloganPorDefecto = "Club Management";
    public const string TextoCredencialPorDefecto = "Presentá esta credencial al ingresar";

    public async Task<Branding> ActualAsync()
    {
        if (_cache.TryGetValue(Clave, out Branding? b) && b is not null) return b;
        var cfg = await _db.ConfiguracionesGimnasio.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync();
        b = Construir(cfg ?? PorDefecto());
        _cache.Set(Clave, b, TimeSpan.FromMinutes(10));
        return b;
    }

    // Fila editable (la crea con los valores por defecto la primera vez que se guarda)
    public async Task<ConfiguracionGimnasio> EditableAsync()
    {
        var cfg = await _db.ConfiguracionesGimnasio.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (cfg is null)
        {
            cfg = PorDefecto();
            _db.ConfiguracionesGimnasio.Add(cfg);
        }
        return cfg;
    }

    public void Invalidar() => _cache.Remove(Clave);

    // Compatibilidad: bases sin configuración toman el nombre de appsettings (Gimnasio:Nombre)
    public ConfiguracionGimnasio PorDefecto() => new()
    {
        NombreComercial = string.IsNullOrWhiteSpace(_config["Gimnasio:Nombre"]) ? "GymCore" : _config["Gimnasio:Nombre"]!.Trim()
    };

    public static Branding Construir(ConfiguracionGimnasio c)
    {
        var principal = Colores.Normalizar(c.ColorPrincipal) ?? ConfiguracionGimnasio.ColorPrincipalPorDefecto;
        var secundario = Colores.Normalizar(c.ColorSecundario) ?? ConfiguracionGimnasio.ColorSecundarioPorDefecto;
        var original = Colores.Parse(principal);
        var primario = Colores.ConContrasteSobreBlanco(original, 3.0);       // relleno, íconos, bordes activos
        var fuerte = principal == ConfiguracionGimnasio.ColorPrincipalPorDefecto
            ? Colores.Parse("#1D4ED8")                                                    // tono original del producto
            : Colores.ConContrasteSobreBlanco(Colores.Mezclar(primario, 0.12), 4.5);       // texto y hover
        var sec = Colores.ConContrasteSobreBlanco(Colores.Parse(secundario), 3.0);
        return new Branding(
            string.IsNullOrWhiteSpace(c.NombreComercial) ? "GymCore" : c.NombreComercial.Trim(),
            string.IsNullOrWhiteSpace(c.Eslogan) ? EsloganPorDefecto : c.Eslogan.Trim(),
            Url(c.LogoUrl), Url(c.LogoReducidoUrl), Url(c.FaviconUrl), Url(c.ImagenBienvenidaUrl),
            principal, secundario, primario, fuerte, sec, Colores.TextoSobre(primario), primario != original,
            Texto(c.Direccion), Texto(c.Telefono), Texto(c.Email), Texto(c.SitioWeb),
            Texto(c.TextoCredencial) ?? TextoCredencialPorDefecto,
            new ConfiguracionKiosco(
                Texto(c.KioscoMensaje) ?? MensajeKioscoPorDefecto,
                Math.Clamp(c.KioscoSegundosResultado, 2, 30), c.KioscoSonido, c.KioscoPantallaEspera,
                Math.Clamp(c.KioscoSegundosInactividad, 10, 600), Math.Clamp(c.KioscoSegundosRotacion, 3, 60)));
    }

    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Url(string? s) => string.IsNullOrWhiteSpace(s) ? null : "/" + s.TrimStart('/');
}
