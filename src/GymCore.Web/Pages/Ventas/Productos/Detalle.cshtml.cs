using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas.Productos;

[RequierePermiso(Permisos.ProductosVer)]
public class DetalleModel : PageModel
{
    private readonly GymDbContext _db;
    public DetalleModel(GymDbContext db) => _db = db;

    public Producto Producto { get; private set; } = null!;
    public List<MovimientoStock> Movimientos { get; private set; } = new();
    public List<EventoSistema> Eventos { get; private set; } = new();

    public record VentaFila(int VentaId, DateTime Fecha, string Cliente, int? SocioId, int Cantidad, decimal Importe, bool Anulada);
    public List<VentaFila> Ventas { get; private set; } = new();

    // Últimos 30 días (ventas no anuladas)
    public int Unidades30 { get; private set; }
    public decimal Importe30 { get; private set; }
    public decimal Margen30 { get; private set; }
    public decimal? DiasCobertura => Unidades30 > 0 ? Math.Round(Producto.StockActual / (Unidades30 / 30m), 0) : null;

    public const int MaxMovimientos = 100;

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await _db.Productos.AsNoTracking().Include(x => x.Categoria).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        Producto = p;

        Movimientos = await _db.MovimientosStock.AsNoTracking()
            .Where(m => m.ProductoId == id)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Take(MaxMovimientos).ToListAsync();

        var ventas = await _db.VentaItems.AsNoTracking()
            .Where(i => i.ProductoId == id)
            .OrderByDescending(i => i.Venta.Fecha)
            .Take(50)
            .Select(i => new
            {
                i.VentaId, i.Venta.Fecha, i.Venta.SocioId, SocioNombre = i.Venta.Socio != null ? i.Venta.Socio.Apellido + ", " + i.Venta.Socio.Nombre : null,
                i.Venta.ClienteNombre, i.Cantidad, i.PrecioUnitario, i.Venta.Anulada
            }).ToListAsync();
        Ventas = ventas.Select(v => new VentaFila(v.VentaId, v.Fecha, v.SocioNombre ?? v.ClienteNombre ?? "Cliente ocasional", v.SocioId,
            v.Cantidad, v.Cantidad * v.PrecioUnitario, v.Anulada)).ToList();

        var desde = DateTime.UtcNow.AddDays(-30);
        var ult = await _db.VentaItems.AsNoTracking()
            .Where(i => i.ProductoId == id && !i.Venta.Anulada && i.Venta.Fecha >= desde)
            .Select(i => new { i.Cantidad, i.PrecioUnitario, i.CostoUnitario }).ToListAsync();
        Unidades30 = ult.Sum(i => i.Cantidad);
        Importe30 = ult.Sum(i => i.Cantidad * i.PrecioUnitario);
        Margen30 = ult.Sum(i => i.Cantidad * (i.PrecioUnitario - i.CostoUnitario));

        Eventos = await _db.EventosSistema.AsNoTracking()
            .Where(e => e.Entidad == EntidadAuditada.Producto && e.EntidadId == id)
            .OrderByDescending(e => e.FechaUtc).Take(30).ToListAsync();
        return Page();
    }
}
