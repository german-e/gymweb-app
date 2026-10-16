using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Marca;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Configuracion;

// Kiosco de ingreso: mensajes, tiempos y pantalla de espera con avisos, promociones y eventos que rotan.
[RequierePermiso(Permisos.ConfiguracionAdministrar)]
public class RecepcionModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ServicioBranding _branding;
    private readonly IWebHostEnvironment _env;
    private readonly IUsuarioActual _usuario;
    public RecepcionModel(GymDbContext db, ServicioBranding branding, IWebHostEnvironment env, IUsuarioActual usuario)
    { _db = db; _branding = branding; _env = env; _usuario = usuario; }

    public ConfiguracionGimnasio Config { get; private set; } = null!;
    public List<AvisoRecepcion> Avisos { get; private set; } = new();

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        Config = await _branding.EditableAsync();
        Avisos = await _db.AvisosRecepcion.AsNoTracking().OrderBy(a => a.Orden).ThenByDescending(a => a.Id).ToListAsync();
    }

    public async Task<IActionResult> OnPostKioscoAsync(string? mensaje, int segundosResultado, bool sonido, bool pantallaEspera,
        int segundosInactividad, int segundosRotacion)
    {
        var c = await _branding.EditableAsync();
        c.KioscoMensaje = string.IsNullOrWhiteSpace(mensaje) ? null : mensaje.Trim()[..Math.Min(80, mensaje.Trim().Length)];
        c.KioscoSegundosResultado = Math.Clamp(segundosResultado, 2, 30);
        c.KioscoSonido = sonido;
        c.KioscoPantallaEspera = pantallaEspera;
        c.KioscoSegundosInactividad = Math.Clamp(segundosInactividad, 10, 600);
        c.KioscoSegundosRotacion = Math.Clamp(segundosRotacion, 3, 60);
        c.FechaModificacion = DateTime.UtcNow;
        c.UsuarioModifico = _usuario.Nombre;
        _db.Auditar(TipoEventoSistema.KioscoConfigurado, EntidadAuditada.Configuracion, c.Id, c.NombreComercial,
            $"Resultado {c.KioscoSegundosResultado} s · sonido {(c.KioscoSonido ? "sí" : "no")} · pantalla de espera {(c.KioscoPantallaEspera ? $"sí ({c.KioscoSegundosInactividad} s / rota cada {c.KioscoSegundosRotacion} s)" : "no")}",
            referencia: c.Id == 0 ? c : null);
        await _db.SaveChangesAsync();
        _branding.Invalidar();
        Flash = "Configuración del kiosco guardada. Los kioscos abiertos la toman al recargar.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAvisoAsync(int? id, TipoAvisoRecepcion tipo, string? titulo, string? texto, DateTime? fechaEvento,
        DateOnly? desde, DateOnly? hasta, int orden, IFormFile? imagen, bool quitarImagen)
    {
        titulo = titulo?.Trim();
        if (string.IsNullOrEmpty(titulo)) { FlashError = "El aviso necesita un título."; return RedirectToPage(); }
        if (!Enum.IsDefined(tipo)) tipo = TipoAvisoRecepcion.Aviso;
        if (desde is { } d && hasta is { } h && h < d) { FlashError = "La fecha de fin no puede ser anterior a la de inicio."; return RedirectToPage(); }
        if (await ImagenesMarca.ValidarAsync(imagen, ImagenesMarca.Uso.Aviso) is { } error) { FlashError = error; return RedirectToPage(); }

        AvisoRecepcion a;
        if (id is null)
        {
            a = new AvisoRecepcion();
            _db.AvisosRecepcion.Add(a);
        }
        else
        {
            var existente = await _db.AvisosRecepcion.FirstOrDefaultAsync(x => x.Id == id);
            if (existente is null) return NotFound();
            a = existente;
        }
        a.Tipo = tipo;
        a.Titulo = titulo.Length > 80 ? titulo[..80] : titulo;
        a.Texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()[..Math.Min(300, texto.Trim().Length)];
        a.FechaEvento = tipo == TipoAvisoRecepcion.Evento ? fechaEvento : null;
        a.VigenteDesde = desde;
        a.VigenteHasta = hasta;
        a.Orden = Math.Clamp(orden, 0, 999);
        a.ImagenUrl = (await FotoStorage.ProcesarAsync(_env, ImagenesMarca.Carpeta, a.ImagenUrl, imagen, quitarImagen)).Url;
        _db.Auditar(id is null ? TipoEventoSistema.AvisoCreado : TipoEventoSistema.AvisoEditado, EntidadAuditada.Aviso, a.Id, a.Titulo,
            $"{Texto(a.Tipo)}{(a.VigenteHasta is { } vh ? $" · hasta {vh:dd/MM/yyyy}" : "")}", referencia: id is null ? a : null);
        await _db.SaveChangesAsync();
        Flash = id is null ? $"Aviso “{a.Titulo}” agregado a la pantalla de espera." : $"Aviso “{a.Titulo}” actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAvisoAsync(int id)
    {
        var a = await _db.AvisosRecepcion.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        a.Activo = !a.Activo;
        _db.Auditar(TipoEventoSistema.AvisoEditado, EntidadAuditada.Aviso, a.Id, a.Titulo, a.Activo ? "Aviso activado" : "Aviso pausado");
        await _db.SaveChangesAsync();
        Flash = a.Activo ? $"“{a.Titulo}” vuelve a mostrarse." : $"“{a.Titulo}” quedó pausado.";
        return RedirectToPage();
    }

    // Los avisos son contenido de pantalla (no datos operativos): se pueden eliminar, con registro en auditoría
    public async Task<IActionResult> OnPostEliminarAvisoAsync(int id)
    {
        var a = await _db.AvisosRecepcion.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        _db.AvisosRecepcion.Remove(a);
        _db.Auditar(TipoEventoSistema.AvisoEliminado, EntidadAuditada.Aviso, a.Id, a.Titulo, Texto(a.Tipo));
        await _db.SaveChangesAsync();
        FotoStorage.Borrar(_env, a.ImagenUrl);
        Flash = $"Aviso “{a.Titulo}” eliminado.";
        return RedirectToPage();
    }

    public static string Texto(TipoAvisoRecepcion t) => t switch
    {
        TipoAvisoRecepcion.Promocion => "Promoción", TipoAvisoRecepcion.Evento => "Evento", _ => "Aviso"
    };

    public static (string Icono, string Variante) Estilo(TipoAvisoRecepcion t) => t switch
    {
        TipoAvisoRecepcion.Promocion => ("bi-tag", "warning"), TipoAvisoRecepcion.Evento => ("bi-calendar-event", "success"), _ => ("bi-megaphone", "primary")
    };
}
