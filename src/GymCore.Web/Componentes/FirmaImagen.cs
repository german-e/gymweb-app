namespace GymCore.Web.Componentes;

// Verifica que el contenido real de un archivo coincida con su extensión (firma o "magic bytes").
// La extensión la elige quien sube el archivo; la firma no se puede falsificar sin romper la imagen.
// Compartido por las fotos (socios, entrenadores) y las imágenes de marca.
public static class FirmaImagen
{
    private static readonly Dictionary<string, byte[][]> Firmas = new()
    {
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".webp"] = new[] { "RIFF"u8.ToArray() },
        [".ico"] = new[] { new byte[] { 0x00, 0x00, 0x01, 0x00 } }
    };

    public static bool EsValida(IFormFile archivo)
    {
        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!Firmas.TryGetValue(ext, out var firmas)) return false;

        var cabecera = new byte[12];
        using var s = archivo.OpenReadStream();
        var leidos = s.ReadAtLeast(cabecera, cabecera.Length, throwOnEndOfStream: false);

        var coincide = firmas.Any(f => leidos >= f.Length && cabecera.AsSpan(0, f.Length).SequenceEqual(f));
        // WEBP: "RIFF" + tamaño (4 bytes) + "WEBP"
        return coincide && (ext != ".webp" || (leidos >= 12 && cabecera.AsSpan(8, 4).SequenceEqual("WEBP"u8)));
    }
}
