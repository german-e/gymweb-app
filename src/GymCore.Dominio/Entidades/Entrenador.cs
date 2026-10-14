using GymCore.Dominio.Enums;

namespace GymCore.Dominio.Entidades;

public class Entrenador
{
    public int Id { get; set; }

    // Datos personales
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string Dni { get; set; } = null!;
    public DateTime? FechaNacimiento { get; set; }
    public Sexo Sexo { get; set; } = Sexo.NoInformado;
    public string? FotoUrl { get; set; }          // ruta relativa dentro de wwwroot
    public string? Telefono { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    // Información profesional
    public string? Especialidad { get; set; }
    public string? Certificaciones { get; set; }  // una por línea
    public string? InstitucionFormadora { get; set; }
    public int? AniosExperiencia { get; set; }
    public string? DescripcionProfesional { get; set; }
    public string? Observaciones { get; set; }

    // Disponibilidad
    public DiasSemana DiasTrabajo { get; set; } = DiasSemana.Ninguno;
    public TimeOnly? HorarioDesde { get; set; }
    public TimeOnly? HorarioHasta { get; set; }
    public Turno? Turno { get; set; }

    // Estado
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
    public DateTime? FechaBaja { get; set; }

    public ICollection<EntrenadorSocio> Asignaciones { get; set; } = new List<EntrenadorSocio>();
    public ICollection<EntrenadorEvento> Historial { get; set; } = new List<EntrenadorEvento>();

    public string NombreCompleto => $"{Nombre} {Apellido}";

    public string Iniciales =>
        $"{(Nombre.Length > 0 ? Nombre[0] : ' ')}{(Apellido.Length > 0 ? Apellido[0] : ' ')}".Trim().ToUpperInvariant();

    public IEnumerable<string> ListaCertificaciones =>
        (Certificaciones ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Horas semanales según días de trabajo y franja horaria
    public double HorasSemanales
    {
        get
        {
            if (HorarioDesde is null || HorarioHasta is null) return 0;
            var horas = (HorarioHasta.Value - HorarioDesde.Value).TotalHours; // TimeOnly resta circular: cubre turnos que cruzan medianoche
            var dias = Enum.GetValues<DiasSemana>().Count(d => d != DiasSemana.Ninguno && DiasTrabajo.HasFlag(d));
            return Math.Round(horas * dias, 1);
        }
    }
}
