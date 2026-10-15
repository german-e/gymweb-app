using GymCore.Web.Comercial;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Periodo = GymCore.Web.Reportes.Periodo;

namespace GymCore.Web.Pages.Ventas;

[RequierePermiso(Permisos.ReportesComercialesVer)]
public class ReportesModel : PageModel
{
    private readonly ReportesComerciales _reportes;
    public ReportesModel(ReportesComerciales reportes) => _reportes = reportes;

    [BindProperty(SupportsGet = true, Name = "p")] public string? ClavePeriodo { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta { get; set; }

    public Periodo Periodo { get; private set; } = null!;
    public ReportesComerciales.Reporte Reporte { get; private set; } = null!;

    // Donut por categoría: color estable por categoría; más de 6 → las que no tienen color se agrupan en "Otras" (gris)
    public IReadOnlyList<(string Nombre, decimal Importe, decimal Pct, string Slot)> Distribucion { get; private set; } = Array.Empty<(string, decimal, decimal, string)>();

    public async Task OnGetAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        Periodo = Periodo.Crear(ClavePeriodo, hoy, Desde, Hasta);
        Reporte = await _reportes.ReporteAsync(Periodo, hoy);

        var total = Reporte.Categorias.Sum(c => c.Importe);
        decimal Pct(decimal v) => total == 0 ? 0 : Math.Round(v * 100 / total, 1);
        var lista = Reporte.Categorias.Where(c => c.Slot != "gris" && c.Importe > 0)
            .Select(c => (c.Nombre, c.Importe, Pct(c.Importe), c.Slot)).ToList();
        var otras = Reporte.Categorias.Where(c => c.Slot == "gris").Sum(c => c.Importe);
        if (otras > 0) lista.Add(("Otras", otras, Pct(otras), "gris"));
        Distribucion = lista;
    }
}
