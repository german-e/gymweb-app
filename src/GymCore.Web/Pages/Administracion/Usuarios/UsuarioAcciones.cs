using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Administracion.Usuarios;

public record ResultadoAccion(bool Ok, string Mensaje, string? PasswordTemporal = null);

// Acciones sobre usuarios invocadas desde el listado y la ficha. Todas quedan auditadas.
public static class UsuarioAcciones
{
    // Regla anti-bloqueo: siempre debe quedar al menos un usuario activo que pueda administrar usuarios.
    // Se evalúa sobre el estado hipotético (antes de aplicar el cambio).
    public static async Task<bool> QuedaAdministradorAsync(GymDbContext db,
        int? usuarioId = null, bool? activoDespues = null, int? rolDespues = null,
        int? rolEditadoId = null, bool? rolEditadoAdministra = null)
    {
        var roles = await db.Roles.AsNoTracking().Include(r => r.Permisos).ToListAsync();
        bool Administra(int rolId) => rolId == rolEditadoId ? rolEditadoAdministra!.Value : roles.First(r => r.Id == rolId).Tiene(Permisos.UsuariosAdministrar);
        var usuarios = await db.Usuarios.AsNoTracking().Select(u => new { u.Id, u.Activo, u.RolId }).ToListAsync();
        return usuarios.Any(u =>
            (u.Id == usuarioId ? activoDespues ?? u.Activo : u.Activo) &&
            Administra(u.Id == usuarioId ? rolDespues ?? u.RolId : u.RolId));
    }

    public static async Task<ResultadoAccion> AlternarEstadoAsync(GymDbContext db, IUsuarioActual actual, int id)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return new(false, "El usuario no existe.");
        if (u.Activo && u.Id == actual.UsuarioId) return new(false, "No podés desactivar tu propio usuario.");
        if (u.Activo && !await QuedaAdministradorAsync(db, u.Id, activoDespues: false))
            return new(false, $"No se puede desactivar a {u.NombreCompleto}: es el último usuario activo que administra usuarios.");

        if (u.Activo) u.Desactivar(); else u.Activar();
        db.Auditar(u.Activo ? TipoEventoSistema.UsuarioActivado : TipoEventoSistema.UsuarioDesactivado, EntidadAuditada.Usuario, u.Id, u.NombreCompleto,
            u.Activo ? "Puede volver a iniciar sesión" : "Sus sesiones abiertas se cerraron");
        await db.SaveChangesAsync();
        return new(true, u.Activo ? $"Se activó a {u.NombreCompleto}." : $"Se desactivó a {u.NombreCompleto}. Sus sesiones abiertas se cerraron.");
    }

    // Genera una contraseña temporal (se muestra una sola vez) y obliga a cambiarla en el próximo ingreso
    public static async Task<ResultadoAccion> ResetearPasswordAsync(GymDbContext db, int id)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return new(false, "El usuario no existe.");
        var temporal = PoliticaPassword.GenerarTemporal();
        u.ResetearPassword(Autenticacion.Hash(temporal));
        db.Auditar(TipoEventoSistema.PasswordReseteada, EntidadAuditada.Usuario, u.Id, u.NombreCompleto,
            "Contraseña temporal emitida · deberá cambiarla al ingresar");
        await db.SaveChangesAsync();
        return new(true, $"Contraseña de {u.NombreCompleto} restablecida. Compartila por un canal seguro: no se vuelve a mostrar.", temporal);
    }

    public static async Task<ResultadoAccion> DesbloquearAsync(GymDbContext db, int id)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return new(false, "El usuario no existe.");
        u.Desbloquear();
        db.Auditar(TipoEventoSistema.UsuarioActivado, EntidadAuditada.Usuario, u.Id, u.NombreCompleto, "Desbloqueo manual");
        await db.SaveChangesAsync();
        return new(true, $"Se desbloqueó a {u.NombreCompleto}.");
    }

    public static (string Texto, string Variante) Estado(Usuario u) =>
        !u.Activo ? ("Inactivo", "muted")
        : u.EstaBloqueado(DateTime.UtcNow) ? ("Bloqueado", "danger")
        : u.DebeCambiarPassword ? ("Contraseña temporal", "warning")
        : ("Activo", "success");
}
