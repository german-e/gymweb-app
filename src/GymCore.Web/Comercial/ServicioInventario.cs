using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Comercial;

// Inventario: único camino para cambiar el stock. Cada cambio deja un MovimientoStock con su resultado,
// fecha y usuario. El stock es token de concurrencia: dos cajas vendiendo el último ítem no lo duplican.
public class ServicioInventario
{
    private readonly GymDbContext _db;
    public ServicioInventario(GymDbContext db) => _db = db;

    public const int MotivoMaxLength = 300;
    public const int CantidadMaxima = 100_000;

    public record SolicitudAjuste(int ProductoId, TipoMovimientoStock Tipo, int Cantidad, string? Motivo,
        decimal? CostoUnitario = null, bool ActualizarCosto = false);

    public record Resultado(bool Ok, string? Error = null, MovimientoStock? Movimiento = null)
    {
        public static Resultado Falla(string error) => new(false, error);
    }

    // Aplica el movimiento en memoria y lo agrega al contexto. No guarda.
    public MovimientoStock Registrar(Producto producto, TipoMovimientoStock tipo, int cantidad, string? motivo,
        decimal? costoUnitario = null, Venta? venta = null)
    {
        if (cantidad == 0) throw new ArgumentException("La cantidad del movimiento no puede ser cero.", nameof(cantidad));
        var resultante = producto.AplicarMovimiento(cantidad);
        var m = new MovimientoStock
        {
            Producto = producto,
            ProductoId = producto.Id,
            Fecha = DateTime.UtcNow,
            Tipo = tipo,
            Cantidad = cantidad,
            StockResultante = resultante,
            CostoUnitario = costoUnitario,
            Motivo = Recortar(motivo),
            Venta = venta
        };
        _db.MovimientosStock.Add(m);
        return m;
    }

    // Movimientos manuales: Compra (+), Ajuste (±, ej. rotura o consumo interno) y Corrección (conteo físico).
    // Para Corrección, Cantidad es el stock contado; el movimiento registra la diferencia.
    public async Task<Resultado> AjustarAsync(SolicitudAjuste s)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == s.ProductoId);
        if (p is null) return Resultado.Falla("El producto no existe.");
        var motivo = s.Motivo?.Trim();

        int delta;
        switch (s.Tipo)
        {
            case TipoMovimientoStock.Compra:
                if (s.Cantidad <= 0) return Resultado.Falla("Ingresá cuántas unidades entraron.");
                if (s.CostoUnitario is < 0) return Resultado.Falla("El costo no puede ser negativo.");
                delta = s.Cantidad;
                break;
            case TipoMovimientoStock.Ajuste:
                if (s.Cantidad == 0) return Resultado.Falla("El ajuste no puede ser cero.");
                if (string.IsNullOrEmpty(motivo)) return Resultado.Falla("Indicá el motivo del ajuste.");
                delta = s.Cantidad;
                break;
            case TipoMovimientoStock.Correccion:
                if (s.Cantidad < 0) return Resultado.Falla("El stock contado no puede ser negativo.");
                if (string.IsNullOrEmpty(motivo)) return Resultado.Falla("Indicá el motivo de la corrección.");
                delta = s.Cantidad - p.StockActual;
                if (delta == 0) return Resultado.Falla($"El stock contado coincide con el del sistema ({p.StockActual}). No hay nada que corregir.");
                break;
            default:
                return Resultado.Falla("Tipo de movimiento no permitido.");
        }
        if (Math.Abs(delta) > CantidadMaxima) return Resultado.Falla("La cantidad es demasiado grande.");
        if (p.StockActual + delta < 0) return Resultado.Falla($"No podés dejar el stock en negativo: hay {p.StockActual} unidades.");

        var anterior = p.StockActual;
        var mov = Registrar(p, s.Tipo, delta, motivo, s.Tipo == TipoMovimientoStock.Compra ? s.CostoUnitario : null);
        if (s.Tipo == TipoMovimientoStock.Compra && s.ActualizarCosto && s.CostoUnitario is { } costo && costo != p.PrecioCompra)
            p.PrecioCompra = costo;

        _db.Auditar(TipoEventoSistema.StockAjustado, EntidadAuditada.Producto, p.Id, p.Nombre,
            $"{InventarioPresentacion.Texto(s.Tipo)} · {(delta > 0 ? "+" : "")}{delta} u. · stock {anterior} → {p.StockActual}",
            motivo);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Resultado.Falla("El stock de este producto cambió mientras cargabas el movimiento. Revisalo y volvé a intentar.");
        }
        return new Resultado(true, Movimiento: mov);
    }

    // ===== Alertas de stock (productos activos) =====
    public record AlertaStock(int Id, string Nombre, string Categoria, int Stock, int Minimo, EstadoStock Estado);

    public record ResumenStock(IReadOnlyList<AlertaStock> Alertas)
    {
        public int SinStock => Alertas.Count(a => a.Estado == EstadoStock.SinStock);
        public int Criticos => Alertas.Count(a => a.Estado == EstadoStock.Critico);
        public int Proximos => Alertas.Count(a => a.Estado == EstadoStock.ProximoAlMinimo);
        public int Urgentes => SinStock + Criticos;
        public static readonly ResumenStock Vacio = new(Array.Empty<AlertaStock>());
    }

    // Memorizado por request: la página (Panel, Ventas) y la campana comparten la misma consulta
    public async Task<ResumenStock> ResumenDelRequestAsync(HttpContext ctx)
    {
        const string clave = "GymCore.Stock";
        if (ctx.Items[clave] is ResumenStock memo) return memo;
        var r = await ResumenAsync();
        ctx.Items[clave] = r;
        return r;
    }

    public async Task<ResumenStock> ResumenAsync()
    {
        // "Próximo al mínimo" = stock <= ceil(1,5 × mínimo)  ⇔  2·stock <= 3·mínimo + 1 (en enteros, traducible a SQL)
        var filas = await _db.Productos.AsNoTracking()
            .Where(p => p.Activo && (p.StockActual <= 0 || (p.StockMinimo > 0 && 2 * p.StockActual <= 3 * p.StockMinimo + 1)))
            .Select(p => new { p.Id, p.Nombre, Categoria = p.Categoria.Nombre, p.StockActual, p.StockMinimo })
            .ToListAsync();
        var alertas = filas
            .Select(f => new AlertaStock(f.Id, f.Nombre, f.Categoria, f.StockActual, f.StockMinimo,
                Producto.EstadoDe(f.StockActual, f.StockMinimo)))
            .Where(a => a.Estado != EstadoStock.Normal)
            .OrderByDescending(a => a.Estado).ThenBy(a => a.Stock).ThenBy(a => a.Nombre)
            .ToList();
        return new ResumenStock(alertas);
    }

    private static string? Recortar(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > MotivoMaxLength ? s.Trim()[..MotivoMaxLength] : s.Trim();
}
