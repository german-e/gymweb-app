using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Reportes;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Comercial;

// Métricas comerciales (solo productos; la cuota incluida en un ticket es ingreso de membresía y se analiza
// en Inteligencia de negocio). Ventas anuladas excluidas. Fechas en hora local del gimnasio.
public class ReportesComerciales
{
    private readonly GymDbContext _db;
    public ReportesComerciales(GymDbContext db) => _db = db;

    // Línea vendida (read model mínimo para las métricas)
    public sealed record ItemH(int VentaId, DateOnly Dia, int ProductoId, string Producto, int CategoriaId, string Categoria,
        int Cantidad, decimal Importe, decimal Costo);

    public sealed record FilaProducto(int ProductoId, string Nombre, string Categoria, int Unidades, decimal Importe, decimal Margen);
    public sealed record FilaCategoria(int CategoriaId, string Nombre, int Unidades, decimal Importe, decimal Margen, string Slot);
    public sealed record MesVentas(DateOnly Mes, decimal Importe, decimal Margen, int Tickets, bool Completo);

    public sealed record Reporte(
        Periodo Periodo, Kpi Hoy, Kpi Mes, Kpi Margen, Kpi Rentabilidad, Kpi TicketPromedio,
        IReadOnlyList<FilaProducto> MasVendidos, IReadOnlyList<FilaProducto> MasRentables,
        IReadOnlyList<FilaCategoria> Categorias, IReadOnlyList<MesVentas> Meses, IReadOnlyList<FilaProducto> SinMovimiento)
    {
        public decimal Importe => Meses.Sum(m => m.Importe);
        public decimal MargenTotal => Meses.Sum(m => m.Margen);
        public int Tickets => Meses.Sum(m => m.Tickets);
    }

    // Slot de color estable por categoría (sigue a la entidad, no al ranking)
    public static string SlotCategoria(int indiceEstable) => indiceEstable < 6 ? (indiceEstable + 1).ToString() : "gris";

    public async Task<List<ItemH>> ItemsAsync(DateOnly desde, DateOnly hasta)
    {
        var desdeUtc = Periodo.InicioUtc(desde);
        var hastaUtc = Periodo.InicioUtc(hasta.AddDays(1));
        var filas = await _db.VentaItems.AsNoTracking()
            .Where(i => !i.Venta.Anulada && i.Venta.Fecha >= desdeUtc && i.Venta.Fecha < hastaUtc)
            .Select(i => new
            {
                i.VentaId, i.Venta.Fecha, i.ProductoId, Producto = i.Producto.Nombre, i.Producto.CategoriaId,
                Categoria = i.Producto.Categoria.Nombre, i.Cantidad, i.PrecioUnitario, i.CostoUnitario
            })
            .ToListAsync();
        return filas.Select(f => new ItemH(f.VentaId, Periodo.Local(f.Fecha), f.ProductoId, f.Producto, f.CategoriaId, f.Categoria,
            f.Cantidad, f.Cantidad * f.PrecioUnitario, f.Cantidad * f.CostoUnitario)).ToList();
    }

    // Ventas del día (para los KPIs del catálogo y del POS)
    public async Task<(decimal Importe, int Tickets)> DelDiaAsync(DateOnly hoy)
    {
        var items = await ItemsAsync(hoy, hoy);
        return (items.Sum(i => i.Importe), items.Select(i => i.VentaId).Distinct().Count());
    }

