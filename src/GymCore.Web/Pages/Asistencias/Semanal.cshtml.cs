using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GymCore.Infraestructura.Persistencia;
using GymCore.Dominio.Enums;

namespace GymCore.Web.Pages.Asistencias;

public class SemanalModel : PageModel
{
    private readonly GymDbContext _db;
    public SemanalModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public DateTime? Start { get; set; }

    public DateTime SemanaInicio { get; set; }
    public DateTime SemanaFinExcl { get; set; }

    public List<ItemResumen> Items { get; set; } = new();

    // Resumen semanal
    public int SociosConAsistencia { get; set; }
    public int TotalAsistenciasSemana { get; set; }
    public double PromedioAsistenciasPorSocio { get; set; }

    public class ItemResumen
    {
        public int SocioId { get; set; }
        public string Dni { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? PlanNombre { get; set; }
        public int DiasUsados { get; set; }
        public int TotalAsistencias { get; set; }

        // Flags de días asistidos en la semana (Lun-Dom)
        public bool Lun { get; set; }
        public bool Mar { get; set; }
        public bool Mie { get; set; }
        public bool Jue { get; set; }
        public bool Vie { get; set; }
        public bool Sab { get; set; }
        public bool Dom { get; set; }
    }

    public async Task OnGetAsync()
    {
        var hoyLocal = DateTime.Now.Date;
        var baseDate = (Start?.Date) ?? hoyLocal;
        SemanaInicio = GetMonday(baseDate);
        SemanaFinExcl = SemanaInicio.AddDays(7);

        // Cálculo en SQL: agrupar por socio y proyectar flags por día + conteos
        var resumenQuery = _db.Asistencias
            .Where(a => a.FechaHoraIngreso >= SemanaInicio && a.FechaHoraIngreso < SemanaFinExcl)
            .GroupBy(a => a.SocioId)
            .Select(g => new
            {
                SocioId = g.Key,
                TotalAsistencias = g.Count(),
                DiasUsados = g.Select(a => a.FechaHoraIngreso.Date).Distinct().Count(),
                Lun = g.Any(a => a.FechaHoraIngreso.DayOfWeek == DayOfWeek.Monday),
                Mar = g.Any(a => a.FechaHoraIngreso.DayOfWeek == DayOfWeek.Tuesday),
                Mie = g.Any(a => a.FechaHoraIngreso.DayOfWeek == DayOfWeek.Wednesday),
                Jue = g.Any(a => a.FechaHoraIngreso.DayOfWeek == DayOfWeek.Thursday),
                Vie = g.Any(a => a.FechaHoraIngreso.DayOfWeek == DayOfWeek.Friday),
                Sab = g.Any(a => a.FechaHoraIngreso.DayOfWeek == DayOfWeek.Saturday),
                Dom = g.Any(a => a.FechaHoraIngreso.DayOfWeek == DayOfWeek.Sunday),
                PlanNombre = _db.SocioPlanes
                    .Where(sp => sp.SocioId == g.Key
                                 && sp.Estado == EstadoSuscripcion.Activo
                                 && sp.FechaInicio <= SemanaFinExcl
                                 && sp.FechaFin >= SemanaInicio)
                    .OrderByDescending(sp => sp.FechaFin)
                    .Select(sp => sp.Plan.Nombre)
                    .FirstOrDefault()
            });

        Items = await resumenQuery
            .Join(_db.Socios.AsNoTracking(),
                  x => x.SocioId,
                  s => s.Id,
                  (x, s) => new ItemResumen
                  {
                      SocioId = s.Id,
                      Dni = s.Dni,
                      Nombre = s.Apellido + ", " + s.Nombre,
                      PlanNombre = x.PlanNombre,
                      DiasUsados = x.DiasUsados,
                      TotalAsistencias = x.TotalAsistencias,
                      Lun = x.Lun, Mar = x.Mar, Mie = x.Mie, Jue = x.Jue, Vie = x.Vie, Sab = x.Sab, Dom = x.Dom
                  })
            .OrderByDescending(i => i.DiasUsados)
            .ThenBy(i => i.Nombre)
            .ToListAsync();

        // Resumen semanal
        SociosConAsistencia = Items.Count;
        TotalAsistenciasSemana = Items.Sum(i => i.TotalAsistencias);
        PromedioAsistenciasPorSocio = SociosConAsistencia > 0
            ? Math.Round((double)TotalAsistenciasSemana / SociosConAsistencia, 2)
            : 0.0;
    }

    private static DateTime GetMonday(DateTime date)
    {
        // Consider Monday as start of week
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}
