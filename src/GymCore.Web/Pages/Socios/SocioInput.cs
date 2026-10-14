using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Web.Componentes;

namespace GymCore.Web.Pages.Socios;

public class SocioInput : IValidatableObject
{
    // Datos personales
    [Required(ErrorMessage = "Ingresá el nombre."), StringLength(80), Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el apellido."), StringLength(80), Display(Name = "Apellido")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el DNI."), StringLength(20), RegularExpression(@"^[0-9A-Za-z.\-]+$", ErrorMessage = "DNI inválido."), Display(Name = "DNI")]
    public string Dni { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Fecha de nacimiento")]
    public DateTime? FechaNacimiento { get; set; }

    [Display(Name = "Sexo")]
    public Sexo? Sexo { get; set; }

    [StringLength(40), Phone(ErrorMessage = "Teléfono inválido."), Display(Name = "Teléfono / WhatsApp")]
    public string? Telefono { get; set; }

    [StringLength(160), EmailAddress(ErrorMessage = "Email inválido."), Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(200), Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [StringLength(120), Display(Name = "Contacto de emergencia")]
    public string? ContactoEmergenciaNombre { get; set; }

    [StringLength(40), Phone(ErrorMessage = "Teléfono inválido."), Display(Name = "Teléfono de emergencia")]
    public string? ContactoEmergenciaTelefono { get; set; }

    public IFormFile? Foto { get; set; }
    public bool QuitarFoto { get; set; }

    // Información del gimnasio
    [Required(ErrorMessage = "Ingresá el número de socio."), Range(1, 9_999_999, ErrorMessage = "Número inválido."), Display(Name = "Número de socio")]
    public int? NumeroSocio { get; set; }

    [DataType(DataType.Date), Display(Name = "Fecha de alta")]
    public DateTime FechaAlta { get; set; } = DateTime.Today;

    // Solo en el alta: se crea la suscripción y la asignación con las reglas existentes
    [Display(Name = "Plan contratado")]
    public int? PlanId { get; set; }

    [Display(Name = "Entrenador asignado")]
    public int? EntrenadorId { get; set; }

    public bool Activo { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (FechaNacimiento.HasValue && (FechaNacimiento.Value > DateTime.Today || FechaNacimiento.Value < DateTime.Today.AddYears(-110)))
            yield return new ValidationResult("Fecha de nacimiento fuera de rango.", new[] { nameof(FechaNacimiento) });
        if (FechaAlta.Date > DateTime.Today.AddDays(30))
            yield return new ValidationResult("La fecha de alta no puede ser tan lejana.", new[] { nameof(FechaAlta) });
        if (FotoStorage.Validar(Foto) is { } error)
            yield return new ValidationResult(error, new[] { nameof(Foto) });
    }

    public static SocioInput Desde(Socio s) => new()
    {
        Nombre = s.Nombre,
        Apellido = s.Apellido,
        Dni = s.Dni,
        FechaNacimiento = s.FechaNacimiento,
        Sexo = s.Sexo,
        Telefono = s.Telefono,
        Email = s.Email,
        Direccion = s.Direccion,
        ContactoEmergenciaNombre = s.ContactoEmergenciaNombre,
        ContactoEmergenciaTelefono = s.ContactoEmergenciaTelefono,
        NumeroSocio = s.NumeroVisible,
        FechaAlta = s.FechaAlta.ToLocalTime().Date,
        Activo = !s.DadoDeBaja
    };

    public void AplicarA(Socio s)
    {
        static string? Limpio(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        s.Nombre = Nombre.Trim();
        s.Apellido = Apellido.Trim();
        s.Dni = Dni.Trim();
        s.FechaNacimiento = FechaNacimiento?.Date;
        s.Sexo = Sexo;
        s.Telefono = Limpio(Telefono);
        s.Email = Limpio(Email)?.ToLowerInvariant();
        s.Direccion = Limpio(Direccion);
        s.ContactoEmergenciaNombre = Limpio(ContactoEmergenciaNombre);
        s.ContactoEmergenciaTelefono = Limpio(ContactoEmergenciaTelefono);
        s.NumeroSocio = NumeroSocio;
        // Conserva la hora original si la fecha no cambió
        if (s.FechaAlta.ToLocalTime().Date != FechaAlta.Date)
            s.FechaAlta = DateTime.SpecifyKind(FechaAlta.Date, DateTimeKind.Local).ToUniversalTime();
        if (Activo) s.FechaBaja = null;
        else s.FechaBaja ??= DateTime.UtcNow;
    }
}
