using GymCore.Dominio.Enums;

namespace GymCore.Dominio.Entidades;

public class SocioPlan
{
    public int Id { get; set; }

    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;

    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public DateTime FechaInicio { get; set; }       // día que empieza a correr
    public DateTime FechaFin { get; set; }          // FechaInicio + DuracionDias (o ajustado por cortes)
    public DateTime? ProximoVencimiento { get; set; }  // util para renovaciones

    public EstadoSuscripcion Estado { get; set; } = EstadoSuscripcion.Activo;

    // Controles de uso (para 3xSemana)
    public int? ConteoSemanalUsos { get; set; }     // cuántos check-ins en la semana actual
    public DateTime? SemanaConteoDesde { get; set; } // lunes (o día que definas) de la semana de conteo

    // Para Pase Diario, podés simplificar: Inicio=Fin=ese día y Estado pasa a Vencido al día siguiente.
}
