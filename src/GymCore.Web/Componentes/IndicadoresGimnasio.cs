using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Pages.Socios;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Componentes;

// Indicadores compartidos por Panel, Pagos y el sidebar: una sola definición para que todos muestren los mismos números.
// Las reglas de membresía son las de SociosConsultas (las mismas del listado de Socios).
public static class IndicadoresGimnasio
{
    public record Cobranza(decimal Hoy, int PagosHoy, decimal Mes, int PagosMes, decimal MesAnteriorMismoPeriodo)
    {
        // Variación vs. el mismo tramo del mes anterior (null si no hay base de comparación)
        public int? VariacionMes => MesAnteriorMismoPeriodo > 0 ? (int)Math.Round((Mes - MesAnteriorMismoPeriodo) * 100 / MesAnteriorMismoPeriodo) : null;
    }

    public record Membresias(
        int Activos, int Morosos, decimal SaldoPendiente, int AlDia,
        List<ResumenSocio> Vencidos, List<ResumenSocio> PorVencer,
        int SinEntrenador, int NuevosMes);

    /// <summary>Todo lo que muestran el layout (sidebar, centro de alertas) y las páginas de indicadores.</summary>
    public record Resumen(Cobranza Cobranza, Membresias Membresias, int PlanesInactivos)
    {
        // Alertas que requieren acción (número de la campana)
        public int AlertasUrgentes => Membresias.Vencidos.Count + Membresias.PorVencer.Count;
    }

    // Memoriza los indicadores durante el request: la página (Panel, Pagos) y el layout comparten el mismo cálculo.
    public static async Task<Resumen> DelRequestAsync(HttpContext ctx, GymDbContext db)
    {
        const string clave = "GymCore.Indicadores";
        if (ctx.Items[clave] is Resumen memo) return memo;
        var r = new Resumen(await CobranzaAsync(db), await MembresiasAsync(db),
            await db.Planes.AsNoTracking().CountAsync(p => !p.Activo));
        ctx.Items[clave] = r;
        return r;
    }

    // Mismas definiciones de período que los reportes (GymCore.Web.Reportes.Periodo), por días locales:
    // el Panel, el sidebar y el Resumen ejecutivo muestran la misma recaudación y la misma variación.
    public static async Task<Cobranza> CobranzaAsync(GymDbContext db)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var (mesIni, _) = Reportes.Periodo.MesActualHastaHoy(hoy);
        var (antIni, antFin) = Reportes.Periodo.MismoTramoMesAnterior(hoy);

        var desdeUtc = Reportes.Periodo.InicioUtc(antIni);
        // Suma en memoria: SQLite no agrega decimal del lado del servidor
        var pagos = (await db.Pagos.AsNoTracking()
                .Where(p => p.FechaPago >= desdeUtc)
                .Select(p => new { p.FechaPago, p.Importe })
                .ToListAsync())
            .Select(p => (Fecha: Reportes.Periodo.Local(p.FechaPago), p.Importe))
            .ToList();

        var delMes = pagos.Where(p => p.Fecha >= mesIni && p.Fecha <= hoy).ToList();
        var deHoy = delMes.Where(p => p.Fecha == hoy).ToList();
        var mesAnterior = pagos.Where(p => p.Fecha >= antIni && p.Fecha <= antFin).Sum(p => p.Importe);

        return new Cobranza(deHoy.Sum(p => p.Importe), deHoy.Count, delMes.Sum(p => p.Importe), delMes.Count, mesAnterior);
    }

    public static async Task<Membresias> MembresiasAsync(GymDbContext db)
    {
        var resumen = (await SociosConsultas.ResumenAsync(db)).Values.Where(r => !r.Socio.DadoDeBaja).ToList();
        var morosos = resumen.Where(r => r.Moroso).ToList();
        return new Membresias(
            Activos: resumen.Count(r => r.Vigente is not null),
            Morosos: morosos.Count,
            SaldoPendiente: morosos.Sum(r => r.Saldo),
            AlDia: resumen.Count(r => r.Vigente is not null && !r.Moroso),
            Vencidos: resumen.Where(r => r.Estado == EstadoMembresia.Vencida).OrderByDescending(r => r.Vencimiento).ToList(),
            PorVencer: resumen.Where(r => r.Estado == EstadoMembresia.PorVencer).OrderBy(r => r.Vencimiento).ToList(),
            // Mismas definiciones que los chips "Sin entrenador" y "Nuevos del mes" del listado de Socios
            SinEntrenador: resumen.Count(r => r.Entrenador is null),
            NuevosMes: resumen.Count(r => r.Socio.FechaAlta.ToLocalTime() >= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)));
    }

    // Plan con más suscripciones históricas (misma definición que el listado de Planes)
    public static async Task<(Plan Plan, int Altas)?> PlanMasContratadoAsync(GymDbContext db)
    {
        var top = await db.SocioPlanes.AsNoTracking()
            .GroupBy(sp => sp.PlanId)
            .Select(g => new { PlanId = g.Key, Altas = g.Count() })
            .OrderByDescending(x => x.Altas)
            .FirstOrDefaultAsync();
        if (top is null) return null;
        var plan = await db.Planes.AsNoTracking().FirstAsync(p => p.Id == top.PlanId);
        return (plan, top.Altas);
    }

    // Recaudación de los últimos n meses (incluye el actual), del más antiguo al más reciente
    public static async Task<List<(DateTime Mes, decimal Total)>> RecaudacionMensualAsync(GymDbContext db, int meses = 6)
    {
        var hoy = DateTime.Today;
        var desde = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-(meses - 1));
        var desdeUtc = desde.ToUniversalTime();
        var pagos = await db.Pagos.AsNoTracking()
            .Where(p => p.FechaPago >= desdeUtc)
            .Select(p => new { p.FechaPago, p.Importe })
            .ToListAsync();
        var porMes = pagos.GroupBy(p => { var l = p.FechaPago.ToLocalTime(); return new DateTime(l.Year, l.Month, 1); })
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Importe));
        return Enumerable.Range(0, meses).Select(i => desde.AddMonths(i)).Select(m => (m, porMes.GetValueOrDefault(m))).ToList();
    }
}
