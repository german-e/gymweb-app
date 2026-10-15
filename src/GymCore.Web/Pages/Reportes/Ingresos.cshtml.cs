using GymCore.Web.Reportes;

namespace GymCore.Web.Pages.Reportes;

public class IngresosModel : ReportesPageModel
{
    public IngresosModel(ServicioReportes reportes) : base(reportes) { }

    public ReporteIngresos Reporte { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        await CargarPeriodoAsync();
        Reporte = await Reportes.IngresosAsync(Periodo);
    }
}
