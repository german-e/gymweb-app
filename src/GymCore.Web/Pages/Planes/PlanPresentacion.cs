using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;

namespace GymCore.Web.Pages.Planes;

// Textos, íconos y colores del módulo. Centralizado para que listado, ficha y formularios hablen igual.
public static class PlanPresentacion
{
    public static string Texto(this TipoPlan t) => t switch
    {
        TipoPlan.MensualDiasPorSemana => "Mensual por días",
        TipoPlan.MensualIlimitado => "Mensual ilimitado",
        TipoPlan.PaseDiario => "Pase diario",
        _ => t.ToString()
    };

    public static string Icono(this TipoPlan t) => t switch
    {
        TipoPlan.MensualDiasPorSemana => "bi-calendar-week",
        TipoPlan.MensualIlimitado => "bi-infinity",
        TipoPlan.PaseDiario => "bi-ticket-perforated",
        _ => "bi-box-seam"
    };

    // Variantes de module-icon (site.css)
    public static string Variante(this TipoPlan t) => t switch
    {
        TipoPlan.MensualDiasPorSemana => "info",
        TipoPlan.MensualIlimitado => "primary",
        TipoPlan.PaseDiario => "warning",
        _ => "muted"
    };

    public static string Duracion(int dias) => dias == 1 ? "1 día" : $"{dias} días";

    // Estado comercial del plan (Plan.Activo)
    public static (string Texto, string Variante) Estado(Plan p) =>
        p.Activo ? ("Activo", "success") : ("Inactivo", "muted");

    // Texto del modal de activar / desactivar
    public static string TextoCambioEstado(Plan p, int sociosVigentes) =>
        !p.Activo ? "Volverá a estar disponible para nuevas altas y renovaciones."
            : "No podrá elegirse en nuevas altas ni renovaciones. " + Conservan(sociosVigentes);

    public static string Conservan(int sociosVigentes) => sociosVigentes switch
    {
        0 => "No tiene socios vigentes.",
        1 => "El socio que lo tiene vigente lo conserva hasta su vencimiento.",
        _ => $"Los {sociosVigentes} socios que lo tienen vigente lo conservan hasta su vencimiento."
    };

    public static string Creado(Plan p) => p.FechaAlta is null ? "Fecha de creación sin registro" : $"Creado el {FechaAlta(p)}";

    public static string FechaAlta(Plan p) => p.FechaAlta?.ToLocalTime().ToString("dd/MM/yyyy") ?? "Sin registro";

    // "1 a 3 m" (mismo formato que la antigüedad de entrenadores)
    public static string Antiguedad(Plan p) =>
        p.FechaAlta is { } f ? GymCore.Web.Pages.Entrenadores.EntrenadorPresentacion.Antiguedad(f) : "—";

    // Estado de una suscripción al plan (vigente = Activo y FechaFin >= hoy)
    public static (string Texto, string Variante) Estado(SocioPlan sp, int diasAviso = 7)
    {
        var hoy = DateTime.Today;
        return sp.Estado switch
        {
            EstadoSuscripcion.Activo when sp.FechaFin >= hoy =>
                sp.FechaFin <= hoy.AddDays(diasAviso) ? ("Por vencer", "warning") : ("Vigente", "success"),
            EstadoSuscripcion.Pausado => ("Pausada", "info"),
            EstadoSuscripcion.Cancelado => ("Cancelada", "muted"),
            _ => ("Vencida", "danger")
        };
    }

    // Reglas del plan expresadas como beneficios para la ficha y la vista previa del formulario
    public static IEnumerable<(string Icono, string Texto, bool Incluido)> Beneficios(Plan p)
    {
        yield return p.Tipo == TipoPlan.PaseDiario
            ? ("bi-door-open", "Acceso por un día", true)
            : p.MaxDiasPorSemana is int n
                ? ("bi-calendar-week", $"Hasta {n} {(n == 1 ? "día" : "días")} por semana", true)
                : ("bi-infinity", "Acceso libre todos los días", true);
        yield return ("bi-arrow-repeat", $"Ciclo de {Duracion(p.DuracionDias)}", true);
        yield return ("bi-calendar-event", "Acceso los sábados", p.PermiteAccesoSabado);
        yield return ("bi-calendar-heart", "Acceso los domingos", p.PermiteAccesoDomingo);
    }
}
