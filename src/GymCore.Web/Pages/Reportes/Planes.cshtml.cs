using GymCore.Web.Reportes;

namespace GymCore.Web.Pages.Reportes;

public class PlanesModel : ReportesPageModel
{
    public PlanesModel(ServicioReportes reportes) : base(reportes) { }

    public ReportePlanes Reporte { get; private set; } = null!;

    // Donut (máx. 6 segmentos). El color sigue al plan, no a su posición en el ranking:
    //  - hasta 6 planes: cada plan tiene siempre el mismo color (orden estable por Id);
    //  - más de 6: los 5 con más socios conservan color por Id entre ellos y el resto se agrupa en "Otros" (gris).
    public IReadOnlyList<(string Nombre, int Socios, decimal Pct, string Slot)> Distribucion { get; private set; } = Array.Empty<(string, int, decimal, string)>();

    public async Task OnGetAsync()
    {
        await CargarPeriodoAsync();
        Reporte = await Reportes.PlanesAsync(Periodo);

        var porId = Reporte.Filas.OrderBy(f => f.Plan.Id).ToList();
        List<FilaPlan> visibles, resto;
        if (porId.Count <= 6) { visibles = porId; resto = new(); }
        else
        {
            var top = porId.OrderByDescending(f => f.SociosActivos).Take(5).Select(f => f.Plan.Id).ToHashSet();
            visibles = porId.Where(f => top.Contains(f.Plan.Id)).ToList();
            resto = porId.Where(f => !top.Contains(f.Plan.Id)).ToList();
        }
        var lista = visibles.Select((f, i) => (f.Plan.Nombre, f.SociosActivos, f.Participacion, (i + 1).ToString()))
            .Where(x => x.SociosActivos > 0).ToList();
        if (resto.Sum(f => f.SociosActivos) > 0)
            lista.Add(("Otros", resto.Sum(f => f.SociosActivos), resto.Sum(f => f.Participacion), "gris"));
        Distribucion = lista;
    }
}
