using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Entrenadores;

// Acciones invocadas desde el listado y desde la ficha.
public static class EntrenadorAcciones
{
    public static async Task<string?> AlternarEstadoAsync(GymDbContext db, int id)
    {
        var e = await db.Entrenadores.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return null;

        e.Activo = !e.Activo;
        e.FechaBaja = e.Activo ? null : DateTime.UtcNow;
        db.RegistrarEvento(e,
            e.Activo ? TipoEventoEntrenador.Activacion : TipoEventoEntrenador.Desactivacion,
            e.Activo ? "Entrenador reactivado" : "Entrenador desactivado");
        await db.SaveChangesAsync();
        return e.Activo ? $"Se reactivó a {e.NombreCompleto}." : $"Se desactivó a {e.NombreCompleto}.";
    }

    public static async Task<string?> EliminarAsync(GymDbContext db, IWebHostEnvironment env, int id)
    {
        var e = await db.Entrenadores.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return null;

        var foto = e.FotoUrl;
        // El historial propio se borra en cascada; la auditoría del sistema conserva el registro
        db.Auditar(TipoEventoSistema.EntrenadorEliminado, EntidadAuditada.Entrenador, e.Id, e.NombreCompleto, $"DNI {e.Dni}");
        db.Entrenadores.Remove(e); // asignaciones e historial se eliminan en cascada
        await db.SaveChangesAsync();

        FotoStorage.Borrar(env, foto);
        return $"Se eliminó a {e.NombreCompleto}.";
    }
}
