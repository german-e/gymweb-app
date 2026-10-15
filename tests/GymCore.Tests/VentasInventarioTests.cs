using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Componentes;
using GymCore.Web.Reportes;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Ventas e inventario: dominio de stock, ventas con y sin cuota, anulaciones, ajustes y reportes comerciales.
public class VentasInventarioTests
{
    private sealed class Cajero : IUsuarioActual
    {
        private readonly HashSet<string> _permisos;
        public Cajero(params string[] permisos) => _permisos = permisos.ToHashSet();
        public int? UsuarioId => 7;
        public string Nombre => "Cajero Test";
        public bool Autenticado => true;
        public string NombreUsuario => "cajero";
        public string Rol => "Recepcionista";
        public string Iniciales => "CT";
        public int? EntrenadorId => null;
        public DateTime? AccesoAnterior => null;
        public bool Puede(string permiso) => _permisos.Contains(permiso);
        public bool PuedeAlguno(params string[] permisos) => permisos.Any(Puede);
    }

    private static (ServicioVentas Ventas, ServicioInventario Inventario) Servicios(GymDbContext db)
    {
        var usuario = new Cajero();
        var inventario = new ServicioInventario(db);
        return (new ServicioVentas(db, usuario, inventario, new ServicioCobranza(db, usuario)), inventario);
    }

    private static async Task<Producto> ProductoAsync(BaseDePrueba b, string nombre, int stock, decimal precio = 1000, decimal costo = 600,
        int minimo = 5, bool activo = true)
    {
        using var db = b.Contexto();
        var cat = await db.CategoriasProducto.FirstOrDefaultAsync() ?? db.CategoriasProducto.Add(new CategoriaProducto { Nombre = "Bebidas" }).Entity;
        var p = new Producto
        {
            Codigo = "P-" + Guid.NewGuid().ToString("N")[..6], Nombre = nombre, Categoria = cat,
            PrecioVenta = precio, PrecioCompra = costo, StockMinimo = minimo, Activo = activo
        };
        db.Productos.Add(p);
        if (stock > 0) new ServicioInventario(db).Registrar(p, TipoMovimientoStock.StockInicial, stock, "Alta", costo);
        await db.SaveChangesAsync();
        return p;
    }

    private static int Stock(BaseDePrueba b, int productoId)
    {
        using var db = b.Contexto();
        return db.Productos.AsNoTracking().Single(p => p.Id == productoId).StockActual;
    }

    // ===================== Dominio =====================

    [Theory]
    [InlineData(0, 5, EstadoStock.SinStock)]
    [InlineData(-1, 5, EstadoStock.SinStock)]
    [InlineData(5, 5, EstadoStock.Critico)]
    [InlineData(3, 5, EstadoStock.Critico)]
    [InlineData(8, 5, EstadoStock.ProximoAlMinimo)]   // ceil(5 × 1,5) = 8
    [InlineData(9, 5, EstadoStock.Normal)]
    [InlineData(1, 0, EstadoStock.Normal)]            // sin mínimo: solo avisa al agotarse
    [InlineData(0, 0, EstadoStock.SinStock)]
    public void Estado_de_stock_segun_minimo(int stock, int minimo, EstadoStock esperado) =>
        Assert.Equal(esperado, Producto.EstadoDe(stock, minimo));

    [Fact]
    public void El_stock_nunca_queda_negativo()
    {
        var p = new Producto { Nombre = "Agua" };
        Assert.Equal(3, p.AplicarMovimiento(3));
        Assert.Throws<InvalidOperationException>(() => p.AplicarMovimiento(-4));
        Assert.Equal(3, p.StockActual);
    }

    [Fact]
    public void Margen_del_producto()
    {
        var p = new Producto { PrecioVenta = 1000, PrecioCompra = 600 };
        Assert.Equal(400, p.MargenUnitario);
        Assert.Equal(40, p.MargenPorcentaje);
        Assert.Null(new Producto().MargenPorcentaje);
    }

    // ===================== Ventas =====================

