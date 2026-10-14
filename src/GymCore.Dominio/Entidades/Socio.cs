using GymCore.Dominio.Enums;

namespace GymCore.Dominio.Entidades;

public class Socio
{

    public int Id { get; set; }
    public string Dni { get; set; } = null!;
    // Código de tarjeta para escaneo (preferible a DNI). Puede ser nulo si aún no se emitió tarjeta.
    public string? TarjetaCodigo { get; set; }
    public string Apellido { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    // Número visible de socio (credencial, listados). Las altas previas se completan con el Id.
    public int? NumeroSocio { get; set; }

    // Datos personales (opcionales: los socios existentes no los tienen)
    public string? FotoUrl { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public Sexo? Sexo { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? ContactoEmergenciaNombre { get; set; }
    public string? ContactoEmergenciaTelefono { get; set; }

    // Baja administrativa: null = socio activo. No afecta suscripciones ni pagos existentes.
    public DateTime? FechaBaja { get; set; }

    public ICollection<SocioPlan> Suscripciones { get; set; } = new List<SocioPlan>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    public ICollection<MedicionFisica> Mediciones { get; set; } = new List<MedicionFisica>();

    public bool DadoDeBaja => FechaBaja.HasValue;
    public string NombreCompleto => $"{Nombre} {Apellido}";
    public string Iniciales =>
        $"{(Nombre.Length > 0 ? Nombre[0] : ' ')}{(Apellido.Length > 0 ? Apellido[0] : ' ')}".Trim().ToUpperInvariant();
    public string CodigoCredencial => string.IsNullOrWhiteSpace(TarjetaCodigo) ? Dni : TarjetaCodigo;
    public int NumeroVisible => NumeroSocio ?? Id;
}
