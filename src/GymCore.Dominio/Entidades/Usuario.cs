namespace GymCore.Dominio.Entidades;

// Persona que opera el sistema. La contraseña solo se guarda como hash (PBKDF2, ver capa web).
// Las reglas de seguridad (bloqueo, sello, cambio obligatorio) viven acá para que no se puedan saltear.
public class Usuario
{
    public int Id { get; set; }

    public string NombreCompleto { get; set; } = null!;
    public string NombreUsuario { get; private set; } = null!;   // siempre en minúsculas (único)
    public string? Email { get; set; }

    public string PasswordHash { get; private set; } = null!;

    public int RolId { get; private set; }
    public Rol Rol { get; set; } = null!;

    // Vínculo opcional con la ficha de entrenador: habilita el alcance "solo mis socios asignados"
    public int? EntrenadorId { get; set; }
    public Entrenador? Entrenador { get; set; }

    public bool Activo { get; private set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;       // UTC
    public DateTime? UltimoAcceso { get; private set; }              // UTC, sesión actual
    public DateTime? AccesoAnterior { get; private set; }            // UTC, la sesión previa (lo que ve el usuario)

    // Bloqueo temporal por intentos fallidos
    public int IntentosFallidos { get; private set; }
    public DateTime? BloqueadoHasta { get; private set; }            // UTC

    // Contraseña temporal (alta o reseteo): se obliga a cambiarla al ingresar
    public bool DebeCambiarPassword { get; private set; }

    // Cambia cuando cambian credenciales, rol o estado: invalida las sesiones abiertas
    public string SelloSeguridad { get; private set; } = NuevoSello();

    private Usuario() { }   // EF

    public Usuario(string nombreCompleto, string nombreUsuario, string passwordHash, int rolId, bool debeCambiarPassword)
    {
        NombreCompleto = nombreCompleto.Trim();
        NombreUsuario = Normalizar(nombreUsuario);
        PasswordHash = passwordHash;
        RolId = rolId;
        DebeCambiarPassword = debeCambiarPassword;
    }

    public static string Normalizar(string nombreUsuario) => nombreUsuario.Trim().ToLowerInvariant();

    public bool EstaBloqueado(DateTime ahoraUtc) => BloqueadoHasta is { } h && h > ahoraUtc;

    /// <returns>true si con este intento la cuenta quedó bloqueada</returns>
    public bool RegistrarIntentoFallido(DateTime ahoraUtc, int maximoIntentos, TimeSpan duracionBloqueo)
    {
        if (EstaBloqueado(ahoraUtc)) return false;
        IntentosFallidos++;
        if (IntentosFallidos < maximoIntentos) return false;
        BloqueadoHasta = ahoraUtc + duracionBloqueo;
        IntentosFallidos = 0;
        return true;
    }

    public void RegistrarAccesoExitoso(DateTime ahoraUtc)
    {
        AccesoAnterior = UltimoAcceso;
        UltimoAcceso = ahoraUtc;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    // Rehash transparente cuando el algoritmo se fortalece (no cambia el sello: no cierra sesiones)
    public void ActualizarHash(string passwordHash) => PasswordHash = passwordHash;

    public void CambiarPassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        DebeCambiarPassword = false;
        SelloSeguridad = NuevoSello();
    }

    // Reseteo por un administrador: contraseña temporal, cambio obligatorio y se desbloquea
    public void ResetearPassword(string passwordHashTemporal)
    {
        PasswordHash = passwordHashTemporal;
        DebeCambiarPassword = true;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        SelloSeguridad = NuevoSello();
    }

    public void CambiarRol(int rolId)
    {
        if (rolId == RolId) return;
        RolId = rolId;
        SelloSeguridad = NuevoSello();
    }

    public void Activar() { Activo = true; IntentosFallidos = 0; BloqueadoHasta = null; }

    public void Desbloquear() { IntentosFallidos = 0; BloqueadoHasta = null; }

    public void Desactivar() { Activo = false; SelloSeguridad = NuevoSello(); }

    private static string NuevoSello() => Guid.NewGuid().ToString("N");
}
