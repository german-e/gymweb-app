using GymCore.Web.Reportes;
using Microsoft.AspNetCore.Mvc;

namespace GymCore.Web.Pages.Reportes;

public class RiesgoModel : ReportesPageModel
{
    public RiesgoModel(ServicioReportes reportes) : base(reportes) { }

    public ReporteRiesgo Reporte { get; private set; } = null!;

    // Filtro inicial (deep-link desde el resumen): ?nivel=alto|medio|bajo
    [BindProperty(SupportsGet = true)] public string? Nivel { get; set; }

    public async Task OnGetAsync() => Reporte = await Reportes.RiesgoAsync();
}
