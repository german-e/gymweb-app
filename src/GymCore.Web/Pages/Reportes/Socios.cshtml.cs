using GymCore.Web.Reportes;

namespace GymCore.Web.Pages.Reportes;

public class SociosModel : ReportesPageModel
{
    public SociosModel(ServicioReportes reportes) : base(reportes) { }

    public ReporteSocios Reporte { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        await CargarPeriodoAsync();
        Reporte = await Reportes.SociosAsync(Periodo);
    }
}
