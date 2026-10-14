using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;

namespace GymCore.Web.Pages.Entrenadores;

public class EntrenadorInput : IValidatableObject
{
    // Datos personales
    [Required(ErrorMessage = "Ingresá el nombre."), StringLength(120), Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el apellido."), StringLength(120), Display(Name = "Apellido")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el DNI."), StringLength(20), RegularExpression(@"^[0-9A-Za-z.\-]+$", ErrorMessage = "DNI inválido."), Display(Name = "DNI")]
    public string Dni { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Fecha de nacimiento")]
    public DateTime? FechaNacimiento { get; set; }

    [Display(Name = "Sexo")]
    public Sexo Sexo { get; set; } = Sexo.NoInformado;

    [StringLength(40), Phone(ErrorMessage = "Teléfono inválido."), Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [StringLength(40), Phone(ErrorMessage = "Celular inválido."), Display(Name = "Celular")]
    public string? Celular { get; set; }

    [StringLength(160), EmailAddress(ErrorMessage = "Email inválido."), Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(200), Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    // Información profesional
    [StringLength(80), Display(Name = "Especialidad principal")]
    public string? Especialidad { get; set; }

    [StringLength(2000), Display(Name = "Certificaciones")]
    public string? Certificaciones { get; set; }

    [StringLength(160), Display(Name = "Institución formadora")]
    public string? InstitucionFormadora { get; set; }

    [Range(0, 60, ErrorMessage = "Entre 0 y 60 años."), Display(Name = "Años de experiencia")]
    public int? AniosExperiencia { get; set; }

    [StringLength(2000), Display(Name = "Descripción profesional")]
    public string? DescripcionProfesional { get; set; }

    [StringLength(2000), Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    // Disponibilidad
    public List<DiasSemana> Dias { get; set; } = new();

    [DataType(DataType.Time), Display(Name = "Horario desde")]
    public TimeOnly? HorarioDesde { get; set; }

    [DataType(DataType.Time), Display(Name = "Horario hasta")]
    public TimeOnly? HorarioHasta { get; set; }

    [Display(Name = "Turno")]
    public Turno? Turno { get; set; }

    // Estado
    public bool Activo { get; set; } = true;

    [DataType(DataType.Date), Display(Name = "Fecha de alta")]
    public DateTime FechaAlta { get; set; } = DateTime.Today;

    [DataType(DataType.Date), Display(Name = "Fecha de baja")]
    public DateTime? FechaBaja { get; set; }

    // Foto
    public IFormFile? Foto { get; set; }
    public bool QuitarFoto { get; set; }

    public DiasSemana DiasFlags => Dias.Aggregate(DiasSemana.Ninguno, (acc, d) => acc | d);

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (HorarioDesde.HasValue != HorarioHasta.HasValue)
            yield return new ValidationResult("Completá ambos horarios o dejalos vacíos.", new[] { nameof(HorarioHasta) });
        else if (HorarioDesde.HasValue && HorarioDesde == HorarioHasta)
            yield return new ValidationResult("El horario de fin debe ser distinto al de inicio.", new[] { nameof(HorarioHasta) });

        if (FechaNacimiento.HasValue && (FechaNacimiento.Value > DateTime.Today.AddYears(-16) || FechaNacimiento.Value < DateTime.Today.AddYears(-100)))
            yield return new ValidationResult("Fecha de nacimiento fuera de rango.", new[] { nameof(FechaNacimiento) });

        if (FechaBaja.HasValue && FechaBaja.Value.Date < FechaAlta.Date)
            yield return new ValidationResult("La baja no puede ser anterior al alta.", new[] { nameof(FechaBaja) });

        if (Foto is { Length: > 0 })
        {
            var ext = Path.GetExtension(Foto.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                yield return new ValidationResult("Formato no soportado (JPG, PNG o WEBP).", new[] { nameof(Foto) });
            if (Foto.Length > 2 * 1024 * 1024)
                yield return new ValidationResult("La foto no puede superar 2 MB.", new[] { nameof(Foto) });
        }
    }

    public static EntrenadorInput Desde(Entrenador e) => new()
    {
        Nombre = e.Nombre,
        Apellido = e.Apellido,
        Dni = e.Dni,
        FechaNacimiento = e.FechaNacimiento,
        Sexo = e.Sexo,
        Telefono = e.Telefono,
        Celular = e.Celular,
        Email = e.Email,
        Direccion = e.Direccion,
        Especialidad = e.Especialidad,
        Certificaciones = e.Certificaciones,
        InstitucionFormadora = e.InstitucionFormadora,
        AniosExperiencia = e.AniosExperiencia,
        DescripcionProfesional = e.DescripcionProfesional,
        Observaciones = e.Observaciones,
        Dias = EntrenadorPresentacion.Dias.Select(d => d.Dia).Where(d => e.DiasTrabajo.HasFlag(d)).ToList(),
        HorarioDesde = e.HorarioDesde,
        HorarioHasta = e.HorarioHasta,
        Turno = e.Turno,
        Activo = e.Activo,
        FechaAlta = e.FechaAlta.ToLocalTime().Date,
        FechaBaja = e.FechaBaja?.ToLocalTime().Date
    };

    public void AplicarA(Entrenador e)
    {
        static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        e.Nombre = Nombre.Trim();
        e.Apellido = Apellido.Trim();
        e.Dni = Dni.Trim();
        e.FechaNacimiento = FechaNacimiento?.Date;
        e.Sexo = Sexo;
        e.Telefono = Limpio(Telefono);
        e.Celular = Limpio(Celular);
        e.Email = Limpio(Email)?.ToLowerInvariant();
        e.Direccion = Limpio(Direccion);
        e.Especialidad = Limpio(Especialidad);
        e.Certificaciones = Limpio(Certificaciones?.Replace("\r", ""));
        e.InstitucionFormadora = Limpio(InstitucionFormadora);
        e.AniosExperiencia = AniosExperiencia;
        e.DescripcionProfesional = Limpio(DescripcionProfesional);
        e.Observaciones = Limpio(Observaciones);
        e.DiasTrabajo = DiasFlags;
        e.HorarioDesde = HorarioDesde;
        e.HorarioHasta = HorarioHasta;
        e.Turno = Turno;
        e.Activo = Activo;
        // Fechas de UI en hora local → se guardan en UTC como el resto del sistema
        e.FechaAlta = DateTime.SpecifyKind(FechaAlta.Date, DateTimeKind.Local).ToUniversalTime();
        e.FechaBaja = Activo ? null : DateTime.SpecifyKind((FechaBaja ?? DateTime.Today).Date, DateTimeKind.Local).ToUniversalTime();
    }
}
