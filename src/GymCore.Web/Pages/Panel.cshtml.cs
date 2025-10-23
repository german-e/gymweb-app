using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages;

public class PanelModel : PageModel
{
    private readonly GymDbContext _db;
    public PanelModel(GymDbContext db) => _db = db;

    // KPIs
    public int SociosActivos { get; set; }
    public int SuscripcionesVencidas { get; set; }
    public decimal IngresosDelMes { get; set; }
    public int AsistenciasHoy { get; set; }

    public async Task OnGetAsync()
    {
        var ahoraLocal = DateTime.Now;
        var hoyLocal = ahoraLocal.Date;
        var inicioMesLocal = new DateTime(ahoraLocal.Year, ahoraLocal.Month, 1);

        // Convertir límites locales a UTC, porque guardamos fechas en UTC
        DateTime ToUtc(DateTime dLocal)
        {
            if (dLocal.Kind == DateTimeKind.Unspecified)
                dLocal = DateTime.SpecifyKind(dLocal, DateTimeKind.Local);
            return dLocal.ToUniversalTime();
        }

        var hoyIniUtc = ToUtc(hoyLocal);
        var hoyFinUtc = ToUtc(hoyLocal.AddDays(1));
        var mesIniUtc = ToUtc(inicioMesLocal);
        var mesFinUtc = ToUtc(inicioMesLocal.AddMonths(1));

        // Socios activos: socios con una suscripción vigente (por fechas)
        SociosActivos = await _db.SocioPlanes
            .AsNoTracking()
            .Where(sp => sp.FechaInicio <= ahoraLocal && sp.FechaFin >= ahoraLocal)
            .Select(sp => sp.SocioId)
            .Distinct()
            .CountAsync();

        // Suscripciones vencidas: por fechas (independiente del flag de estado)
        SuscripcionesVencidas = await _db.SocioPlanes
            .AsNoTracking()
            .Where(sp => sp.FechaFin < ahoraLocal)
            .CountAsync();

        // Ingresos del mes (suma de pagos en el mes en UTC)
        IngresosDelMes = await _db.Pagos
            .AsNoTracking()
            .Where(p => p.FechaPago >= mesIniUtc && p.FechaPago < mesFinUtc)
            .SumAsync(p => (decimal?)p.Importe) ?? 0m;

        // Asistencias de hoy (rango de hoy en UTC)
        AsistenciasHoy = await _db.Asistencias
            .AsNoTracking()
            .Where(a => a.FechaHoraIngreso >= hoyIniUtc && a.FechaHoraIngreso < hoyFinUtc)
            .CountAsync();
    }
}
