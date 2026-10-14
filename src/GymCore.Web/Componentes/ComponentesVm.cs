namespace GymCore.Web.Componentes;

// Modelos de vista de los parciales reutilizables en Pages/Shared/Components.

/// <param name="Tamano">"sm" | "" | "lg" | "xl"</param>
public record AvatarVm(string? FotoUrl, string Iniciales, string Tamano = "", string? Alt = null);

/// <param name="Variante">primary | success | warning | danger (clases de stat-card en site.css)</param>
public record StatCardVm(string Titulo, string Valor, string Icono, string Variante = "primary", string? Nota = null, string? Href = null);

public record EmptyStateVm(string Icono, string Titulo, string Texto, string? AccionTexto = null, string? AccionHref = null);

/// <summary>Credencial del socio (tarjeta con código de barras). Variante: success | warning | danger | muted.</summary>
public record CredencialVm(
    string NombreCompleto, string? FotoUrl, string Iniciales, string NumeroSocio, string Dni,
    string? Plan, DateTime? Vencimiento, string EstadoTexto, string Variante, string Codigo,
    bool Mini = false, string? Id = null);
