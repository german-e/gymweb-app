using GymCore.Web.Seguridad;
using System.Text;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Administracion;

// Administración > Auditoría: timeline filtrable de todos los eventos del sistema (solo lectura).
[RequierePermiso(Permisos.AuditoriaVer)]
public class AuditoriaModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    public AuditoriaModel(GymDbContext db, IUsuarioActual usuario) { _db = db; _usuario = usuario; }

    public const int PorPagina = 50;

    // Filtros (GET, compartibles por URL)
    [BindProperty(SupportsGet = true)] public DateTime? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Hasta { get; set; }
    [BindProperty(SupportsGet = true)] public TipoEventoSistema? Tipo { get; set; }
    [BindProperty(SupportsGet = true)] public EntidadAuditada? Entidad { get; set; }
    [BindProperty(SupportsGet = true)] public string? Usuario { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;

    public List<EventoSistema> Eventos { get; private set; } = new();
    public int Total { get; private set; }
    public int Paginas => Math.Max(1, (int)Math.Ceiling(Total / (double)PorPagina));
    public List<string> Usuarios { get; private set; } = new();
    public bool HayFiltros => Desde.HasValue || Hasta.HasValue || Tipo.HasValue || Entidad.HasValue || !string.IsNullOrEmpty(Usuario) || !string.IsNullOrEmpty(Q);

    // KPIs
    public int EventosHoy { get; private set; }
    public int Eventos7Dias { get; private set; }
    public int Operadores30Dias { get; private set; }
    public int AnulacionesMes { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {

        var q = Filtrar();
        Total = await q.CountAsync();
        Pagina = Math.Clamp(Pagina, 1, Paginas);
        Eventos = await q.OrderByDescending(e => e.FechaUtc).ThenByDescending(e => e.Id)
            .Skip((Pagina - 1) * PorPagina).Take(PorPagina).ToListAsync();

        Usuarios = await _db.EventosSistema.AsNoTracking().Where(e => e.Usuario != null)
            .Select(e => e.Usuario!).Distinct().OrderBy(u => u).ToListAsync();

        var hoyUtc = DateTime.Today.ToUniversalTime();
        var mesUtc = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToUniversalTime();
        var todos = _db.EventosSistema.AsNoTracking();
        EventosHoy = await todos.CountAsync(e => e.FechaUtc >= hoyUtc);
        Eventos7Dias = await todos.CountAsync(e => e.FechaUtc >= hoyUtc.AddDays(-6));
        Operadores30Dias = await todos.Where(e => e.FechaUtc >= hoyUtc.AddDays(-29) && e.Usuario != null).Select(e => e.Usuario).Distinct().CountAsync();
        AnulacionesMes = await todos.CountAsync(e => e.Tipo == TipoEventoSistema.PagoAnulado && e.FechaUtc >= mesUtc);
        return Page();
    }

    // Exporta los eventos filtrados (hasta 10.000) para revisiones externas
    public async Task<IActionResult> OnGetCsvAsync()
    {
        var eventos = await Filtrar().OrderByDescending(e => e.FechaUtc).Take(10_000).ToListAsync();
        var sb = new StringBuilder("Fecha;Hora;Usuario;Accion;Entidad;Id;Nombre;Resumen;Detalle\n");
        string C(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        foreach (var e in eventos)
        {
            var l = e.FechaUtc.ToLocalTime();
            sb.AppendLine(string.Join(';', C(l.ToString("dd/MM/yyyy")), C(l.ToString("HH:mm:ss")), C(e.Usuario),
                C(e.Tipo.Estilo().Texto), C(e.Entidad.Texto()), C(e.EntidadId?.ToString()), C(e.EntidadNombre), C(e.Resumen), C(e.Detalle)));
        }
        // BOM para que Excel abra bien los acentos
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"auditoria-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    private IQueryable<EventoSistema> Filtrar()
    {
        var q = _db.EventosSistema.AsNoTracking();
        if (Desde is { } d) { var u = d.Date.ToUniversalTime(); q = q.Where(e => e.FechaUtc >= u); }
        if (Hasta is { } h) { var u = h.Date.AddDays(1).ToUniversalTime(); q = q.Where(e => e.FechaUtc < u); }
        if (Tipo is { } t) q = q.Where(e => e.Tipo == t);
        if (Entidad is { } en) q = q.Where(e => e.Entidad == en);
        if (!string.IsNullOrEmpty(Usuario)) q = q.Where(e => e.Usuario == Usuario);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var patron = $"%{Q.Trim()}%";
            q = q.Where(e => EF.Functions.Like(e.EntidadNombre, patron) || EF.Functions.Like(e.Resumen!, patron) || EF.Functions.Like(e.Detalle!, patron));
        }
        return q;
    }

    public string? UrlPagina(int pagina) => Url.Page("Auditoria", new
    {
        Desde = Desde?.ToString("yyyy-MM-dd"), Hasta = Hasta?.ToString("yyyy-MM-dd"), Tipo, Entidad, Usuario, Q, Pagina = pagina
    });
}