    public async Task<Reporte> ReporteAsync(Periodo periodo, DateOnly hoy)
    {
        var inicioTendencia = Periodo.InicioMes(hoy).AddMonths(-11);
        var desde = new[] { periodo.Desde, inicioTendencia, Periodo.InicioMes(hoy).AddMonths(-1) }.Min();
        var hasta = periodo.Hasta > hoy ? periodo.Hasta : hoy;
        var items = await ItemsAsync(desde, hasta);
        var categorias = await _db.CategoriasProducto.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).ToListAsync();
        var activos = await _db.Productos.AsNoTracking().Where(p => p.Activo)
            .Select(p => new { p.Id, p.Nombre, Cat = p.Categoria.Nombre }).ToListAsync();
        return Calcular(periodo, hoy, items, categorias, activos.Select(p => (p.Id, p.Nombre, p.Cat)).ToList());
    }

    // Puro: testeable sin base de datos
    public static Reporte Calcular(Periodo periodo, DateOnly hoy, IReadOnlyList<ItemH> items, IReadOnlyList<int> categoriasPorId,
        IReadOnlyList<(int Id, string Nombre, string Categoria)> activos)
    {
        IEnumerable<ItemH> Entre(DateOnly d, DateOnly h) => items.Where(i => i.Dia >= d && i.Dia <= h);
        decimal Importe(IEnumerable<ItemH> xs) => xs.Sum(i => i.Importe);
        decimal Margen(IEnumerable<ItemH> xs) => xs.Sum(i => i.Importe - i.Costo);
        int Tickets(IEnumerable<ItemH> xs) => xs.Select(i => i.VentaId).Distinct().Count();
        decimal Rent(IEnumerable<ItemH> xs) { var imp = Importe(xs); return imp == 0 ? 0 : Math.Round(Margen(xs) * 100 / imp, 1); }
        decimal TicketProm(IEnumerable<ItemH> xs) { var t = Tickets(xs); return t == 0 ? 0 : Math.Round(Importe(xs) / t, 0); }

        var meses12 = Enumerable.Range(0, 12).Select(k => Periodo.InicioMes(hoy).AddMonths(k - 11)).ToList();
        var etiquetas = meses12.Select(Presentacion.Mes).ToList();
        IReadOnlyList<decimal> Tendencia(Func<IEnumerable<ItemH>, decimal> f) =>
            meses12.Select(m => f(Entre(m, Periodo.FinMes(m)))).ToList();

        var (mesDesde, mesHasta) = Periodo.MesActualHastaHoy(hoy);
        var (antDesde, antHasta) = Periodo.MismoTramoMesAnterior(hoy);
        var delMes = Entre(mesDesde, mesHasta).ToList();
        var mesAnt = Entre(antDesde, antHasta).ToList();
        var ayer = hoy.AddDays(-1);
        var semanaPasada = hoy.AddDays(-7);

        var kHoy = new Kpi("ventas-hoy", "Ventas de hoy", "bi-cart-check", "primary",
            Importe(Entre(hoy, hoy)), Importe(Entre(semanaPasada, semanaPasada)), FormatoKpi.Dinero, true,
            "vs. mismo día de la semana pasada", Tendencia(Importe), etiquetas,
            "Productos vendidos hoy (sin cuotas ni ventas anuladas). Se compara con el mismo día de la semana anterior.");
        var kMes = new Kpi("ventas-mes", "Ventas del mes", "bi-bag-check", "success",
            Importe(delMes), Importe(mesAnt), FormatoKpi.Dinero, true,
            "vs. mismo tramo del mes anterior", Tendencia(Importe), etiquetas,
            "Productos vendidos desde el 1° del mes hasta hoy, comparado con los mismos días del mes anterior.");
        var kMargen = new Kpi("margen", "Margen estimado del mes", "bi-piggy-bank", "warning",
            Margen(delMes), Margen(mesAnt), FormatoKpi.Dinero, true,
            "vs. mismo tramo del mes anterior", Tendencia(Margen), etiquetas,
            "Precio cobrado menos costo de compra registrado en cada venta. Es estimado: no incluye gastos fijos.");
        var kRent = new Kpi("rentabilidad", "Rentabilidad", "bi-percent", "danger",
            Rent(delMes), mesAnt.Count == 0 ? null : Rent(mesAnt), FormatoKpi.Porcentaje, true,
            "vs. mismo tramo del mes anterior", Tendencia(Rent), etiquetas,
            "Margen sobre ventas del mes (margen ÷ ventas). La variación se expresa en puntos porcentuales.");
        var kTicket = new Kpi("ticket", "Ticket promedio", "bi-receipt", "primary",
            TicketProm(delMes), mesAnt.Count == 0 ? null : TicketProm(mesAnt), FormatoKpi.Dinero, true,
            "vs. mismo tramo del mes anterior", Tendencia(TicketProm), etiquetas,
            "Ventas de productos del mes divididas por la cantidad de tickets.");

        var enPeriodo = Entre(periodo.Desde, periodo.Hasta).ToList();
        var porProducto = enPeriodo.GroupBy(i => i.ProductoId)
            .Select(g => new FilaProducto(g.Key, g.First().Producto, g.First().Categoria, g.Sum(i => i.Cantidad), Importe(g), Margen(g)))
            .ToList();
        var masVendidos = porProducto.OrderByDescending(f => f.Unidades).ThenByDescending(f => f.Importe).Take(10).ToList();
        var indice = categoriasPorId.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var porCategoria = enPeriodo.GroupBy(i => i.CategoriaId)
            .Select(g => new FilaCategoria(g.Key, g.First().Categoria, g.Sum(i => i.Cantidad), Importe(g), Margen(g),
                SlotCategoria(indice.TryGetValue(g.Key, out var ix) ? ix : 99)))
            .OrderByDescending(f => f.Importe)
            .ToList();
        var meses = periodo.Meses().Select(m =>
        {
            var (d, h) = periodo.Tramo(m);
            var xs = Entre(d, h).ToList();
            return new MesVentas(m, Importe(xs), Margen(xs), Tickets(xs), h == Periodo.FinMes(m));
        }).ToList();
        var vendidos = porProducto.Select(f => f.ProductoId).ToHashSet();
        var sinMovimiento = activos.Where(a => !vendidos.Contains(a.Id))
            .Select(a => new FilaProducto(a.Id, a.Nombre, a.Categoria, 0, 0, 0)).OrderBy(f => f.Nombre).ToList();

        var masRentables = porProducto.Where(f => f.Margen > 0).OrderByDescending(f => f.Margen).Take(5).ToList();
        return new Reporte(periodo, kHoy, kMes, kMargen, kRent, kTicket, masVendidos, masRentables, porCategoria, meses, sinMovimiento);
    }

    // ===== Compras de un socio (ficha) =====
    public sealed record CompraSocio(int VentaId, string Numero, DateTime Fecha, string Producto, int Cantidad, decimal Importe, bool Anulada);

    public sealed record ComprasSocio(IReadOnlyList<CompraSocio> Lineas, decimal Total, int Tickets, DateTime? Ultima)
    {
        public decimal TicketPromedio => Tickets == 0 ? 0 : Math.Round(Total / Tickets, 0);
    }

    public async Task<ComprasSocio> ComprasDeSocioAsync(int socioId, int maxLineas = 100)
    {
        var lineas = await _db.VentaItems.AsNoTracking()
            .Where(i => i.Venta.SocioId == socioId)
            .OrderByDescending(i => i.Venta.Fecha).ThenBy(i => i.Id)
            .Take(maxLineas)
            .Select(i => new { i.VentaId, i.Venta.Fecha, i.Descripcion, i.Cantidad, i.PrecioUnitario, i.Venta.Anulada })
            .ToListAsync();
        var totales = await _db.Ventas.AsNoTracking()
            .Where(v => v.SocioId == socioId && !v.Anulada && v.TotalProductos > 0)
            .GroupBy(v => 1)
            .Select(g => new { Total = g.Sum(v => v.TotalProductos), Tickets = g.Count(), Ultima = g.Max(v => v.Fecha) })
            .FirstOrDefaultAsync();
        return new ComprasSocio(
            lineas.Select(l => new CompraSocio(l.VentaId, $"V-{l.VentaId:D6}", l.Fecha, l.Descripcion, l.Cantidad, l.Cantidad * l.PrecioUnitario, l.Anulada)).ToList(),
            totales?.Total ?? 0, totales?.Tickets ?? 0, totales?.Ultima);
    }
}
