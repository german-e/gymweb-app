using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

// Acciones invocadas desde el listado y desde la ficha.
public static class PlanAcciones
{
    // Desactivar solo cierra el plan a nuevas altas y renovaciones: las suscripciones vigentes no se tocan.
    public static async Task<string?> AlternarEstadoAsync(GymDbContext db, int id)
    {
        var p = await db.Planes.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return null;
        p.Activo = !p.Activo;
        db.Auditar(p.Activo ? TipoEventoSistema.PlanActivado : TipoEventoSistema.PlanDesactivado, EntidadAuditada.Plan, p.Id, p.Nombre);
        await db.SaveChangesAsync();
        return p.Activo
            ? $"Se activó el plan {p.Nombre}. Ya puede elegirse en nuevas altas y renovaciones."
            : $"Se desactivó el plan {p.Nombre}. Los socios que lo tienen lo conservan.";
    }
}
