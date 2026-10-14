using GymCore.Dominio.Enums;

namespace GymCore.Dominio.Entidades;

// Entrada del historial (timeline) del entrenador.
public class EntrenadorEvento
{
    public int Id { get; set; }

    public int EntrenadorId { get; set; }
    public Entrenador Entrenador { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public TipoEventoEntrenador Tipo { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Detalle { get; set; }
}
