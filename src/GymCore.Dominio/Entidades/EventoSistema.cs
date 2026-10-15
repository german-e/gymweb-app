using GymCore.Dominio.Enums;

namespace GymCore.Dominio.Entidades;

// Registro de auditoría: quién hizo qué, cuándo y sobre qué entidad.
// No tiene claves foráneas a propósito: el evento sobrevive aunque la entidad se elimine
// (por eso guarda una copia del nombre). Solo se inserta; nunca se modifica ni se borra.
public class EventoSistema
{
    public long Id { get; set; }

    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;

    // Responsable. Si queda vacío, el DbContext lo completa con el usuario autenticado.
    // Usuario es la copia del nombre (legible aunque el usuario se renombre); UsuarioId lo identifica.
    public string? Usuario { get; set; }
    public int? UsuarioId { get; set; }

    public TipoEventoSistema Tipo { get; set; }

    public EntidadAuditada Entidad { get; set; }
    public int? EntidadId { get; set; }
    public string EntidadNombre { get; set; } = string.Empty;   // copia al momento del evento

    // Socio involucrado (pagos, suscripciones, asignaciones): alimenta el historial del socio
    public int? SocioId { get; set; }

    public string? Resumen { get; set; }   // una línea: "$ 20.000 · Efectivo · Cuota Mensual"
    public string? Detalle { get; set; }   // texto libre, multilínea

    // Entidad recién creada cuyo Id todavía no existe. El DbContext completa EntidadId al guardar (no se persiste).
    public object? Referencia { get; set; }
}
