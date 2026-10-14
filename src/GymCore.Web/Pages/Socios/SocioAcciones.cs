using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

// Acciones invocadas desde el listado y desde la ficha.
public static class SocioAcciones
{
    // Baja administrativa (reversible). No toca suscripciones, pagos ni asistencias.
    public static async Task<string?> AlternarBajaAsync(GymDbContext db, int id)
    {
        var s = await db.Socios.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;
        s.FechaBaja = s.FechaBaja is null ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        return s.FechaBaja is null ? $"Se reactivó a {s.NombreCompleto}." : $"Se dio de baja a {s.NombreCompleto}.";
    }
}
