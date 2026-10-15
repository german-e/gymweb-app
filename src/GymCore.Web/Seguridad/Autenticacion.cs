using System.Security.Claims;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymCore.Web.Seguridad;

// Nombres de claims propios de la sesión
public static class Reclamos
{
    public const string NombreCompleto = "gym:nombre";
    public const string Permiso = "gym:permiso";
    public const string AccesoTotal = "gym:acceso_total";
    public const string Sello = "gym:sello";
    public const string EntrenadorId = "gym:entrenador";
    public const string AccesoAnterior = "gym:acceso_anterior";
    public const string DebeCambiarPassword = "gym:debe_cambiar";
}

public enum ResultadoLogin { Exitoso, CredencialesInvalidas, Bloqueado, Inactivo }

public record IntentoLogin(ResultadoLogin Resultado, Usuario? Usuario = null, DateTime? BloqueadoHasta = null);

// Login, logout, hash de contraseñas y validación de sesión. Las cookies las emite ASP.NET Core (cifradas con Data Protection).
public class Autenticacion
{
    public const string Esquema = CookieAuthenticationDefaults.AuthenticationScheme;

    private readonly GymDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly OpcionesSeguridad _opciones;
    private static readonly PasswordHasher<Usuario> Hasher = new();   // PBKDF2 HMAC-SHA512, sal aleatoria por usuario

    // Hash de referencia para igualar el tiempo de respuesta cuando el usuario no existe (evita enumerar usuarios)
    private static readonly string HashFicticio = Hasher.HashPassword(null!, Guid.NewGuid().ToString());

    public Autenticacion(GymDbContext db, IHttpContextAccessor http, IOptions<OpcionesSeguridad> opciones)
    {
        _db = db; _http = http; _opciones = opciones.Value;
    }

    public static string Hash(string password) => Hasher.HashPassword(null!, password);

    public static PasswordVerificationResult Verificar(Usuario u, string password) =>
        Hasher.VerifyHashedPassword(u, u.PasswordHash, password);

    public Task<bool> HayUsuariosAsync() => _db.Usuarios.AnyAsync();

    public async Task<IntentoLogin> IniciarSesionAsync(string? nombreUsuario, string? password, bool recordar)
    {
        var ahora = DateTime.UtcNow;
        var nombre = Usuario.Normalizar(nombreUsuario ?? "");
        var u = nombre.Length == 0 ? null : await _db.Usuarios.Include(x => x.Rol).ThenInclude(r => r.Permisos)
            .FirstOrDefaultAsync(x => x.NombreUsuario == nombre);

        if (u is null)
        {
            Hasher.VerifyHashedPassword(null!, HashFicticio, password ?? "");
            Seguridad(TipoEventoSistema.LoginFallido, nombre, null, "Usuario inexistente");
            await _db.SaveChangesAsync();
            return new(ResultadoLogin.CredencialesInvalidas);
        }

        if (u.EstaBloqueado(ahora))
        {
            Seguridad(TipoEventoSistema.LoginFallido, u.NombreCompleto, u, "Intento con la cuenta bloqueada");
            await _db.SaveChangesAsync();
            return new(ResultadoLogin.Bloqueado, u, u.BloqueadoHasta);
        }

        var verificacion = Verificar(u, password ?? "");
        if (verificacion == PasswordVerificationResult.Failed)
        {
            var bloqueado = u.RegistrarIntentoFallido(ahora, _opciones.MaximoIntentosFallidos, TimeSpan.FromMinutes(_opciones.MinutosBloqueo));
            Seguridad(TipoEventoSistema.LoginFallido, u.NombreCompleto, u, "Contraseña incorrecta");
            if (bloqueado)
                Seguridad(TipoEventoSistema.UsuarioBloqueado, u.NombreCompleto, u,
                    $"{_opciones.MaximoIntentosFallidos} intentos fallidos · bloqueado {_opciones.MinutosBloqueo} min");
            await _db.SaveChangesAsync();
            return bloqueado ? new(ResultadoLogin.Bloqueado, u, u.BloqueadoHasta) : new(ResultadoLogin.CredencialesInvalidas);
        }

        // Inactivo: se informa solo con la contraseña correcta (no revela cuentas a terceros)
        if (!u.Activo)
        {
            Seguridad(TipoEventoSistema.LoginFallido, u.NombreCompleto, u, "Usuario inactivo");
            await _db.SaveChangesAsync();
            return new(ResultadoLogin.Inactivo, u);
        }

        if (verificacion == PasswordVerificationResult.SuccessRehashNeeded) u.ActualizarHash(Hash(password!));
        u.RegistrarAccesoExitoso(ahora);
        Seguridad(TipoEventoSistema.SesionIniciada, u.NombreCompleto, u, recordar ? "Sesión recordada en este dispositivo" : null);
        await _db.SaveChangesAsync();

        await EmitirCookieAsync(u, recordar);
        return new(ResultadoLogin.Exitoso, u);
    }