    [Fact]
    public async Task Vender_descuenta_stock_congela_precio_y_audita()
    {
        using var b = new BaseDePrueba();
        var agua = await ProductoAsync(b, "Agua", stock: 10, precio: 1500, costo: 700);
        var (socio, _) = await b.SocioConPlanAsync();

        int ventaId;
        using (var db = b.Contexto())
        {
            var (ventas, _) = Servicios(db);
            // Dos líneas del mismo producto (escaneado dos veces) se agrupan
            var r = await ventas.RegistrarAsync(new ServicioVentas.Solicitud(
                new[] { new ServicioVentas.Linea(agua.Id, 2), new ServicioVentas.Linea(agua.Id, 1) }, MedioPago.Efectivo, socio.Id));
            Assert.True(r.Ok, r.Error);
            ventaId = r.Venta!.Id;
        }

        Assert.Equal(7, Stock(b, agua.Id));
        using (var db = b.Contexto())
        {
            // Cambiar el precio después no altera la venta
            var p = db.Productos.Single(x => x.Id == agua.Id);
            p.PrecioVenta = 9999;
            await db.SaveChangesAsync();
        }
        using (var db = b.Contexto())
        {
            var v = db.Ventas.Include(x => x.Items).Single(x => x.Id == ventaId);
            var item = Assert.Single(v.Items);
            Assert.Equal(3, item.Cantidad);
            Assert.Equal(1500, item.PrecioUnitario);
            Assert.Equal(700, item.CostoUnitario);
            Assert.Equal(4500, v.Total);
            Assert.Equal("Cajero Test", v.Usuario);
            Assert.Equal(socio.Id, v.SocioId);

            var mov = db.MovimientosStock.Single(m => m.VentaId == ventaId);
            Assert.Equal(TipoMovimientoStock.Venta, mov.Tipo);
            Assert.Equal(-3, mov.Cantidad);
            Assert.Equal(7, mov.StockResultante);
            Assert.Equal("Cajero Test", mov.Usuario);
            Assert.Equal(7, mov.UsuarioId);

            var ev = db.EventosSistema.Single(e => e.Tipo == TipoEventoSistema.VentaRealizada);
            Assert.Equal(ventaId, ev.EntidadId);
            Assert.Equal(socio.Id, ev.SocioId);
            Assert.Contains("V-", ev.Resumen);
        }
    }

    [Fact]
    public async Task Sin_stock_suficiente_no_se_vende_nada()
    {
        using var b = new BaseDePrueba();
        var agua = await ProductoAsync(b, "Agua", stock: 5);
        var barra = await ProductoAsync(b, "Barra", stock: 1);
        using (var db = b.Contexto())
        {
            var (ventas, _) = Servicios(db);
            var r = await ventas.RegistrarAsync(new ServicioVentas.Solicitud(
                new[] { new ServicioVentas.Linea(agua.Id, 2), new ServicioVentas.Linea(barra.Id, 2) }, MedioPago.Efectivo));
            Assert.False(r.Ok);
            Assert.Contains("Barra", r.Error);
        }
        Assert.Equal(5, Stock(b, agua.Id));
        using var db2 = b.Contexto();
        Assert.Equal(0, db2.Ventas.Count());
        Assert.Equal(2, db2.MovimientosStock.Count());   // solo los stocks iniciales
    }

    [Fact]
    public async Task No_se_vende_un_producto_desactivado_ni_un_ticket_vacio()
    {
        using var b = new BaseDePrueba();
        var viejo = await ProductoAsync(b, "Discontinuado", stock: 5, activo: false);
        using var db = b.Contexto();
        var (ventas, _) = Servicios(db);
        Assert.False((await ventas.RegistrarAsync(new(new[] { new ServicioVentas.Linea(viejo.Id, 1) }, MedioPago.Efectivo))).Ok);
        Assert.False((await ventas.RegistrarAsync(new(Array.Empty<ServicioVentas.Linea>(), MedioPago.Efectivo))).Ok);
        Assert.False((await ventas.RegistrarAsync(new(Array.Empty<ServicioVentas.Linea>(), MedioPago.Efectivo, ImporteCuota: 500))).Ok); // cuota sin socio
    }

