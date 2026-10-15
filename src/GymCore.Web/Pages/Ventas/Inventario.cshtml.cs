using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas;

[RequierePermiso(Permisos.ProductosVer)]
public class InventarioModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ServicioInventario _inventario;
    public InventarioModel(GymDbContext db, ServicioInventario inventario) { _db = db; _inventario = inventario; }

    public List<Producto> Productos { get; private set; } = new();
    public ServicioInventario.ResumenStock Resumen { get; private set; } = ServicioInventario.ResumenStock.Vacio;

    public record Valorizacion(string Categoria, string Icono, int Unidades, decimal Costo, decimal Venta);
    public List<Valorizacion> PorCategoria { get; private set; } = new();
    public decimal ValorCosto => PorCategoria.Sum(v => v.Costo);
    public decimal ValorVenta => PorCategoria.Sum(v => v.Venta);
    public int Unidades => PorCategoria.Sum(v => v.Unidades);

    // ?f=sinstock|critico|proximo|todos (por defecto: los que requieren atención)
    [BindProperty(SupportsGet = true)] public string? F { get; set; }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        Productos = await _db.Productos.AsNoTracking().Include(p => p.Categoria).Where(p => p.Activo).ToListAsync();
        Productos = Productos.OrderByDescending(p => p.Estado).ThenBy(p => p.StockActual).ThenBy(p => p.Nombre).ToList();
        Resumen = await _inventario.ResumenDelRequestAsync(HttpContext);
        PorCategoria = Productos.GroupBy(p => p.Categoria)
            .Select(g => new Valorizacion(g.Key.Nombre, g.Key.Icono, g.Sum(p => Math.Max(0, p.StockActual)),
                g.Sum(p => Math.Max(0, p.StockActual) * p.PrecioCompra), g.Sum(p => Math.Max(0, p.StockActual) * p.PrecioVenta)))
            .OrderByDescending(v => v.Costo).ToList();
    }

    // Compra, ajuste o corrección desde el modal (cualquier pantalla del módulo)
    [RequierePermiso(Permisos.InventarioAdministrar)]
    public async Task<IActionResult> OnPostAjustarAsync(int productoId, TipoMovimientoStock tipo, int cantidad, string? motivo,
        decimal? costoUnitario, bool actualizarCosto, string? volver)
    {
        var r = await _inventario.AjustarAsync(new ServicioInventario.SolicitudAjuste(productoId, tipo, cantidad, motivo, costoUnitario, actualizarCosto));
        if (r.Ok)
            TempData["Flash"] = $"Movimiento registrado ({InventarioPresentacion.Texto(r.Movimiento!.Tipo).ToLowerInvariant()}): {InventarioPresentacion.ConSigno(r.Movimiento.Cantidad)} · stock actual {r.Movimiento.StockResultante}.";
        else
            TempData["FlashError"] = r.Error;
        return !string.IsNullOrEmpty(volver) && Url.IsLocalUrl(volver) ? LocalRedirect(volver) : RedirectToPage();
    }
}
