using System.Net;
using System.Text.RegularExpressions;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Web.Comercial;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Ventas e inventario de punta a punta: permisos por pantalla y flujo completo del Punto de Venta.
[Collection(AppDePrueba.Coleccion)]
public class VentasIntegracionTests : IClassFixture<AppDePrueba>
{
    private readonly AppDePrueba _app;
    public VentasIntegracionTests(AppDePrueba app) => _app = app;

    private static async Task<string> TokenAsync(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, string paginaToken, Dictionary<string, string> datos)
    {
        datos["__RequestVerificationToken"] = await TokenAsync(c, paginaToken);
        return await c.PostAsync(url, new FormUrlEncodedContent(datos));
    }

    private static async Task<HttpClient> SesionAsync(AppDePrueba app, string usuario)
    {
        var c = app.Cliente();
        await PostAsync(c, "/Cuenta/Login", "/Cuenta/Login", new() { ["NombreUsuario"] = usuario, ["Password"] = AppDePrueba.Clave });
        return c;
    }

    private static string? Destino(HttpResponseMessage r) => r.Headers.Location?.OriginalString;

    private int Producto(string nombre, int stock, decimal precio = 1500) => _app.Db(db =>
    {
        var p = new Producto
        {
            Codigo = "IT-" + Guid.NewGuid().ToString("N")[..6], CodigoBarras = "779" + Random.Shared.Next(100000000, 999999999),
            Nombre = nombre, Categoria = db.CategoriasProducto.First(), PrecioVenta = precio, PrecioCompra = 800, StockMinimo = 2
        };
        db.Productos.Add(p);
        new ServicioInventario(db).Registrar(p, TipoMovimientoStock.StockInicial, stock, "Alta");
        db.SaveChanges();
        return p.Id;
    });

    [Fact]
    public void Se_crean_las_categorias_iniciales()
    {
        var nombres = _app.Db(db => db.CategoriasProducto.Select(c => c.Nombre).ToList());
        foreach (var n in new[] { "Bebidas", "Suplementos", "Nutrición", "Accesorios", "Indumentaria" })
            Assert.Contains(n, nombres);
    }

