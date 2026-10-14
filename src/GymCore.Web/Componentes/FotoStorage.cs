namespace GymCore.Web.Componentes;

// Guarda/borra fotos de perfil dentro de wwwroot/uploads/<carpeta>. Usado por Socios y Entrenadores.
public static class FotoStorage
{
    public static readonly string[] Extensiones = { ".jpg", ".jpeg", ".png", ".webp" };
    public const long TamanoMaximo = 2 * 1024 * 1024;
    public const string Accept = "image/jpeg,image/png,image/webp";

    public static string? Validar(IFormFile? archivo)
    {
        if (archivo is not { Length: > 0 }) return null;
        if (!Extensiones.Contains(Path.GetExtension(archivo.FileName).ToLowerInvariant()))
            return "Formato no soportado (JPG, PNG o WEBP).";
        if (archivo.Length > TamanoMaximo)
            return "La foto no puede superar 2 MB.";
        return null;
    }

    // Devuelve la nueva ruta relativa (o la actual si no hubo cambios). Borra la anterior al reemplazar/quitar.
    public static async Task<(string? Url, bool Cambio)> ProcesarAsync(IWebHostEnvironment env, string carpeta, string? actual, IFormFile? nueva, bool quitar)
    {
        var raiz = $"uploads/{carpeta}";
        if (nueva is { Length: > 0 })
        {
            var dir = Path.Combine(env.WebRootPath, raiz);
            Directory.CreateDirectory(dir);
            var nombre = $"{Guid.NewGuid():N}{Path.GetExtension(nueva.FileName).ToLowerInvariant()}";
            await using (var fs = File.Create(Path.Combine(dir, nombre)))
                await nueva.CopyToAsync(fs);
            Borrar(env, actual);
            return ($"{raiz}/{nombre}", true);
        }
        if (quitar && actual is not null)
        {
            Borrar(env, actual);
            return (null, true);
        }
        return (actual, false);
    }

    public static void Borrar(IWebHostEnvironment env, string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("uploads/") || url.Contains("..")) return;
        var ruta = Path.Combine(env.WebRootPath, url);
        try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* best-effort */ }
    }
}
