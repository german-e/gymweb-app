using System.Security.Claims;
using GymCore.Aplicacion.Abstractiones;
using GymCore.Web.Seguridad;

namespace GymCore.Web.Componentes;

// Usuario autenticado de la request. Toda la app (auditoría, cobros, UI) pregunta acá quién opera y qué puede hacer.
// Sin sesión (procesos de sistema, arranque) el responsable es "Sistema" y no tiene permisos.
public interface IUsuarioActual : IOperadorAuditoria
{
    bool Autenticado { get; }
    string NombreUsuario { get; }
    string Rol { get; }
    string Iniciales { get; }
    int? EntrenadorId { get; }
    DateTime? AccesoAnterior { get; }   // UTC

    // Autorización por permiso del catálogo (Seguridad.Permisos). Nunca por nombre de rol.
    bool Puede(string permiso);
    bool PuedeAlguno(params string[] permisos);
}

public class UsuarioActual : IUsuarioActual
{
    private readonly IHttpContextAccessor _http;
    public UsuarioActual(IHttpContextAccessor http) => _http = http;

    // Se lee en cada acceso (no en el constructor): el DbContext puede crearse antes de validar la cookie
    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public bool Autenticado => Principal?.Identity?.IsAuthenticated == true;
    public int? UsuarioId => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && Autenticado ? id : null;
    public string Nombre => Autenticado ? Principal!.FindFirstValue(Reclamos.NombreCompleto) ?? NombreUsuario : GymCore.Infraestructura.Persistencia.GymDbContext.UsuarioSistema;
    public string NombreUsuario => Autenticado ? Principal!.FindFirstValue(ClaimTypes.Name) ?? "" : "";
    public string Rol => Autenticado ? Principal!.FindFirstValue(ClaimTypes.Role) ?? "" : "";
    public string Iniciales => InicialesDe(Nombre);
    public int? EntrenadorId => int.TryParse(Principal?.FindFirstValue(Reclamos.EntrenadorId), out var id) ? id : null;
    public DateTime? AccesoAnterior => DateTime.TryParse(Principal?.FindFirstValue(Reclamos.AccesoAnterior), null,
        System.Globalization.DateTimeStyles.RoundtripKind, out var f) ? f : null;

    public bool Puede(string permiso) =>
        Autenticado && (Principal!.HasClaim(Reclamos.AccesoTotal, "true") || Principal.HasClaim(Reclamos.Permiso, permiso));

    public bool PuedeAlguno(params string[] permisos) => permisos.Any(Puede);

    public static string InicialesDe(string nombre) =>
        string.Concat(nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
