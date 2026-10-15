using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Reportes;

// Única puerta de entrada de datos para inteligencia de negocio.
//  - Caché: decorar esta interfaz (ej. IMemoryCache por gimnasio con TTL de minutos) sin tocar las métricas.
//  - Multi-sucursal / multi-gimnasio: el filtro de alcance (GimnasioId/SucursalId) se aplica acá y solo acá.
public interface IFuenteHechos
{
    Task<HechosNegocio> ObtenerAsync(CancellationToken ct = default);
}

public sealed class FuenteHechosDb : IFuenteHechos
{
    // Ventanas: 25 meses de pagos (comparación interanual mes a mes) y 13 de asistencias (tendencia de 12 meses)
    public const int MesesPagos = 25;
    public const int MesesAsistencias = 13;

    private readonly GymDbContext _db;
    private Task<HechosNegocio>? _memo;   // una carga por request (servicio scoped)
    public FuenteHechosDb(GymDbContext db) => _db = db;

    public Task<HechosNegocio> ObtenerAsync(CancellationToken ct = default) => _memo ??= CargarAsync(ct);

    private async Task<HechosNegocio> CargarAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var desdePagos = Periodo.InicioUtc(Periodo.InicioMes(hoy).AddMonths(-(MesesPagos - 1)));
        var desdeAsist = Periodo.InicioUtc(Periodo.InicioMes(hoy).AddMonths(-(MesesAsistencias - 1)));

        // Solo las columnas necesarias y sin tracking. Las sumas se hacen en memoria (SQLite no agrega decimal).
        var socios = (await _db.Socios.AsNoTracking()
                .Select(s => new { s.Id, s.Nombre, s.Apellido, s.Dni, s.Telefono, s.FechaAlta, s.FechaBaja })
                .ToListAsync(ct))
            .Select(s => new SocioH(s.Id, s.Nombre, s.Apellido, s.Dni, s.Telefono, Periodo.Local(s.FechaAlta),
                s.FechaBaja is { } b ? Periodo.Local(b) : null))
            .ToList();

        // Vigencia por fechas: las canceladas o pausadas no cuentan como cobertura
        var subs = (await _db.SocioPlanes.AsNoTracking()
                .Where(sp => sp.Estado != EstadoSuscripcion.Cancelado && sp.Estado != EstadoSuscripcion.Pausado)
                .Select(sp => new { sp.Id, sp.SocioId, sp.PlanId, sp.FechaInicio, sp.FechaFin, sp.Plan.Precio })
                .ToListAsync(ct))
            .Select(sp => new SuscripcionH(sp.Id, sp.SocioId, sp.PlanId, DateOnly.FromDateTime(sp.FechaInicio), DateOnly.FromDateTime(sp.FechaFin), sp.Precio))
            .ToList();

        // Filtro global: excluye pagos anulados
        var pagos = (await _db.Pagos.AsNoTracking().Where(p => p.FechaPago >= desdePagos)
                .Select(p => new { p.SocioId, p.SocioPlanId, p.FechaPago, p.Importe })
                .ToListAsync(ct))
            .Select(p => new PagoH(p.SocioId, p.SocioPlanId, Periodo.Local(p.FechaPago), p.Importe))
            .ToList();

        var asistencias = (await _db.Asistencias.AsNoTracking().Where(a => a.FechaHoraIngreso >= desdeAsist)
                .Select(a => new { a.SocioId, a.FechaHoraIngreso })
                .ToListAsync(ct))
            .Select(a => new AsistenciaH(a.SocioId, Periodo.Local(a.FechaHoraIngreso)))
            .ToList();

        // Última asistencia histórica (índice SocioId + FechaHoraIngreso)
        var ultima = (await _db.Asistencias.AsNoTracking()
                .GroupBy(a => a.SocioId)
                .Select(g => new { g.Key, Ultima = g.Max(a => a.FechaHoraIngreso) })
                .ToListAsync(ct))
            .ToDictionary(x => x.Key, x => Periodo.Local(x.Ultima));

        var asignaciones = (await _db.EntrenadorSocios.AsNoTracking()
                .Select(a => new { a.EntrenadorId, a.SocioId, a.FechaAsignacion, a.FechaFin, a.Activa })
                .ToListAsync(ct))
            .Select(a => new AsignacionH(a.EntrenadorId, a.SocioId, Periodo.Local(a.FechaAsignacion),
                a.FechaFin is { } f ? Periodo.Local(f) : null, a.Activa))
            .ToList();

        var planes = await _db.Planes.AsNoTracking()
            .Select(p => new PlanH(p.Id, p.Nombre, p.Precio, p.Activo)).ToListAsync(ct);
        var entrenadores = await _db.Entrenadores.AsNoTracking()
            .Select(e => new EntrenadorH(e.Id, e.Nombre + " " + e.Apellido, e.Activo)).ToListAsync(ct);

        return new HechosNegocio
        {
            Hoy = hoy, Socios = socios, Suscripciones = subs, Pagos = pagos, Asistencias = asistencias,
            UltimaAsistencia = ultima, Asignaciones = asignaciones, Planes = planes, Entrenadores = entrenadores
        };
    }
}
