namespace GymCore.Dominio.Entidades;

// Asignación socio ↔ entrenador. Se conserva el registro al desasignar (Activa = false)
// para mantener la trazabilidad y como base para rutinas, seguimientos y comisiones.
public class EntrenadorSocio
{
    public int Id { get; set; }

    public int EntrenadorId { get; set; }
    public Entrenador Entrenador { get; set; } = null!;

    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;

    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public bool Activa { get; set; } = true;

    public string? Objetivo { get; set; }   // objetivo principal del socio con este entrenador
}