    [Fact]
    public async Task Recepcion_vende_desde_el_POS_y_el_stock_se_descuenta()
    {
        var id = Producto("Isotónica", stock: 6);
        var c = await SesionAsync(_app, "recepcion");

        var pos = WebUtility.HtmlDecode(await c.GetStringAsync("/Ventas/Pos"));
        Assert.Contains($"\"id\":{id},", pos);          // el catálogo viaja como JSON al POS
        Assert.Contains("Punto de Venta", pos);

        var r = await PostAsync(c, "/Ventas/Pos", "/Ventas/Pos", new()
        {
            ["Lineas"] = $"[{{\"id\":{id},\"cantidad\":2}}]",
            ["SocioId"] = _app.SocioAsignado.ToString(),
            ["Medio"] = "Efectivo"
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Ventas/Ticket/", Destino(r));

        Assert.Equal(4, _app.Db(db => db.Productos.Single(p => p.Id == id).StockActual));
        var venta = _app.Db(db => db.Ventas.Include(v => v.Items).OrderByDescending(v => v.Id).First());
        Assert.Equal(3000, venta.Total);
        Assert.Equal("recepcion Test", venta.Usuario);
        Assert.Equal(_app.SocioAsignado, venta.SocioId);

        var ticket = WebUtility.HtmlDecode(await c.GetStringAsync(Destino(r)!));
        Assert.Contains(venta.Numero, ticket);
        Assert.DoesNotContain("Anular venta", ticket);   // recepción no anula

        // La venta aparece en la ficha del socio (pestaña Compras)
        var ficha = WebUtility.HtmlDecode(await c.GetStringAsync($"/Socios/Detalle/{_app.SocioAsignado}"));
        Assert.Contains("data-bs-target=\"#tab-compras\"", ficha);
        Assert.Contains("Isotónica", ficha);
    }

    [Fact]
    public async Task El_navegador_no_puede_imponer_precios_ni_superar_el_stock()
    {
        var id = Producto("Barra", stock: 1, precio: 900);
        var c = await SesionAsync(_app, "recepcion");
        var r = await PostAsync(c, "/Ventas/Pos", "/Ventas/Pos", new()
        {
            ["Lineas"] = $"[{{\"id\":{id},\"cantidad\":3,\"precio\":1}}]",
            ["Medio"] = "Efectivo"
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);   // vuelve a la pantalla con el error
        Assert.Contains("Stock insuficiente de Barra", WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
        Assert.Equal(1, _app.Db(db => db.Productos.Single(p => p.Id == id).StockActual));
    }

    [Fact]
    public async Task Recepcion_no_administra_inventario_ni_ve_reportes_comerciales_ni_anula()
    {
        var c = await SesionAsync(_app, "recepcion");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Ventas/Productos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Ventas/Inventario")).StatusCode);
        foreach (var url in new[] { "/Ventas/Reportes", "/Ventas/Movimientos", "/Ventas/Productos/Editar" })
            Assert.Contains("/Cuenta/AccesoDenegado", Destino(await c.GetAsync(url)));

        var id = Producto("Toalla", stock: 3);
        var ajuste = await PostAsync(c, "/Ventas/Inventario?handler=Ajustar", "/Ventas/Pos",
            new() { ["ProductoId"] = id.ToString(), ["Tipo"] = "Compra", ["Cantidad"] = "50" });
        Assert.Contains("/Cuenta/AccesoDenegado", Destino(ajuste));
        Assert.Equal(3, _app.Db(db => db.Productos.Single(p => p.Id == id).StockActual));

        var menu = WebUtility.HtmlDecode(await c.GetStringAsync("/Ventas/Pos"));
        Assert.DoesNotContain("Reportes comerciales", menu);
        Assert.DoesNotContain(">Movimientos<", menu);
    }

    [Fact]
    public async Task Entrenador_no_accede_al_modulo_de_ventas()
    {
        var c = await SesionAsync(_app, "entrenador");
        foreach (var url in new[] { "/Ventas/Pos", "/Ventas", "/Ventas/Productos", "/Ventas/Inventario" })
            Assert.Contains("/Cuenta/AccesoDenegado", Destino(await c.GetAsync(url)));
        var panel = WebUtility.HtmlDecode(await c.GetStringAsync("/panel"));
        Assert.DoesNotContain("Punto de Venta", panel);
        Assert.DoesNotContain("Alertas de inventario", panel);
    }

    [Fact]
    public async Task Administrador_ajusta_stock_anula_ventas_y_ve_reportes()
    {
        var id = Producto("Creatina", stock: 1, precio: 30000);
        var c = await SesionAsync(_app, "admin");

        // Stock crítico → aparece en el panel y en la campana
        var panel = WebUtility.HtmlDecode(await c.GetStringAsync("/panel"));
        Assert.Contains("Alertas de inventario", panel);
        Assert.Contains("Creatina", panel);
        Assert.Contains("Stock crítico", panel);

        var compra = await PostAsync(c, "/Ventas/Inventario?handler=Ajustar", "/Ventas/Inventario", new()
        {
            ["ProductoId"] = id.ToString(), ["Tipo"] = "Compra", ["Cantidad"] = "10", ["CostoUnitario"] = "21000",
            ["ActualizarCosto"] = "true", ["Volver"] = $"/Ventas/Productos/Detalle/{id}"
        });
        Assert.Equal($"/Ventas/Productos/Detalle/{id}", Destino(compra));
        Assert.Equal(11, _app.Db(db => db.Productos.Single(p => p.Id == id).StockActual));
        Assert.Equal(21000, _app.Db(db => db.Productos.Single(p => p.Id == id).PrecioCompra));

        var venta = await PostAsync(c, "/Ventas/Pos", "/Ventas/Pos", new()
        {
            ["Lineas"] = $"[{{\"id\":{id},\"cantidad\":1}}]", ["Medio"] = "MercadoPago", ["ClienteNombre"] = "Visitante"
        });
        var ventaId = int.Parse(Regex.Match(Destino(venta)!, @"/Ventas/Ticket/(\d+)").Groups[1].Value);
        Assert.Equal(10, _app.Db(db => db.Productos.Single(p => p.Id == id).StockActual));

        var anular = await PostAsync(c, $"/Ventas/Ticket/{ventaId}?handler=Anular", $"/Ventas/Ticket/{ventaId}",
            new() { ["motivo"] = "Devolución del cliente" });
        Assert.Equal(HttpStatusCode.Redirect, anular.StatusCode);
        Assert.True(_app.Db(db => db.Ventas.Single(v => v.Id == ventaId).Anulada));
        Assert.Equal(11, _app.Db(db => db.Productos.Single(p => p.Id == id).StockActual));

        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Ventas/Reportes")).StatusCode);
        var movimientos = WebUtility.HtmlDecode(await c.GetStringAsync($"/Ventas/Movimientos?producto={id}"));
        Assert.Contains("Anulación de venta", movimientos);
        Assert.Contains("Compra", movimientos);

        var auditoria = WebUtility.HtmlDecode(await c.GetStringAsync("/Administracion/Auditoria"));
        Assert.Contains("Venta anulada", auditoria);
        Assert.Contains("Movimiento de stock", auditoria);
    }

    [Fact]
    public async Task Volver_tras_un_ajuste_solo_acepta_urls_locales()
    {
        var id = Producto("Shaker", stock: 2);
        var c = await SesionAsync(_app, "admin");
        var r = await PostAsync(c, "/Ventas/Inventario?handler=Ajustar", "/Ventas/Inventario", new()
        {
            ["ProductoId"] = id.ToString(), ["Tipo"] = "Ajuste", ["Cantidad"] = "-1", ["Motivo"] = "Rotura", ["Volver"] = "https://malicioso.example/"
        });
        Assert.DoesNotContain("malicioso", Destino(r));
        Assert.Equal(1, _app.Db(db => db.Productos.Single(p => p.Id == id).StockActual));
    }
}
