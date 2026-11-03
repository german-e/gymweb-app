using GymCore.Web.Componentes;

namespace GymCore.Web.Marca;

// Validación y guardado de imágenes de marca. Se verifica el contenido real (firma del archivo), no solo la
// extensión, y no se aceptan SVG (pueden contener scripts que se ejecutarían en el dominio de la aplicación).
public static class ImagenesMarca
{
    public enum Uso { Logo, Isotipo, Favicon, Bienvenida, Aviso }

    public const string Carpeta = "branding";
    public const string AcceptImagen = "image/png,image/jpeg,image/webp";
    public const string AcceptFavicon = "image/png,image/x-icon,image/vnd.microsoft.icon";

    public static long TamanoMaximo(Uso u) => u switch { Uso.Favicon => 256 * 1024, Uso.Bienvenida or Uso.Aviso => 3 * 1024 * 1024, _ => 1024 * 1024 };

    public static string[] Extensiones(Uso u) => u == Uso.Favicon ? new[] { ".png", ".ico" } : new[] { ".png", ".jpg", ".jpeg", ".webp" };

    // null = válido (o sin archivo); si no, el mensaje de error. Se mantiene la firma asíncrona de los llamadores.
    public static Task<string?> ValidarAsync(IFormFile? archivo, Uso uso) => Task.FromResult(Validar(archivo, uso));

    public static string? Validar(IFormFile? archivo, Uso uso)
    {
        if (archivo is not { Length: > 0 }) return null;
        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!Extensiones(uso).Contains(ext))
            return uso == Uso.Favicon ? "El favicon debe ser PNG o ICO." : "Formato no soportado: usá PNG, JPG o WEBP (SVG no está permitido).";
        if (archivo.Length > TamanoMaximo(uso))
            return $"La imagen no puede superar {TamanoMaximo(uso) / 1024} KB.";
        // Firma real del archivo (compartida con las fotos de socios y entrenadores)
        return FirmaImagen.EsValida(archivo) ? null : "El archivo no es una imagen válida.";
    }
}
