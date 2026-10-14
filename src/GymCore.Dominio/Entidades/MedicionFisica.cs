namespace GymCore.Dominio.Entidades;

// Registro de evolución física del socio (base para evaluaciones físicas y seguimiento de objetivos).
public class MedicionFisica
{
    public int Id { get; set; }

    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public decimal? PesoKg { get; set; }
    public decimal? AlturaCm { get; set; }
    public decimal? MasaMuscularKg { get; set; }
    public decimal? PorcentajeGraso { get; set; }
    public string? Observaciones { get; set; }

    public int? EntrenadorId { get; set; }   // quién tomó la medición (opcional)
    public Entrenador? Entrenador { get; set; }

    public decimal? Imc =>
        PesoKg is > 0 && AlturaCm is > 0
            ? Math.Round(PesoKg.Value / ((AlturaCm.Value / 100m) * (AlturaCm.Value / 100m)), 1)
            : null;
}
