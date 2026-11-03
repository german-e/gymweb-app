namespace GymCore.Web.Componentes;

// Guarda/borra fotos de perfil e imágenes en el almacenamiento de archivos (fuera de wwwroot).
// Usado por Socios, Entrenadores, Branding y Avisos de recepción.
public static class FotoStorage
{
    public static readonly string[] Extensiones = { ".jpg", ".jpeg", ".png", ".webp" };
    public const long TamanoMaximo = 2 * 1024 * 1024;
    public const string Accept = "image/jpeg,image/png,image/webp";

    // null = válido (o sin archivo); si no, el mensaje de error. Verifica extensión, tamaño y contenido real.
    public static string? Validar(IFormFile? archivo)
    {
        if (archivo is not { Length: > 0 }) return null;
        if (!Extensiones.Contains(Path.GetExtension(archivo.FileName).ToLowerInvariant()))
            return "Formato no soportado (JPG, PNG o WEBP).";
        if (archivo.Length > TamanoMaximo)
            return "La foto no puede superar 2 MB.";
        if (!FirmaImagen.EsValida(archivo))
            return "El archivo no es una imagen válida.";
        return null;
    }

    // Devuelve la nueva URL relativa (o la actual si no hubo cambios). Borra la anterior al reemplazar/quitar.
    public static async Task<(string? Url, bool Cambio)> ProcesarAsync(AlmacenamientoArchivos archivos, string carpeta, string? actual, IFormFile? nueva, bool quitar)
    {
        if (nueva is { Length: > 0 })
        {
            var dir = archivos.Carpeta(carpeta);
            // Nombre generado por el servidor: nunca se usa el nombre que envía el usuario
            var nombre = $"{Guid.NewGuid():N}{Path.GetExtension(nueva.FileName).ToLowerInvariant()}";
            await using (var fs = File.Create(Path.Combine(dir, nombre)))
                await nueva.CopyToAsync(fs);
            Borrar(archivos, actual);
            return ($"{AlmacenamientoArchivos.PrefijoUrl}/{carpeta}/{nombre}", true);
        }
        if (quitar && actual is not null)
        {
            Borrar(archivos, actual);
            return (null, true);
        }
        return (actual, false);
    }

    public static void Borrar(AlmacenamientoArchivos archivos, string? url)
    {
        var ruta = archivos.RutaFisica(url);
        if (ruta is null) return;
        try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best-effort */ }
    }
}
