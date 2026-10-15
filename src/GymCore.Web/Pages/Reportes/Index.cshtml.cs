using GymCore.Web.Reportes;

namespace GymCore.Web.Pages.Reportes;

public class IndexModel : ReportesPageModel
{
    public IndexModel(ServicioReportes reportes) : base(reportes) { }

    public ResumenEjecutivo Resumen { get; private set; } = null!;

    public async Task OnGetAsync() => Resumen = await Reportes.ResumenAsync();
}
