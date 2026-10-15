using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;

namespace GymCore.Web.Pages.Entrenadores;

// Textos, íconos y colores del módulo. Centralizado para que listado, ficha y formularios hablen igual.
public static class EntrenadorPresentacion
{
    public static readonly string[] EspecialidadesSugeridas =
    {
        "Musculación", "Entrenamiento funcional", "CrossFit", "Yoga", "Pilates", "Spinning",
        "Boxeo", "Artes marciales", "Natación", "Running", "Rehabilitación / Kinesiología",
        "Nutrición deportiva", "Personal trainer"
    };

    public static readonly (DiasSemana Dia, string Corto, string Largo)[] Dias =
    {
        (DiasSemana.Lunes, "Lu", "Lunes"),
        (DiasSemana.Martes, "Ma", "Martes"),
        (DiasSemana.Miercoles, "Mi", "Miércoles"),
        (DiasSemana.Jueves, "Ju", "Jueves"),
        (DiasSemana.Viernes, "Vi", "Viernes"),
        (DiasSemana.Sabado, "Sá", "Sábado"),
        (DiasSemana.Domingo, "Do", "Domingo"),
    };

    public static string Texto(this Turno? t) => t switch
    {
        Turno.Manana => "Mañana",
        Turno.Tarde => "Tarde",
        Turno.Noche => "Noche",
        Turno.Completo => "Jornada completa",
        Turno.Rotativo => "Rotativo",
        _ => "—"
    };

    public static string Texto(this Sexo s) => s switch
    {
        Sexo.Masculino => "Masculino",
        Sexo.Femenino => "Femenino",
        Sexo.Otro => "Otro",
        _ => "No informado"
    };

    public static string DiasTexto(DiasSemana dias)
    {
        if (dias == DiasSemana.Ninguno) return "Sin definir";
        var lista = Dias.Where(d => dias.HasFlag(d.Dia)).Select(d => d.Corto).ToList();
        return string.Join(" · ", lista);
    }

    public static string HorarioTexto(Entrenador e) =>
        e.HorarioDesde is null || e.HorarioHasta is null
            ? "Sin horario"
            : $"{e.HorarioDesde:HH\\:mm} – {e.HorarioHasta:HH\\:mm}";

    public static string Antiguedad(DateTime desdeUtc, DateTime? hastaUtc = null)
    {
        var desde = desdeUtc.ToLocalTime().Date;
        var hasta = (hastaUtc?.ToLocalTime() ?? DateTime.Now).Date;
        if (hasta < desde) return "—";
        var meses = (hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month - (hasta.Day < desde.Day ? 1 : 0);
        if (meses < 1) return $"{(hasta - desde).Days} d";
        if (meses < 12) return $"{meses} m";
        var anios = meses / 12; var resto = meses % 12;
        return resto == 0 ? $"{anios} a" : $"{anios} a {resto} m";
    }

    public static int? Edad(DateTime? nacimiento)
    {
        if (nacimiento is null) return null;
        var hoy = DateTime.Today;
        var edad = hoy.Year - nacimiento.Value.Year;
        if (nacimiento.Value.Date > hoy.AddYears(-edad)) edad--;
        return edad;
    }

    public static (string Icono, string Variante) Estilo(TipoEventoEntrenador tipo) => tipo switch
    {
        TipoEventoEntrenador.Alta => ("bi-person-plus", "success"),
        TipoEventoEntrenador.Modificacion => ("bi-pencil", "primary"),
        TipoEventoEntrenador.CambioHorario => ("bi-clock-history", "info"),
        TipoEventoEntrenador.AsignacionSocio => ("bi-person-check", "success"),
        TipoEventoEntrenador.DesasignacionSocio => ("bi-person-dash", "warning"),
        TipoEventoEntrenador.Observacion => ("bi-chat-left-text", "muted"),
        TipoEventoEntrenador.Activacion => ("bi-toggle-on", "success"),
        TipoEventoEntrenador.Desactivacion => ("bi-toggle-off", "danger"),
        _ => ("bi-dot", "muted")
    };

    // Historial propio del entrenador + evento de auditoría del sistema (mismo SaveChanges).
    // socioId: socio involucrado cuando la acción afecta a uno solo (asignaciones).
    public static void RegistrarEvento(this GymDbContext db, Entrenador entrenador, TipoEventoEntrenador tipo, string titulo, string? detalle = null, int? socioId = null)
    {
        db.EntrenadorEventos.Add(new EntrenadorEvento
        {
            Entrenador = entrenador,
            Tipo = tipo,
            Titulo = titulo,
            Detalle = detalle,
            Fecha = DateTime.UtcNow
        });
        db.Auditar(EventoPresentacion.DesdeEntrenador(tipo), EntidadAuditada.Entrenador, entrenador.Id, entrenador.NombreCompleto,
            titulo, detalle, socioId, referencia: entrenador);
    }
}
