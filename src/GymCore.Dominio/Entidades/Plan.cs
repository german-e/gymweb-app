using GymCore.Dominio.Enums;
using GymCore.Dominio.ValueObjects;

namespace GymCore.Dominio.Entidades;

public class Plan
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;     // "Mensual 3xSemana", "Mensual Ilimitado", "Pase Diario"
    public string? Descripcion { get; set; }

    public TipoPlan Tipo { get; set; }

    // Precio base del ciclo (mensual o diario)
    public decimal Precio { get; set; }             // podés usar Dinero si querés VO
    public string Moneda { get; set; } = "ARS";

    // Reglas
    public int? MaxDiasPorSemana { get; set; }      // solo aplica a MensualDiasPorSemana (p.ej. 3)
    public bool CuentaFeriados { get; set; } = false;

    // Ciclo
    public int DuracionDias { get; set; }           // 30 para mensuales, 1 para pase diario

    // Flags útiles
    public bool RequiereRenovacionAutomatica { get; set; } = false;  // si implementás auto-renovación
    public bool PermiteAccesoSabado { get; set; } = true;
    public bool PermiteAccesoDomingo { get; set; } = true;

    // Navegación
    public ICollection<SocioPlan> Suscripciones { get; set; } = new List<SocioPlan>();
}