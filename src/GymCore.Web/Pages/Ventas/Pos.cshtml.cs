using System.Text.Json;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas;

// Punto de Venta: productos y, opcionalmente, la cuota del socio en un mismo ticket.
// El navegador solo envía ids y cantidades; precios, stock y totales los resuelve ServicioVentas.
[RequierePermiso(Permisos.VentasCrear)]
public class PosModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly ServicioVentas _ventas;
    private readonly ServicioCobranza _cobranza;
    private readonly ReportesComerciales _reportes;

    public PosModel(GymDbContext db, IUsuarioActual usuario, ServicioVentas ventas, ServicioCobranza cobranza, ReportesComerciales reportes)
    { _db = db; _usuario = usuario; _ventas = ventas; _cobranza = cobranza; _reportes = reportes; }

    public record ProductoPos(int Id, string Nombre, string Codigo, string? Barras, string? Marca, decimal Precio, int Stock,
        int StockMinimo, int CategoriaId, string Icono, bool Destacado);
    public record CategoriaPos(int Id, string Nombre, string Icono, int Productos);

    public List<ProductoPos> Productos { get; private set; } = new();
    public List<CategoriaPos> Categorias { get; private set; } = new();
    public bool PuedeCobrarCuota => _usuario.Puede(Permisos.PagosCrear);
    public decimal VentasHoy { get; private set; }
    public int TicketsHoy { get; private set; }

    // Socio preseleccionado (?socioId= desde la ficha) o reenviado tras un error
    [BindProperty(SupportsGet = true)] public int? SocioId { get; set; }

    // Carrito: [{"id":1,"cantidad":2}, …]
    [BindProperty] public string? Lineas { get; set; }
    [BindProperty] public string? ClienteNombre { get; set; }
    [BindProperty] public MedioPago Medio { get; set; } = MedioPago.Efectivo;
    [BindProperty] public decimal? ImporteCuota { get; set; }
    [BindProperty] public string? Observaciones { get; set; }

    public string? Error { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        List<ServicioVentas.Linea> lineas;
        try
        {
            lineas = (JsonSerializer.Deserialize<List<LineaJson>>(Lineas ?? "[]", Json) ?? new())
                .Select(l => new ServicioVentas.Linea(l.Id, l.Cantidad)).ToList();
        }
        catch (JsonException)
        {
            lineas = new();
        }

        if (ImporteCuota is > 0 && !PuedeCobrarCuota)
            return await ConErrorAsync("Tu usuario no tiene permiso para cobrar cuotas. Quitá la cuota del ticket.");

        var r = await _ventas.RegistrarAsync(new ServicioVentas.Solicitud(lineas, Medio, SocioId, ClienteNombre,
            ImporteCuota is > 0 ? ImporteCuota : null, Observaciones));
        if (!r.Ok) return await ConErrorAsync(r.Error!);

        return RedirectToPage("Ticket", new { id = r.Venta!.Id, nuevo = 1 });
    }

    // Búsqueda de socios para asociar la venta (nombre, apellido, DNI o N° de socio)
    public async Task<IActionResult> OnGetSociosAsync(string? term)
    {
        term = (term ?? "").Trim();
        if (term.Length < 2) return new JsonResult(Array.Empty<object>());
        var q = _db.Socios.AsNoTracking().Where(s => s.FechaBaja == null);
        q = int.TryParse(term, out var n)
            ? q.Where(s => s.Dni.Contains(term) || s.NumeroSocio == n || s.TarjetaCodigo == term)
            : q.Where(s => s.Apellido.Contains(term) || s.Nombre.Contains(term) || (s.Apellido + " " + s.Nombre).Contains(term) || (s.Nombre + " " + s.Apellido).Contains(term));
        var items = await q.OrderBy(s => s.Apellido).ThenBy(s => s.Nombre).Take(8)
            .Select(s => new { id = s.Id, nombre = s.Apellido + ", " + s.Nombre, dni = s.Dni, numero = s.NumeroSocio ?? s.Id })
            .ToListAsync();
        return new JsonResult(items);
    }

    // Ficha breve del socio para el ticket: plan, vencimiento, última asistencia y saldo de la cuota
    public async Task<IActionResult> OnGetSocioAsync(int id)
    {
        var s = await _cobranza.SocioConSuscripcionesAsync(id);
        if (s is null) return new JsonResult(new { ok = false });
        var saldo = await _cobranza.SaldoAsync(s);
        var ultima = await _db.Asistencias.AsNoTracking().Where(a => a.SocioId == id)
            .OrderByDescending(a => a.FechaHoraIngreso).Select(a => (DateTime?)a.FechaHoraIngreso).FirstOrDefaultAsync();
        var ultimaSuscripcion = s.Suscripciones.OrderByDescending(sp => sp.FechaFin).FirstOrDefault();
        var vigente = ServicioCobranza.SuscripcionVigente(s, DateTime.Now);
        return new JsonResult(new
        {
            ok = true,
            id = s.Id,
            nombre = $"{s.Nombre} {s.Apellido}",
            iniciales = s.Iniciales,
            foto = s.FotoUrl,
            numero = s.NumeroVisible,
            dni = s.Dni,
            baja = s.DadoDeBaja,
            plan = (vigente ?? ultimaSuscripcion)?.Plan.Nombre,
            vigente = vigente is not null,
            vence = (vigente ?? ultimaSuscripcion)?.FechaFin.ToString("dd/MM/yyyy"),
            ultimaAsistencia = ultima is { } u ? Relativo(u) : null,
            precio = saldo?.Precio,
            faltante = saldo?.Faltante,
            puedeCobrarCuota = PuedeCobrarCuota && !s.DadoDeBaja
        });
    }

    private async Task<IActionResult> ConErrorAsync(string error)
    {
        Error = error;
        await CargarAsync();
        return Page();
    }

    private async Task CargarAsync()
    {
        var productos = await _db.Productos.AsNoTracking().Include(p => p.Categoria)
            .Where(p => p.Activo)
            .OrderByDescending(p => p.Destacado).ThenBy(p => p.Nombre)
            .ToListAsync();
        Productos = productos.Select(p => new ProductoPos(p.Id, p.Nombre, p.Codigo, p.CodigoBarras, p.Marca, p.PrecioVenta, p.StockActual,
            p.StockMinimo, p.CategoriaId, p.Categoria.Icono, p.Destacado)).ToList();
        Categorias = productos.GroupBy(p => p.Categoria)
            .Select(g => new CategoriaPos(g.Key.Id, g.Key.Nombre, g.Key.Icono, g.Count()))
            .OrderBy(c => c.Nombre).ToList();
        (VentasHoy, TicketsHoy) = await _reportes.DelDiaAsync(DateOnly.FromDateTime(DateTime.Now));
    }

    // Hace cuánto vino (UTC → texto local)
    private static string Relativo(DateTime utc)
    {
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        var dias = (DateTime.Today - local.Date).Days;
        return dias switch
        {
            0 => $"Hoy {local:HH:mm}",
            1 => $"Ayer {local:HH:mm}",
            < 30 => $"Hace {dias} días ({local:dd/MM})",
            _ => local.ToString("dd/MM/yyyy")
        };
    }

    private sealed record LineaJson(int Id, int Cantidad);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
}
