using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Pagos;
using GymCore.Web.Pages.Socios;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Comercial;

// Ventas: un ticket = productos (+ cuota opcional del socio) en una sola transacción.
// Precios y costos se toman del catálogo en el servidor (nunca del navegador) y quedan congelados en el ítem.
public class ServicioVentas
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly ServicioInventario _inventario;
    private readonly ServicioCobranza _cobranza;

    public ServicioVentas(GymDbContext db, IUsuarioActual usuario, ServicioInventario inventario, ServicioCobranza cobranza)
    {
        _db = db; _usuario = usuario; _inventario = inventario; _cobranza = cobranza;
    }

    public const int MaxLineas = 60;
    public const int MaxCantidadPorLinea = 999;
    public const decimal MaxCuota = 1_000_000m;

    public record Linea(int ProductoId, int Cantidad);

    public record Solicitud(IReadOnlyList<Linea> Lineas, MedioPago Medio, int? SocioId = null, string? ClienteNombre = null,
        decimal? ImporteCuota = null, string? Observaciones = null, CanalVenta Canal = CanalVenta.Mostrador);

    public record Resultado(bool Ok, string? Error = null, Venta? Venta = null)
    {
        public static Resultado Falla(string e) => new(false, e);
    }

    public async Task<Resultado> RegistrarAsync(Solicitud s)
    {
        // Agrupa líneas repetidas del mismo producto (ej. escaneado dos veces)
        var lineas = (s.Lineas ?? Array.Empty<Linea>())
            .Where(l => l.Cantidad != 0)
            .GroupBy(l => l.ProductoId)
            .Select(g => new Linea(g.Key, g.Sum(l => l.Cantidad)))
            .ToList();
        var cuota = s.ImporteCuota is > 0 ? Math.Round(s.ImporteCuota.Value, 2) : (decimal?)null;

        if (lineas.Count == 0 && cuota is null) return Resultado.Falla("El ticket está vacío: agregá productos o la cuota del socio.");
        if (lineas.Count > MaxLineas) return Resultado.Falla($"Un ticket admite hasta {MaxLineas} productos distintos.");
        if (lineas.Any(l => l.Cantidad < 0 || l.Cantidad > MaxCantidadPorLinea)) return Resultado.Falla($"Las cantidades deben estar entre 1 y {MaxCantidadPorLinea}.");
        if (!Enum.IsDefined(s.Medio)) return Resultado.Falla("Elegí el medio de pago.");
        if (s.ImporteCuota is < 0 || cuota > MaxCuota) return Resultado.Falla("El importe de la cuota no es válido.");

        Socio? socio = null;
        if (s.SocioId is { } socioId)
        {
            socio = await _cobranza.SocioConSuscripcionesAsync(socioId);
            if (socio is null) return Resultado.Falla("El socio seleccionado no existe.");
        }
        if (cuota is not null && socio is null) return Resultado.Falla("Para cobrar una cuota seleccioná al socio.");
        if (cuota is not null && socio!.DadoDeBaja) return Resultado.Falla("El socio está dado de baja: reactivalo antes de cobrarle una cuota.");

        var ids = lineas.Select(l => l.ProductoId).ToList();
        var productos = await _db.Productos.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var l in lineas)
        {
            if (!productos.TryGetValue(l.ProductoId, out var p)) return Resultado.Falla("Uno de los productos ya no existe. Actualizá la pantalla.");
            if (!p.Activo) return Resultado.Falla($"{p.Nombre} está desactivado y no se puede vender.");
            if (p.StockActual < l.Cantidad)
                return Resultado.Falla(p.StockActual <= 0 ? $"{p.Nombre} no tiene stock." : $"Stock insuficiente de {p.Nombre}: quedan {p.StockActual}.");
        }

        var venta = new Venta
        {
            Fecha = DateTime.UtcNow,
            SocioId = socio?.Id,
            ClienteNombre = socio is null ? Recortar(s.ClienteNombre, 120) : null,
            Medio = s.Medio,
            Canal = s.Canal,
            Observaciones = Recortar(s.Observaciones, 500),
            Usuario = _usuario.Nombre,
            UsuarioId = _usuario.UsuarioId
        };
        foreach (var l in lineas)
        {
            var p = productos[l.ProductoId];
            venta.Items.Add(new VentaItem
            {
                ProductoId = p.Id, Producto = p, Descripcion = p.Nombre, Cantidad = l.Cantidad,
                PrecioUnitario = p.PrecioVenta, CostoUnitario = p.PrecioCompra
            });
            _inventario.Registrar(p, TipoMovimientoStock.Venta, -l.Cantidad, null, venta: venta);
        }
        venta.TotalProductos = venta.Items.Sum(i => i.Subtotal);
        venta.Total = venta.TotalProductos + (cuota ?? 0m);
        _db.Ventas.Add(venta);

        if (cuota is { } importe)
            venta.Cuota = _cobranza.CrearPago(socio!, importe, s.Medio, observaciones: "Cobrado en el Punto de Venta", venta: venta);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.SaveChangesAsync();
            // Con el número de ticket ya asignado se registra el evento (misma transacción)
            _db.Auditar(TipoEventoSistema.VentaRealizada, EntidadAuditada.Venta, venta.Id, Cliente(venta, socio),
                ResumenVenta(venta), DetalleItems(venta), socio?.Id);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return Resultado.Falla("El stock cambió mientras cobrabas (otra caja vendió el mismo producto). Revisá el ticket y volvé a cobrar.");
        }
        return new Resultado(true, Venta: venta);
    }

    // Anulación: la venta se conserva marcada, el stock vuelve con un movimiento "Anulación de venta"
    // y la cuota incluida (si la hubo) se anula como cualquier pago.
    public async Task<Resultado> AnularAsync(int ventaId, string motivo)
    {
        motivo = motivo?.Trim() ?? "";
        if (motivo.Length == 0) return Resultado.Falla("Indicá el motivo de la anulación.");

        var venta = await _db.Ventas
            .Include(v => v.Items).ThenInclude(i => i.Producto)
            .Include(v => v.Socio)
            .FirstOrDefaultAsync(v => v.Id == ventaId);
        if (venta is null) return Resultado.Falla("La venta no existe.");
        if (venta.Anulada) return Resultado.Falla("La venta ya estaba anulada.");
        var cuota = await _db.Pagos.Include(p => p.Socio).FirstOrDefaultAsync(p => p.VentaId == ventaId);

        venta.Anular(motivo, _usuario.Nombre, DateTime.UtcNow);
        foreach (var i in venta.Items)
            _inventario.Registrar(i.Producto, TipoMovimientoStock.AnulacionVenta, i.Cantidad, $"Anulación {venta.Numero}", venta: venta);
        if (cuota is not null)
        {
            cuota.Anular($"Anulación del ticket {venta.Numero}: {venta.MotivoAnulacion}", _usuario.Nombre, DateTime.UtcNow);
            _db.Auditar(TipoEventoSistema.PagoAnulado, EntidadAuditada.Pago, cuota.Id, $"{cuota.Socio.Apellido}, {cuota.Socio.Nombre}",
                $"{cuota.Numero()} · {SocioPresentacion.Dinero(cuota.Importe, cuota.Moneda)} · {cuota.MotivoAnulacion}", socioId: cuota.SocioId);
        }
        _db.Auditar(TipoEventoSistema.VentaAnulada, EntidadAuditada.Venta, venta.Id, Cliente(venta, venta.Socio),
            $"{venta.Numero} · {SocioPresentacion.Dinero(venta.Total)} · {venta.MotivoAnulacion}", DetalleItems(venta), venta.SocioId);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Resultado.Falla("El stock de algún producto cambió en este momento. Volvé a intentar.");
        }
        return new Resultado(true, Venta: venta);
    }

    public static string Cliente(Venta v, Socio? socio) =>
        socio is not null ? $"{socio.Apellido}, {socio.Nombre}" : string.IsNullOrWhiteSpace(v.ClienteNombre) ? "Cliente ocasional" : v.ClienteNombre!;

    private static string ResumenVenta(Venta v)
    {
        var partes = new List<string> { v.Numero, SocioPresentacion.Dinero(v.Total), v.Medio.Texto() };
        var unidades = v.Items.Sum(i => i.Cantidad);
        if (unidades > 0) partes.Add($"{unidades} {(unidades == 1 ? "producto" : "productos")}");
        if (v.Cuota is not null) partes.Add("incluye cuota");
        return string.Join(" · ", partes);
    }

    private static string? DetalleItems(Venta v) => v.Items.Count == 0 ? null
        : string.Join("\n", v.Items.Select(i => $"{i.Cantidad} × {i.Descripcion} ({SocioPresentacion.Dinero(i.PrecioUnitario)})"));

    private static string? Recortar(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();
}
