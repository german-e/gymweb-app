namespace GymCore.Dominio.Entidades;

public class Socio
{

    public int Id { get; set; }
    public string Dni { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
    public ICollection<SocioPlan> Suscripciones { get; set; } = new List<SocioPlan>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
}


