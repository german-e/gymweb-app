namespace GymCore.Web.Componentes;

// Modelos de vista de los parciales reutilizables en Pages/Shared/Components.

/// <param name="Tamano">"sm" | "" | "lg" | "xl"</param>
public record AvatarVm(string? FotoUrl, string Iniciales, string Tamano = "", string? Alt = null);

/// <param name="Variante">primary | success | warning | danger (clases de stat-card en site.css)</param>
/// <param name="AccionTexto">KPI accionable: botón al pie que lleva a la pantalla de trabajo</param>
public record StatCardVm(string Titulo, string Valor, string Icono, string Variante = "primary", string? Nota = null, string? Href = null,
    string? AccionTexto = null, string? AccionHref = null);

public record EmptyStateVm(string Icono, string Titulo, string Texto, string? AccionTexto = null, string? AccionHref = null);

/// <summary>Tarjeta de alerta con semáforo (danger | warning | success) y lista corta de casos.</summary>
public record AlertCardVm(
    string Variante, string Icono, string Titulo, int Cantidad, string Texto,
    IReadOnlyList<AlertCardItem> Items, string? AccionTexto = null, string? AccionHref = null);

public record AlertCardItem(string Titulo, string Detalle, string? Href = null);

/// <summary>Historial de pagos en formato timeline, agrupado por mes. ActualId resalta un pago (detalle de pago).</summary>
public record PagosTimelineVm(
    IReadOnlyList<GymCore.Dominio.Entidades.Pago> Pagos,
    IReadOnlyDictionary<int, decimal> PagadoPorPeriodo,
    int? ActualId = null);

/// <summary>Timeline de eventos de auditoría (Auditoría, Actividad reciente, Historial de entidades).</summary>
/// <param name="MostrarEntidad">false en el historial de una entidad (ya se sabe cuál es)</param>
/// <param name="Compacto">sin detalle multilínea (Panel)</param>
public record EventosTimelineVm(
    IReadOnlyList<GymCore.Dominio.Entidades.EventoSistema> Eventos,
    bool MostrarEntidad = true,
    bool Compacto = false,
    string TextoVacio = "Todavía no hay actividad registrada.");

/// <summary>Credencial del socio (tarjeta con código de barras). Variante: success | warning | danger | muted.</summary>
public record CredencialVm(
    string NombreCompleto, string? FotoUrl, string Iniciales, string NumeroSocio, string Dni,
    string? Plan, DateTime? Vencimiento, string EstadoTexto, string Variante, string Codigo,
    bool Mini = false, string? Id = null);
