using GymCore.Dominio.ValueObjects;
using GymCore.Dominio.Enums;

namespace GymCore.Dominio.Entidades;

public class Pago
{
    public int Id { get; set; }

    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;

    public int? SocioPlanId { get; set; }   // opcional: pago asociado a una suscripción
    public SocioPlan? SocioPlan { get; set; }

    public decimal Importe { get; set; }
    public string Moneda { get; set; } = "ARS";

    public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    public MedioPago Medio { get; set; }

    public string? ReferenciaExterna { get; set; }  // id de MP/transferencia/últimos 4 tarjeta
    public string? Observaciones { get; set; }

    // Auditoría
    public string? UsuarioCajero { get; set; }

    // Cobrado dentro de un ticket del Punto de Venta (cuota + productos en una sola operación)
    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }

    // Anulación lógica: el pago nunca se borra. Un pago anulado no cuenta en recaudación, saldos ni indicadores
    // (filtro global en PagoConfig); solo se ve en listados, detalle e historial.
    public bool Anulado { get; private set; }
    public DateTime? FechaAnulacion { get; private set; }   // UTC
    public string? MotivoAnulacion { get; private set; }
    public string? UsuarioAnulo { get; private set; }

    public const int MotivoMaxLength = 300;

    public void Anular(string motivo, string usuario, DateTime fechaUtc)
    {
        if (Anulado) throw new InvalidOperationException("El pago ya está anulado.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(motivo));
        if (string.IsNullOrWhiteSpace(usuario)) throw new ArgumentException("El usuario que anula es obligatorio.", nameof(usuario));
        motivo = motivo.Trim();
        Anulado = true;
        FechaAnulacion = fechaUtc;
        MotivoAnulacion = motivo.Length > MotivoMaxLength ? motivo[..MotivoMaxLength] : motivo;
        UsuarioAnulo = usuario.Trim();
    }
}
