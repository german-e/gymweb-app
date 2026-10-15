using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas;

[RequierePermiso(Permisos.InventarioAdministrar)]
public class MovimientosModel : PageModel
{
    private readonly GymDbContext _db;
    public MovimientosModel(GymDbContext db) => _db = db;

    public const int Maximo = 300;

    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }
    [BindProperty(SupportsGet = true)] public TipoMovimientoStock? Tipo { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta { get; set; }
    [BindProperty(SupportsGet = true)] public string? Usuario { get; set; }

    public bool HayFiltros => Producto.HasValue || Tipo.HasValue || Desde.HasValue || Hasta.HasValue || !string.IsNullOrEmpty(Usuario);

    public List<MovimientoStock> Movimientos { get; private set; } = new();
    public int Total { get; private set; }
    public int Entradas { get; private set; }
    public int Salidas { get; private set; }
    public List<(int Id, string Nombre)> Productos { get; private set; } = new();
    public List<string> Usuarios { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var q = _db.MovimientosStock.AsNoTracking().AsQueryable();
        if (Producto is { } pid) q = q.Where(m => m.ProductoId == pid);
        if (Tipo is { } t) q = q.Where(m => m.Tipo == t);
        if (Desde is { } d) { var u = GymCore.Web.Reportes.Periodo.InicioUtc(d); q = q.Where(m => m.Fecha >= u); }
        if (Hasta is { } h) { var u = GymCore.Web.Reportes.Periodo.InicioUtc(h.AddDays(1)); q = q.Where(m => m.Fecha < u); }
        if (!string.IsNullOrWhiteSpace(Usuario)) q = q.Where(m => m.Usuario == Usuario);

        Total = await q.CountAsync();
        Entradas = await q.Where(m => m.Cantidad > 0).SumAsync(m => (int?)m.Cantidad) ?? 0;
        Salidas = -(await q.Where(m => m.Cantidad < 0).SumAsync(m => (int?)m.Cantidad) ?? 0);
        Movimientos = await q.Include(m => m.Producto)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Take(Maximo).ToListAsync();

        Productos = (await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new { p.Id, p.Nombre }).ToListAsync())
            .Select(p => (p.Id, p.Nombre)).ToList();
        Usuarios = await _db.MovimientosStock.AsNoTracking().Where(m => m.Usuario != null).Select(m => m.Usuario!).Distinct().OrderBy(u => u).ToListAsync();
    }
}
