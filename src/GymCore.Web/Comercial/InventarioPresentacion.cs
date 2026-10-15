using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Comercial;

// Textos, íconos y colores del módulo comercial (estado siempre con ícono + texto, nunca solo color)
public static class InventarioPresentacion
{
    public static (string Texto, string Icono, string Variante) Estilo(this EstadoStock e) => e switch
    {
        EstadoStock.SinStock => ("Sin stock", "bi-x-octagon-fill", "danger"),
        EstadoStock.Critico => ("Stock crítico", "bi-exclamation-triangle-fill", "warning"),
        EstadoStock.ProximoAlMinimo => ("Próximo al mínimo", "bi-exclamation-circle", "info"),
        _ => ("Normal", "bi-check-circle", "success")
    };

    // Indicador visual de la alerta (🔴 🟠 🟡)
    public static string Punto(this EstadoStock e) => e switch
    {
        EstadoStock.SinStock => "stock-dot sin", EstadoStock.Critico => "stock-dot critico",
        EstadoStock.ProximoAlMinimo => "stock-dot proximo", _ => "stock-dot ok"
    };

    public static string Texto(TipoMovimientoStock t) => t switch
    {
        TipoMovimientoStock.StockInicial => "Stock inicial",
        TipoMovimientoStock.Compra => "Compra",
        TipoMovimientoStock.Venta => "Venta",
        TipoMovimientoStock.Ajuste => "Ajuste",
        TipoMovimientoStock.Correccion => "Corrección",
        TipoMovimientoStock.AnulacionVenta => "Anulación de venta",
        _ => t.ToString()
    };

    public static (string Icono, string Variante) Estilo(this TipoMovimientoStock t) => t switch
    {
        TipoMovimientoStock.StockInicial => ("bi-box-arrow-in-down", "primary"),
        TipoMovimientoStock.Compra => ("bi-truck", "success"),
        TipoMovimientoStock.Venta => ("bi-cart-check", "primary"),
        TipoMovimientoStock.Ajuste => ("bi-sliders", "warning"),
        TipoMovimientoStock.Correccion => ("bi-clipboard-check", "info"),
        TipoMovimientoStock.AnulacionVenta => ("bi-arrow-counterclockwise", "danger"),
        _ => ("bi-dot", "muted")
    };

    private static readonly System.Globalization.CultureInfo Es = new("es-AR");

    // Precio con centavos solo cuando los tiene (el ticket debe cerrar al centavo)
    public static string Precio(decimal v) => "$ " + v.ToString(v == decimal.Truncate(v) ? "N0" : "N2", Es);

    public static string Unidades(int n) => $"{n:N0} {(Math.Abs(n) == 1 ? "unidad" : "unidades")}";
    public static string ConSigno(int n) => n > 0 ? $"+{n:N0}" : n.ToString("N0");

    // Motivos frecuentes de ajuste (el usuario puede escribir otro)
    public static readonly string[] MotivosAjuste =
        { "Rotura o daño", "Vencimiento", "Consumo interno", "Muestra o degustación", "Robo o faltante", "Devolución de cliente" };

    public static readonly string[] MotivosAnulacion =
        { "Error de carga", "Devolución del cliente", "Cobro duplicado", "Medio de pago incorrecto", "Otro" };

    // Íconos sugeridos para categorías
    public static readonly (string Icono, string Nombre)[] IconosCategoria =
    {
        ("bi-cup-straw", "Bebida"), ("bi-capsule", "Suplemento"), ("bi-egg-fried", "Nutrición"), ("bi-bag", "Accesorio"),
        ("bi-person-standing-dress", "Indumentaria"), ("bi-droplet", "Hidratación"), ("bi-heart-pulse", "Salud"),
        ("bi-lightning-charge", "Energía"), ("bi-gift", "Regalo"), ("bi-box", "General")
    };

    // Categorías iniciales: solo se crean si no existe ninguna (nunca productos de ejemplo)
    public static readonly (string Nombre, string Icono, string Descripcion)[] CategoriasIniciales =
    {
        ("Bebidas", "bi-cup-straw", "Agua, isotónicas y bebidas energéticas."),
        ("Suplementos", "bi-capsule", "Proteínas, creatina, aminoácidos y pre-entrenos."),
        ("Nutrición", "bi-egg-fried", "Barras, snacks y alimentos."),
        ("Accesorios", "bi-bag", "Guantes, shakers, toallas, candados."),
        ("Indumentaria", "bi-person-standing-dress", "Remeras, buzos y ropa deportiva.")
    };

    public static async Task SembrarCategoriasAsync(GymDbContext db)
    {
        if (await db.CategoriasProducto.AnyAsync()) return;
        foreach (var (nombre, icono, desc) in CategoriasIniciales)
            db.CategoriasProducto.Add(new CategoriaProducto { Nombre = nombre, Icono = icono, Descripcion = desc });
        await db.SaveChangesAsync();
    }
}

/// <summary>Línea de tiempo de movimientos de stock (ficha del producto y pantalla Movimientos).</summary>
public record MovimientosVm(IReadOnlyList<MovimientoStock> Movimientos, bool MostrarProducto = true,
    string TextoVacio = "Todavía no hay movimientos de stock.");
