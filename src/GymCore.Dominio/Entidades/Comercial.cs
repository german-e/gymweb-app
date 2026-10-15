using GymCore.Dominio.Enums;

namespace GymCore.Dominio.Entidades;

// ==================== Ventas e inventario ====================

public class CategoriaProducto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string Icono { get; set; } = "bi-box";   // ícono de Bootstrap Icons para el POS
    public bool Activa { get; set; } = true;
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

public enum EstadoStock { Normal, ProximoAlMinimo, Critico, SinStock }

public class Producto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;          // interno, único (ej. BEB-001)
    public string? CodigoBarras { get; set; }            // EAN/UPC, único; lo lee el escáner
    public string Nombre { get; set; } = null!;
    public int CategoriaId { get; set; }
    public CategoriaProducto Categoria { get; set; } = null!;
    public string? Marca { get; set; }
    public string? Descripcion { get; set; }
    public decimal PrecioCompra { get; set; }            // costo unitario de reposición
    public decimal PrecioVenta { get; set; }
    public int StockMinimo { get; set; }
    public bool Activo { get; set; } = true;
    public bool Destacado { get; set; }                  // botón rápido en el Punto de Venta
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    // Solo cambia a través de MovimientoStock (ServicioInventario): nunca se edita directo
    public int StockActual { get; private set; }

    public const decimal FactorProximo = 1.5m;   // "próximo al mínimo": hasta 50% por encima del mínimo

    public EstadoStock Estado => EstadoDe(StockActual, StockMinimo);

    public static EstadoStock EstadoDe(int stock, int minimo) => stock <= 0 ? EstadoStock.SinStock
        : minimo > 0 && stock <= minimo ? EstadoStock.Critico
        : minimo > 0 && stock <= Math.Ceiling(minimo * FactorProximo) ? EstadoStock.ProximoAlMinimo
        : EstadoStock.Normal;

    public decimal MargenUnitario => PrecioVenta - PrecioCompra;
    public decimal? MargenPorcentaje => PrecioVenta > 0 ? Math.Round(MargenUnitario * 100 / PrecioVenta, 1) : null;

    /// <summary>Aplica un movimiento y devuelve el stock resultante. No permite stock negativo.</summary>
    public int AplicarMovimiento(int cantidad)
    {
        if (StockActual + cantidad < 0)
            throw new InvalidOperationException($"Stock insuficiente de {Nombre}: hay {StockActual} y se necesitan {-cantidad}.");
        StockActual += cantidad;
        return StockActual;
    }
}

public enum TipoMovimientoStock { StockInicial = 1, Compra = 2, Venta = 3, Ajuste = 4, Correccion = 5, AnulacionVenta = 6 }

// Libro de inventario: cada cambio de stock deja un registro inmutable con su resultado.
public class MovimientoStock
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;   // UTC
    public TipoMovimientoStock Tipo { get; set; }
    public int Cantidad { get; set; }                         // con signo: + entra, − sale
    public int StockResultante { get; set; }
    public decimal? CostoUnitario { get; set; }               // compras: costo pagado
    public string? Motivo { get; set; }
    public string? Usuario { get; set; }                      // lo completa el DbContext
    public int? UsuarioId { get; set; }
    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }
}

public enum CanalVenta { Mostrador = 1, Online = 2 }

// Ticket de venta: productos y, opcionalmente, la cuota del socio (como Pago vinculado) en una sola operación.
public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;    // UTC
    public int? SocioId { get; set; }                         // null = cliente ocasional
    public Socio? Socio { get; set; }
    public string? ClienteNombre { get; set; }                // cliente ocasional (opcional)
    public MedioPago Medio { get; set; }
    public CanalVenta Canal { get; set; } = CanalVenta.Mostrador;
    public string? Observaciones { get; set; }
    public string? Usuario { get; set; }                      // cajero (lo completa el servicio)
    public int? UsuarioId { get; set; }

    public ICollection<VentaItem> Items { get; set; } = new List<VentaItem>();
    public Pago? Cuota { get; set; }                          // pago de membresía incluido en el ticket

    public decimal TotalProductos { get; set; }
    public decimal Total { get; set; }                        // productos + cuota

    public bool Anulada { get; private set; }
    public DateTime? FechaAnulacion { get; private set; }
    public string? MotivoAnulacion { get; private set; }
    public string? UsuarioAnulo { get; private set; }

    public string Numero => $"V-{Id:D6}";

    public void Anular(string motivo, string usuario, DateTime fechaUtc)
    {
        if (Anulada) throw new InvalidOperationException("La venta ya está anulada.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("El motivo es obligatorio.", nameof(motivo));
        Anulada = true;
        FechaAnulacion = fechaUtc;
        MotivoAnulacion = motivo.Trim().Length > Pago.MotivoMaxLength ? motivo.Trim()[..Pago.MotivoMaxLength] : motivo.Trim();
        UsuarioAnulo = usuario;
    }
}

public class VentaItem
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public Venta Venta { get; set; } = null!;
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public string Descripcion { get; set; } = null!;   // nombre al momento de la venta
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }         // precio cobrado (congelado)
    public decimal CostoUnitario { get; set; }          // costo a la fecha: margen histórico real
    public decimal Subtotal => Cantidad * PrecioUnitario;
    public decimal Margen => Cantidad * (PrecioUnitario - CostoUnitario);
}
