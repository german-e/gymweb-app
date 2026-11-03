namespace GymCore.Web.Componentes;

// Carpeta donde se guardan los archivos subidos (fotos, imágenes de marca), FUERA de wwwroot.
// Lo que está en wwwroot se publica sin control de acceso; desde acá, Program.cs decide qué es público
// (solo las imágenes de marca) y qué exige sesión (fotos de socios y entrenadores).
//
// Las URLs guardadas en la base no cambian ("uploads/socios/abc.jpg"): se siguen sirviendo en /uploads/...
// Ruta configurable con "Almacenamiento:Uploads" (en Docker: /app/uploads, montado como volumen).
public sealed class AlmacenamientoArchivos
{
    public const string PrefijoUrl = "uploads";

    public string Raiz { get; }

    public AlmacenamientoArchivos(IConfiguration config, IWebHostEnvironment env)
    {
        var ruta = config["Almacenamiento:Uploads"];
        Raiz = Path.GetFullPath(string.IsNullOrWhiteSpace(ruta)
            ? Path.Combine(env.ContentRootPath, "data", PrefijoUrl)
            : Path.IsPathRooted(ruta) ? ruta : Path.Combine(env.ContentRootPath, ruta));
        Directory.CreateDirectory(Raiz);
    }

    // Carpeta física de una categoría ("socios", "entrenadores", "branding"); la crea si falta
    public string Carpeta(string carpeta)
    {
        var dir = Path.GetFullPath(Path.Combine(Raiz, carpeta));
        if (!DentroDeRaiz(dir)) throw new ArgumentException("Carpeta inválida.", nameof(carpeta));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // Traduce una URL guardada ("uploads/socios/abc.jpg") a su ruta física. null si no es una URL de uploads válida.
    public string? RutaFisica(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith(PrefijoUrl + "/") || url.Contains("..")) return null;
        var ruta = Path.GetFullPath(Path.Combine(Raiz, url[(PrefijoUrl.Length + 1)..]));
        return DentroDeRaiz(ruta) ? ruta : null;
    }

    // Migración única: mueve los archivos de la ubicación anterior (wwwroot/uploads) a la nueva.
    // No pisa archivos existentes. Devuelve cuántos movió.
    public int MigrarDesde(string carpetaAnterior)
    {
        if (!Directory.Exists(carpetaAnterior)) return 0;
        var movidos = 0;
        foreach (var origen in Directory.EnumerateFiles(carpetaAnterior, "*", SearchOption.AllDirectories))
        {
            var destino = Path.GetFullPath(Path.Combine(Raiz, Path.GetRelativePath(carpetaAnterior, origen)));
            if (!DentroDeRaiz(destino) || File.Exists(destino)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            File.Copy(origen, destino);
            File.Delete(origen);
            movidos++;
        }
        return movidos;
    }

    private bool DentroDeRaiz(string ruta) =>
        ruta.Equals(Raiz, StringComparison.Ordinal) || ruta.StartsWith(Raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
