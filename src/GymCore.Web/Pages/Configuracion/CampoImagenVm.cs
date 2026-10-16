namespace GymCore.Web.Pages.Configuracion;

/// <param name="Forma">"ancho" (logo, imagen de bienvenida) | "cuadrado" (isotipo, favicon)</param>
public record CampoImagenVm(string Nombre, string NombreQuitar, string Titulo, string Ayuda, string? UrlActual, string Accept,
    string Icono = "bi-image", string Forma = "ancho", string? Error = null);
