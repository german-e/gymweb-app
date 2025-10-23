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

    // Posibles campos de auditoría
    public string? UsuarioCajero { get; set; }
}
