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

    private static readonly Dictionary<string, byte[][]> Firmas = new()
    {
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".webp"] = new[] { "RIFF"u8.ToArray() },
        [".ico"] = new[] { new byte[] { 0x00, 0x00, 0x01, 0x00 } }
    };

    public static string[] Extensiones(Uso u) => u == Uso.Favicon ? new[] { ".png", ".ico" } : new[] { ".png", ".jpg", ".jpeg", ".webp" };

    // null = válido (o sin archivo); si no, el mensaje de error
    public static async Task<string?> ValidarAsync(IFormFile? archivo, Uso uso)
    {
        if (archivo is not { Length: > 0 }) return null;
        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!Extensiones(uso).Contains(ext))
            return uso == Uso.Favicon ? "El favicon debe ser PNG o ICO." : "Formato no soportado: usá PNG, JPG o WEBP (SVG no está permitido).";
        if (archivo.Length > TamanoMaximo(uso))
            return $"La imagen no puede superar {TamanoMaximo(uso) / 1024} KB.";
        var cabecera = new byte[12];
        await using var s = archivo.OpenReadStream();
        var leidos = await s.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false);
        var ok = Firmas[ext].Any(f => leidos >= f.Length && cabecera.AsSpan(0, f.Length).SequenceEqual(f))
                 && (ext != ".webp" || (leidos >= 12 && cabecera.AsSpan(8, 4).SequenceEqual("WEBP"u8)));
        return ok ? null : "El archivo no es una imagen válida.";
    }
}
