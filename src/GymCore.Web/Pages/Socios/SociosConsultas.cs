using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

public enum EstadoMembresia { Vigente, PorVencer, Pausada, Vencida, SinPlan, Baja }

public record ResumenSocio(
    Socio Socio,
    SocioPlan? Vigente,          // suscripción vigente (o null)
    SocioPlan? Ultima,           // última suscripción conocida (vigente o no)
    EstadoMembresia Estado,
    decimal Saldo,               // faltante de la suscripción vigente (misma regla que Pagos/Pendientes)
    DateTime? UltimaAsistencia,  // UTC
    Entrenador? Entrenador)      // entrenador asignado actual
{
    public bool Moroso => Saldo > 0m;
    public string? PlanNombre => (Vigente ?? Ultima)?.Plan?.Nombre;
    public DateTime? Vencimiento => (Vigente ?? Ultima)?.FechaFin;
    public bool CredencialActiva => !Socio.DadoDeBaja && Vigente is not null;
}

// Lecturas agregadas del estado de los socios. Reproduce las reglas existentes del sistema:
//  - vigente: Estado = Activo y FechaFin >= hoy (listado de Socios / Suscripciones)
//  - moroso: vigente con Precio del plan - pagos asociados > 0 (Pagos/Pendientes)
public static class SociosConsultas
{
    public const int DiasAvisoVencimiento = 7;

    public static async Task<Dictionary<int, ResumenSocio>> ResumenAsync(GymDbContext db, ICollection<int>? socioIds = null)
    {
        var hoy = DateTime.Now.Date;

        var qSocios = db.Socios.AsNoTracking();
        var qPlanes = db.SocioPlanes.AsNoTracking().Include(sp => sp.Plan).AsQueryable();
        var qAsist = db.Asistencias.AsNoTracking();
        var qAsign = db.EntrenadorSocios.AsNoTracking().Include(a => a.Entrenador).Where(a => a.Activa);
        if (socioIds is not null)
        {
            qSocios = qSocios.Where(s => socioIds.Contains(s.Id));
            qPlanes = qPlanes.Where(sp => socioIds.Contains(sp.SocioId));
            qAsist = qAsist.Where(a => socioIds.Contains(a.SocioId));
            qAsign = qAsign.Where(a => socioIds.Contains(a.SocioId));
        }

        var socios = await qSocios.ToListAsync();
        var planes = (await qPlanes.ToListAsync()).ToLookup(sp => sp.SocioId);
        var ultimas = await qAsist.GroupBy(a => a.SocioId)
            .Select(g => new { g.Key, Ultima = g.Max(a => a.FechaHoraIngreso) })
            .ToDictionaryAsync(x => x.Key, x => x.Ultima);
        var entrenadores = (await qAsign.ToListAsync())
            .GroupBy(a => a.SocioId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FechaAsignacion).First().Entrenador);

        // Pagado por suscripción vigente
        var vigentes = new Dictionary<int, SocioPlan>();
        foreach (var s in socios)
        {
            var v = planes[s.Id].Where(sp => sp.Estado == EstadoSuscripcion.Activo && sp.FechaFin >= hoy)
                .OrderByDescending(sp => sp.FechaFin).FirstOrDefault();
            if (v is not null) vigentes[s.Id] = v;
        }
        var idsVigentes = vigentes.Values.Select(v => v.Id).ToList();
        // Suma en memoria: SQLite no agrega decimal del lado del servidor
        var pagado = (await db.Pagos.AsNoTracking()
                .Where(p => p.SocioPlanId != null && idsVigentes.Contains(p.SocioPlanId.Value))
                .Select(p => new { Id = p.SocioPlanId!.Value, p.Importe })
                .ToListAsync())
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Importe));

        var resultado = new Dictionary<int, ResumenSocio>();
        foreach (var s in socios)
        {
            vigentes.TryGetValue(s.Id, out var vig);
            var ultima = planes[s.Id].OrderByDescending(sp => sp.FechaFin).FirstOrDefault();
            var saldo = vig is null ? 0m : Math.Max(0m, vig.Plan.Precio - pagado.GetValueOrDefault(vig.Id));

            var estado = s.DadoDeBaja ? EstadoMembresia.Baja
                : vig is not null ? (vig.FechaFin <= hoy.AddDays(DiasAvisoVencimiento) ? EstadoMembresia.PorVencer : EstadoMembresia.Vigente)
                : ultima is null ? EstadoMembresia.SinPlan
                : ultima.Estado == EstadoSuscripcion.Pausado ? EstadoMembresia.Pausada
                : EstadoMembresia.Vencida;

            resultado[s.Id] = new ResumenSocio(s, vig, ultima, estado, saldo,
                ultimas.TryGetValue(s.Id, out var u) ? u : null,
                entrenadores.GetValueOrDefault(s.Id));
        }
        return resultado;
    }

    // Socios con asignación activa al entrenador (alcance del permiso SOCIOS_VER_ASIGNADOS)
    public static async Task<List<int>> AsignadosAsync(GymDbContext db, int? entrenadorId) =>
        entrenadorId is null ? new List<int>()
            : await db.EntrenadorSocios.AsNoTracking().Where(a => a.Activa && a.EntrenadorId == entrenadorId)
                .Select(a => a.SocioId).Distinct().ToListAsync();

    // Quien tiene SOCIOS_VER ve a todos; con SOCIOS_VER_ASIGNADOS, solo a los asignados a su ficha de entrenador
    public static async Task<bool> PuedeVerSocioAsync(GymDbContext db, GymCore.Web.Componentes.IUsuarioActual usuario, int socioId)
    {
        if (usuario.Puede(GymCore.Web.Seguridad.Permisos.SociosVer)) return true;
        if (!usuario.Puede(GymCore.Web.Seguridad.Permisos.SociosVerAsignados) || usuario.EntrenadorId is null) return false;
        return await db.EntrenadorSocios.AnyAsync(a => a.Activa && a.SocioId == socioId && a.EntrenadorId == usuario.EntrenadorId);
    }

    public static async Task<int> SiguienteNumeroAsync(GymDbContext db)
    {
        var max = await db.Socios.MaxAsync(s => (int?)(s.NumeroSocio ?? s.Id)) ?? 0;
        return max + 1;
    }
}