    public async Task CerrarSesionAsync(IUsuarioActual actual)
    {
        if (actual.Autenticado)
        {
            _db.Auditar(TipoEventoSistema.SesionCerrada, EntidadAuditada.Usuario, actual.UsuarioId, actual.Nombre);
            await _db.SaveChangesAsync();
        }
        await _http.HttpContext!.SignOutAsync(Esquema);
    }

    // Re-emite la cookie (ej. tras cambiar la propia contraseña, que cambia el sello) sin cerrar la sesión
    public async Task RenovarSesionAsync(int usuarioId)
    {
        var u = await _db.Usuarios.AsNoTracking().Include(x => x.Rol).ThenInclude(r => r.Permisos).FirstAsync(x => x.Id == usuarioId);
        var actual = await _http.HttpContext!.AuthenticateAsync(Esquema);
        await EmitirCookieAsync(u, actual.Properties?.IsPersistent == true);
    }

    private async Task EmitirCookieAsync(Usuario u, bool recordar)
    {
        var props = new AuthenticationProperties { IsPersistent = recordar, IssuedUtc = DateTimeOffset.UtcNow };
        if (recordar)
        {
            // Duración fija: no se renueva con la actividad
            props.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(_opciones.DiasRecordarSesion);
            props.AllowRefresh = false;
        }
        await _http.HttpContext!.SignInAsync(Esquema, CrearPrincipal(u), props);
    }

    public static ClaimsPrincipal CrearPrincipal(Usuario u)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.NombreUsuario),
            new(Reclamos.NombreCompleto, u.NombreCompleto),
            new(ClaimTypes.Role, u.Rol.Nombre),          // solo informativo (cabecera); nunca se autoriza por rol
            new(Reclamos.Sello, u.SelloSeguridad),
        };
        if (u.Rol.AccesoTotal) claims.Add(new(Reclamos.AccesoTotal, "true"));
        claims.AddRange(u.Rol.Permisos.Select(p => new Claim(Reclamos.Permiso, p.Permiso)));
        if (u.EntrenadorId is { } e) claims.Add(new(Reclamos.EntrenadorId, e.ToString()));
        if (u.AccesoAnterior is { } a) claims.Add(new(Reclamos.AccesoAnterior, DateTime.SpecifyKind(a, DateTimeKind.Utc).ToString("O")));
        if (u.DebeCambiarPassword) claims.Add(new(Reclamos.DebeCambiarPassword, "true"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Esquema, ClaimTypes.Name, ClaimTypes.Role));
    }

    // En cada request: la sesión sigue valiendo solo si el usuario existe, está activo y su sello no cambió.
    // Los permisos se recargan del rol actual, así un cambio de permisos impacta sin volver a iniciar sesión.
    public static async Task ValidarSesionAsync(CookieValidatePrincipalContext ctx)
    {
        var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sello = ctx.Principal?.FindFirstValue(Reclamos.Sello);
        var db = ctx.HttpContext.RequestServices.GetRequiredService<GymDbContext>();
        var u = int.TryParse(id, out var uid)
            ? await db.Usuarios.AsNoTracking().Include(x => x.Rol).ThenInclude(r => r.Permisos).FirstOrDefaultAsync(x => x.Id == uid)
            : null;
        if (u is null || !u.Activo || u.SelloSeguridad != sello)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(Esquema);
            return;
        }
        ctx.ReplacePrincipal(CrearPrincipal(u));
    }

    private void Seguridad(TipoEventoSistema tipo, string nombre, Usuario? u, string? resumen)
    {
        var ev = _db.Auditar(tipo, EntidadAuditada.Usuario, u?.Id, nombre, resumen,
            detalle: $"IP {_http.HttpContext?.Connection.RemoteIpAddress}");
        // Antes de autenticarse el responsable es quien intenta ingresar
        ev.Usuario = u?.NombreCompleto ?? (nombre.Length > 0 ? nombre : "Anónimo");
        ev.UsuarioId = u?.Id;
    }
}
