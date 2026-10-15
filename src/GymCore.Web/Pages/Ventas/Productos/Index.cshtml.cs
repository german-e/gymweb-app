using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas.Productos;

[RequierePermiso(Permisos.ProductosVer)]
public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ReportesComerciales _reportes;
    public IndexModel(GymDbContext db, ReportesComerciales reportes) { _db = db; _reportes = reportes; }

    public record Fila(Producto Producto, int VendidasMes);

    public List<Fila> Filas { get; private set; } = new();
    public List<CategoriaProducto> Categorias { get; private set; } = new();

    public int Activos => Filas.Count(f => f.Producto.Activo);
    public int StockBajo => Filas.Count(f => f.Producto.Activo && f.Producto.Estado is EstadoStock.Critico or EstadoStock.ProximoAlMinimo);
    public int SinStock => Filas.Count(f => f.Producto.Activo && f.Producto.Estado == EstadoStock.SinStock);
    public decimal VentasHoy { get; private set; }
    public int TicketsHoy { get; private set; }

    // Filtro inicial por URL: ?f=activo|bajo|sinstock|inactivo y ?categoria=Id
    [BindProperty(SupportsGet = true)] public string? F { get; set; }
    [BindProperty(SupportsGet = true)] public int? Categoria { get; set; }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        var productos = await _db.Productos.AsNoTracking().Include(p => p.Categoria)
            .OrderBy(p => p.Nombre).ToListAsync();
        var (desde, _) = GymCore.Web.Reportes.Periodo.MesActualHastaHoy(DateOnly.FromDateTime(DateTime.Now));
        var desdeUtc = GymCore.Web.Reportes.Periodo.InicioUtc(desde);
        var vendidas = await _db.VentaItems.AsNoTracking()
            .Where(i => !i.Venta.Anulada && i.Venta.Fecha >= desdeUtc)
            .GroupBy(i => i.ProductoId)
            .Select(g => new { g.Key, Cant = g.Sum(i => i.Cantidad) })
            .ToDictionaryAsync(x => x.Key, x => x.Cant);
        Filas = productos.Select(p => new Fila(p, vendidas.GetValueOrDefault(p.Id))).ToList();
        Categorias = await _db.CategoriasProducto.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        (VentasHoy, TicketsHoy) = await _reportes.DelDiaAsync(DateOnly.FromDateTime(DateTime.Now));
    }

    // Activar / desactivar (un producto con ventas nunca se borra: se desactiva y deja de ofrecerse)
    [RequierePermiso(Permisos.ProductosEditar)]
    public async Task<IActionResult> OnPostToggleEstadoAsync(int id, string? volver)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        p.Activo = !p.Activo;
        _db.Auditar(p.Activo ? TipoEventoSistema.ProductoActivado : TipoEventoSistema.ProductoDesactivado, EntidadAuditada.Producto, p.Id, p.Nombre,
            p.Activo ? "Disponible otra vez para la venta" : $"Ya no se ofrece en el Punto de Venta · stock {p.StockActual}");
        await _db.SaveChangesAsync();
        Flash = p.Activo ? $"{p.Nombre} está activo y vuelve a aparecer en el Punto de Venta." : $"{p.Nombre} fue desactivado. Su historial de ventas y movimientos se conserva.";
        return volver == "detalle" ? RedirectToPage("Detalle", new { id }) : RedirectToPage();
    }
}
