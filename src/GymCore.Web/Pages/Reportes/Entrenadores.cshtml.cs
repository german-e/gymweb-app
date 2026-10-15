using GymCore.Web.Reportes;

namespace GymCore.Web.Pages.Reportes;

public class EntrenadoresModel : ReportesPageModel
{
    public EntrenadoresModel(ServicioReportes reportes) : base(reportes) { }

    public ReporteEntrenadores Reporte { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        await CargarPeriodoAsync();
        Reporte = await Reportes.EntrenadoresAsync(Periodo);
    }
}