    [Fact]
    public async Task Cuota_y_productos_en_un_solo_ticket()
    {
        using var b = new BaseDePrueba();
        var (socio, sp) = await b.SocioConPlanAsync(precio: 20000);
        var prote = await ProductoAsync(b, "Proteína", stock: 3, precio: 35000, costo: 25000);

        int ventaId;
        using (var db = b.Contexto())
        {
            var (ventas, _) = Servicios(db);
            var r = await ventas.RegistrarAsync(new(new[] { new ServicioVentas.Linea(prote.Id, 1) }, MedioPago.TarjetaDebito, socio.Id, ImporteCuota: 20000));
            Assert.True(r.Ok, r.Error);
            ventaId = r.Venta!.Id;
            Assert.Equal(35000, r.Venta.TotalProductos);
            Assert.Equal(55000, r.Venta.Total);
        }
        using (var db = b.Contexto())
        {
            // La cuota es un Pago común: imputado a la suscripción vigente, con su cajero y su evento
            var pago = db.Pagos.Single(p => p.VentaId == ventaId);
            Assert.Equal(sp.Id, pago.SocioPlanId);
            Assert.Equal(20000, pago.Importe);
            Assert.Equal(MedioPago.TarjetaDebito, pago.Medio);
            Assert.Equal("Cajero Test", pago.UsuarioCajero);
            Assert.True(db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.PagoRegistrado && e.EntidadId == pago.Id));

            var cobranza = new ServicioCobranza(db, new Cajero());
            var saldo = await cobranza.SaldoAsync((await cobranza.SocioConSuscripcionesAsync(socio.Id))!);
            Assert.Equal(0, saldo!.Faltante);
        }
    }

    [Fact]
    public async Task Anular_venta_repone_stock_anula_la_cuota_y_conserva_el_ticket()
    {
        using var b = new BaseDePrueba();
        var (socio, sp) = await b.SocioConPlanAsync(precio: 1000);
        var agua = await ProductoAsync(b, "Agua", stock: 4);
        int ventaId;
        using (var db = b.Contexto())
        {
            var r = await Servicios(db).Ventas.RegistrarAsync(new(new[] { new ServicioVentas.Linea(agua.Id, 3) }, MedioPago.Efectivo, socio.Id, ImporteCuota: 1000));
            ventaId = r.Venta!.Id;
        }
        Assert.Equal(1, Stock(b, agua.Id));

        using (var db = b.Contexto())
        {
            var r = await Servicios(db).Ventas.AnularAsync(ventaId, "Devolución del cliente");
            Assert.True(r.Ok, r.Error);
            var otra = await Servicios(db).Ventas.AnularAsync(ventaId, "Otra vez");
            Assert.False(otra.Ok);
        }

        Assert.Equal(4, Stock(b, agua.Id));
        using (var db = b.Contexto())
        {
            var v = db.Ventas.Single(x => x.Id == ventaId);
            Assert.True(v.Anulada);
            Assert.Equal("Devolución del cliente", v.MotivoAnulacion);
            Assert.Equal("Cajero Test", v.UsuarioAnulo);
            Assert.Equal(1, db.Ventas.Count());   // nunca se borra

            var repo = db.MovimientosStock.Single(m => m.Tipo == TipoMovimientoStock.AnulacionVenta);
            Assert.Equal(3, repo.Cantidad);
            Assert.Equal(4, repo.StockResultante);

            Assert.False(db.Pagos.Any(p => p.VentaId == ventaId));   // filtro global: la cuota anulada no cuenta
            Assert.True(db.Pagos.IgnoreQueryFilters().Single(p => p.VentaId == ventaId).Anulado);
            Assert.True(db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.VentaAnulada && e.EntidadId == ventaId));
            Assert.True(db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.PagoAnulado));
        }
    }

    [Fact]
    public async Task Dos_cajas_no_pueden_vender_la_misma_ultima_unidad()
    {
        using var b = new BaseDePrueba();
        var agua = await ProductoAsync(b, "Agua", stock: 1);
        using var caja1 = b.Contexto();
        using var caja2 = b.Contexto();
        var v1 = Servicios(caja1).Ventas;
        var v2 = Servicios(caja2).Ventas;
        // Ambas cajas leen el producto antes de que la otra guarde
        _ = caja1.Productos.Single(p => p.Id == agua.Id);
        _ = caja2.Productos.Single(p => p.Id == agua.Id);

        var r1 = await v1.RegistrarAsync(new(new[] { new ServicioVentas.Linea(agua.Id, 1) }, MedioPago.Efectivo));
        var r2 = await v2.RegistrarAsync(new(new[] { new ServicioVentas.Linea(agua.Id, 1) }, MedioPago.Efectivo));

        Assert.True(r1.Ok, r1.Error);
        Assert.False(r2.Ok);
        Assert.Equal(0, Stock(b, agua.Id));
        using var db = b.Contexto();
        Assert.Equal(1, db.Ventas.Count());
    }

    // ===================== Inventario =====================

    [Fact]
    public async Task Compra_suma_stock_y_puede_actualizar_el_costo()
    {
        using var b = new BaseDePrueba();
        var agua = await ProductoAsync(b, "Agua", stock: 2, costo: 600);
        using (var db = b.Contexto())
        {
            var r = await new ServicioInventario(db).AjustarAsync(new(agua.Id, TipoMovimientoStock.Compra, 24, "Factura 123", 650, true));
            Assert.True(r.Ok, r.Error);
        }
        using (var db = b.Contexto())
        {
            var p = db.Productos.Single(x => x.Id == agua.Id);
            Assert.Equal(26, p.StockActual);
            Assert.Equal(650, p.PrecioCompra);
            var m = db.MovimientosStock.Single(x => x.Tipo == TipoMovimientoStock.Compra);
            Assert.Equal(650, m.CostoUnitario);
            Assert.Equal("Factura 123", m.Motivo);
            Assert.True(db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.StockAjustado && e.EntidadId == agua.Id));
        }
    }

    [Fact]
    public async Task Correccion_por_conteo_registra_la_diferencia()
    {
        using var b = new BaseDePrueba();
        var agua = await ProductoAsync(b, "Agua", stock: 10);
        using (var db = b.Contexto())
        {
            var inv = new ServicioInventario(db);
            Assert.False((await inv.AjustarAsync(new(agua.Id, TipoMovimientoStock.Correccion, 7, null))).Ok);       // motivo obligatorio
            Assert.False((await inv.AjustarAsync(new(agua.Id, TipoMovimientoStock.Correccion, 10, "Conteo"))).Ok);  // sin diferencia
            var r = await inv.AjustarAsync(new(agua.Id, TipoMovimientoStock.Correccion, 7, "Inventario mensual"));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(-3, r.Movimiento!.Cantidad);
        }
        Assert.Equal(7, Stock(b, agua.Id));
    }

    [Fact]
    public async Task Ajuste_no_puede_dejar_stock_negativo()
    {
        using var b = new BaseDePrueba();
        var agua = await ProductoAsync(b, "Agua", stock: 2);
        using (var db = b.Contexto())
        {
            var inv = new ServicioInventario(db);
            Assert.False((await inv.AjustarAsync(new(agua.Id, TipoMovimientoStock.Ajuste, -3, "Rotura"))).Ok);
            Assert.True((await inv.AjustarAsync(new(agua.Id, TipoMovimientoStock.Ajuste, -2, "Rotura"))).Ok);
            Assert.False((await inv.AjustarAsync(new(agua.Id, TipoMovimientoStock.Venta, -1, "Manual"))).Ok);   // las ventas solo desde el POS
        }
        Assert.Equal(0, Stock(b, agua.Id));
    }

    [Fact]
    public async Task Resumen_de_alertas_solo_cuenta_productos_activos()
    {
        using var b = new BaseDePrueba();
        await ProductoAsync(b, "Agotado", stock: 0, minimo: 5);
        await ProductoAsync(b, "Critico", stock: 4, minimo: 5);
        await ProductoAsync(b, "Proximo", stock: 8, minimo: 5);
        await ProductoAsync(b, "Normal", stock: 9, minimo: 5);
        await ProductoAsync(b, "Inactivo agotado", stock: 0, minimo: 5, activo: false);
        using var db = b.Contexto();
        var r = await new ServicioInventario(db).ResumenAsync();
        Assert.Equal(1, r.SinStock);
        Assert.Equal(1, r.Criticos);
        Assert.Equal(1, r.Proximos);
        Assert.Equal(2, r.Urgentes);
        Assert.Equal("Agotado", r.Alertas[0].Nombre);   // lo más urgente primero
    }

    // ===================== Reportes comerciales =====================

    [Fact]
    public void Metricas_comerciales_por_periodo()
    {
        var hoy = new DateOnly(2026, 10, 15);
        var items = new List<ReportesComerciales.ItemH>
        {
            new(1, hoy, 1, "Agua", 1, "Bebidas", 2, 2000, 1200),
            new(1, hoy, 2, "Barra", 2, "Nutrición", 1, 1500, 700),
            new(2, hoy.AddDays(-3), 1, "Agua", 1, "Bebidas", 5, 5000, 3000),
            new(3, new DateOnly(2026, 9, 10), 1, "Agua", 1, "Bebidas", 1, 1000, 600),   // mes anterior, mismo tramo
            new(4, new DateOnly(2026, 9, 20), 2, "Barra", 2, "Nutrición", 1, 1500, 700) // mes anterior, fuera del tramo
        };
        var periodo = Periodo.Crear("3m", hoy);
        var r = ReportesComerciales.Calcular(periodo, hoy, items, new[] { 1, 2, 3 },
            new List<(int, string, string)> { (1, "Agua", "Bebidas"), (2, "Barra", "Nutrición"), (3, "Shaker", "Accesorios") });

        Assert.Equal(3500, r.Hoy.Valor);
        Assert.Equal(8500, r.Mes.Valor);
        Assert.Equal(1000, r.Mes.Anterior);                 // solo hasta el día 15 del mes anterior
        Assert.Equal(3600, r.Margen.Valor);                 // 8500 − 4900
        Assert.Equal(42.4m, r.Rentabilidad.Valor);
        Assert.Equal("Agua", r.MasVendidos[0].Nombre);
        Assert.Equal(8, r.MasVendidos[0].Unidades);
        Assert.Equal("1", r.Categorias.Single(c => c.Nombre == "Bebidas").Slot);   // color estable por categoría
        Assert.Equal("2", r.Categorias.Single(c => c.Nombre == "Nutrición").Slot);
        Assert.Equal("Shaker", Assert.Single(r.SinMovimiento).Nombre);
        Assert.Equal(11000, r.Importe);       // todo el período de 3 meses
    }

    [Fact]
    public async Task Ventas_anuladas_no_suman_en_reportes_ni_en_compras_del_socio()
    {
        using var b = new BaseDePrueba();
        var (socio, _) = await b.SocioConPlanAsync();
        var agua = await ProductoAsync(b, "Agua", stock: 10, precio: 1000);
        using (var db = b.Contexto())
        {
            var v = Servicios(db).Ventas;
            await v.RegistrarAsync(new(new[] { new ServicioVentas.Linea(agua.Id, 2) }, MedioPago.Efectivo, socio.Id));
            var anular = await v.RegistrarAsync(new(new[] { new ServicioVentas.Linea(agua.Id, 5) }, MedioPago.Efectivo, socio.Id));
            await v.AnularAsync(anular.Venta!.Id, "Error de carga");
        }
        using (var db = b.Contexto())
        {
            var rep = new ReportesComerciales(db);
            var (importe, tickets) = await rep.DelDiaAsync(DateOnly.FromDateTime(DateTime.Now));
            Assert.Equal(2000, importe);
            Assert.Equal(1, tickets);

            var compras = await rep.ComprasDeSocioAsync(socio.Id);
            Assert.Equal(2000, compras.Total);
            Assert.Equal(1, compras.Tickets);
            Assert.Equal(2000, compras.TicketPromedio);
            Assert.Equal(2, compras.Lineas.Count);   // la anulada se lista, marcada
            Assert.Single(compras.Lineas, l => l.Anulada);
        }
    }
}
