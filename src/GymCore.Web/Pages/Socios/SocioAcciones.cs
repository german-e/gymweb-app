using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
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
        db.Auditar(s.FechaBaja is null ? TipoEventoSistema.SocioReactivado : TipoEventoSistema.SocioBaja,
            EntidadAuditada.Socio, s.Id, $"{s.Apellido}, {s.Nombre}", $"N° {SocioPresentacion.Numero(s.NumeroVisible)}", socioId: s.Id);
        await db.SaveChangesAsync();
        return s.FechaBaja is null ? $"Se reactivó a {s.NombreCompleto}." : $"Se dio de baja a {s.NombreCompleto}.";
    }
}
