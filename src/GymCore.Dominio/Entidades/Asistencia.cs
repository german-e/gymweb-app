

namespace GymCore.Dominio.Entidades;

public class Asistencia
{
    public int Id { get; set; }

    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;

    public DateTime FechaHoraIngreso { get; set; } = DateTime.UtcNow;

    public int? SocioPlanId { get; set; }   // para saber con qué plan se validó el acceso
    public SocioPlan? SocioPlan { get; set; }

    public string? Observaciones { get; set; }
}
